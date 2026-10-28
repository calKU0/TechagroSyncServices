using AgroramiSyncService.Constants;
using AgroramiSyncService.Helpers;
using AgroramiSyncService.Services;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Helpers;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;
using TechagroSyncServices.Shared.Services;

namespace AgroramiSyncService
{
    public partial class AgroramiSyncService : SyncServiceBase
    {
        // Services
        private readonly ApiService _apiService;

        private readonly IProductSyncService _productSyncService;
        private readonly IEmailService _emailService;

        private DateTime _lastSnapshotSave = DateTime.MinValue;

        public AgroramiSyncService()
        {
            // Repositories
            string connectionString = ConfigHelper.GetConnenctionString();
            var productRepository = new ProductRepository(connectionString);

            // Services
            _productSyncService = new ProductSyncService(productRepository);
            _emailService = new EmailService(CommonSettingsLoader.LoadSmtpSettings());
            _apiService = new ApiService();

            InitializeComponent();
        }

        protected override async Task RunSyncAsync()
        {
            // 1. Getting info about products
            var products = await _apiService.SyncProducts();
            if (products == null || !products.Any())
            {
                Log.Warning("No products retrieved from API.");
                return;
            }

            if (_lastSnapshotSave < DateTime.Today && DateTime.Now.Hour >= 6)
            {
                // Step 2.1: Detect newly added products
                var exportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ServiceConstants.ExportFolder);
                var newProductsFolder = Path.Combine(exportPath, ServiceConstants.NewProductsFolder);
                var newProductsFileName = string.Format(ServiceConstants.NewProductsFileNameFormat, DateTime.Today.ToString("dd-MM-yyyy"));
                var snapshotPath = Path.Combine(exportPath, ServiceConstants.SnapshotFileName);
                var newProducts = await SnapshotChangeDetector.DetectNewAsync(snapshotPath, products, p => p.Code);

                if (newProducts.Any() && newProducts.Count <= 1500)
                {
                    // Step 2.2: Send notification email about new products
                    var to = CommonSettingsLoader.GetEmailsToNotify();
                    await BatchEmailNotifier.SendAsync(
                        newProducts,
                        100,
                        batch => $"Nowe produkty Agrorami ({newProducts.Count})",
                        batch => HtmlHelper.BuildNewProductsEmailHtml(batch, "Agrorami"),
                        recipients: to,
                        from: "Agrorami Sync Service",
                        emailService: _emailService);

                    Log.Information("Detected {Count} new products. Notification email sent to: {Recipients}", newProducts.Count, string.Join(", ", to));

                    // Step 2.3: Save CSV snapshot of new products
                    var exportedFileName = await SnapshotChangeDetector.SaveProductsSnapshotCsvAsync(newProductsFolder, newProductsFileName, newProducts);
                    Log.Information("CSV snapshot of new products saved at {Path}", exportedFileName);

                    // Step 2.4: Clean old snapshots
                    var daysToKeep = CommonSettingsLoader.GetFilesExpirationDays();
                    SnapshotChangeDetector.CleanOldSnapshots(newProductsFolder, daysToKeep);
                }
                else
                {
                    Log.Information("No new products detected.");
                }

                // Step 3: Export to JSON
                Log.Information($"Exporting product data to json file...");
                await SnapshotChangeDetector.SaveSnapshotAsync(snapshotPath, products);
                Log.Information("JSON file created at {Path}", snapshotPath);
                _lastSnapshotSave = DateTime.Today;
            }

            // Step 4: Filter by import list
            var importFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ServiceConstants.ImportCodesFilePath);

            var allowedCodes = FileUtils.ReadImportList(importFilePath);

            if (!allowedCodes.Any())
            {
                Log.Warning("Import file is empty or missing. Aborting import");
                return;
            }

            products = ImportFilterHelper.FilterByAllowedCodes(products, allowedCodes, p => p.Code, out var missingCodes).ToList();

            if (missingCodes.Any())
            {
                Log.Warning("Missing {Count} product codes", missingCodes.Count);
                foreach (var code in missingCodes)
                    Log.Warning("Product: {Code} is in import file but wasn't found in API", code);
            }

            // Step 5: Delete products not in the current import list
            await _productSyncService.DeleteNotSyncedProducts(allowedCodes, ServiceConstants.Company);

            // Step 6: Sync current products
            await _productSyncService.SyncToDatabaseAsync(products);
        }
    }
}