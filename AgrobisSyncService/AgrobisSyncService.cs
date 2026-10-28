using AgrobisSyncService.Constants;
using AgrobisSyncService.Helpers;
using AgrobisSyncService.Services;
using Serilog;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Helpers;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;
using TechagroSyncServices.Shared.Services;

namespace AgrobisSyncService
{
    public partial class AgrobisSyncService : SyncServiceBase
    {
        // Services
        private readonly ProductsSyncService _apiService;
        private readonly IProductSyncService _productSyncService;
        private readonly IEmailService _emailService;

        private readonly HttpClient _httpClient;

        private DateTime _lastSnapshotSave = DateTime.MinValue;

        public AgrobisSyncService()
        {
            // Repositories
            string connectionString = ConfigHelper.GetConnenctionString();
            var productRepository = new ProductRepository(connectionString);

            _httpClient = new HttpClient();

            // Services
            _productSyncService = new ProductSyncService(productRepository);
            _emailService = new EmailService(CommonSettingsLoader.LoadSmtpSettings());
            _apiService = new ProductsSyncService(_httpClient);

            InitializeComponent();
        }

        protected override async Task RunSyncAsync()
        {
            // 1. Get AppSettings
            var defaultMargin = CommonSettingsLoader.GetDefaultMargin();
            var marginRanges = CommonSettingsLoader.GetMarginRanges();

            // 2. Getting info about products
            var productDtos = await _apiService.FetchProducts();

            var products = productDtos.Select(p => BuildHelper.BuildProductDto(p, defaultMargin, marginRanges)).ToList();

            if (_lastSnapshotSave < DateTime.Today && DateTime.Now.Hour >= 6)
            {
                // Step 3.1: Detect newly added products
                var exportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ServiceConstants.ExportFolder);
                var newProductsFolder = Path.Combine(exportPath, ServiceConstants.NewProductsFolder);
                var newProductsFileName = string.Format(ServiceConstants.NewProductsFileNameFormat, DateTime.Today.ToString("dd-MM-yyyy"));
                var snapshotPath = Path.Combine(exportPath, ServiceConstants.SnapshotFileName);
                var newProducts = await SnapshotChangeDetector.DetectNewAsync(snapshotPath, products, p => p.Code);

                if (newProducts.Any() && newProducts.Count <= 1500)
                {
                    // Step 3.2: Send notification email about new products
                    var to = CommonSettingsLoader.GetEmailsToNotify();
                    await BatchEmailNotifier.SendAsync(
                        newProducts,
                        100,
                        batch => $"Nowe produkty Agrobis ({newProducts.Count})",
                        batch => HtmlHelper.BuildNewProductsEmailHtml(batch, "Agrobis"),
                        recipients: to,
                        from: "Agrobis Sync Service",
                        emailService: _emailService);

                    Log.Information("Detected {Count} new products. Notification email sent to: {Recipients}", newProducts.Count, string.Join(", ", to));

                    // Step 3.3: Save CSV snapshot of new products
                    var exportedFileName = await SnapshotChangeDetector.SaveProductsSnapshotCsvAsync(newProductsFolder, newProductsFileName, newProducts);
                    Log.Information("CSV snapshot of new products saved at {Path}", exportedFileName);

                    // Step 3.4: Clean old snapshots
                    var daysToKeep = CommonSettingsLoader.GetFilesExpirationDays();
                    SnapshotChangeDetector.CleanOldSnapshots(newProductsFolder, daysToKeep);
                }
                else
                {
                    Log.Information("No new products detected.");
                }

                // Step 4: Export to JSON
                Log.Information($"Exporting product data to json file...");
                await SnapshotChangeDetector.SaveSnapshotAsync(snapshotPath, products);
                Log.Information("JSON file created at {Path}", snapshotPath);
                _lastSnapshotSave = DateTime.Today;
            }

            // Step 5: Filter by import list
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
                    Log.Warning("Missing: {Code}", code);
            }

            // Step 6: Delete products not in the current import list
            await _productSyncService.DeleteNotSyncedProducts(allowedCodes, ServiceConstants.Company);

            // Step 7: Sync current products
            await _productSyncService.SyncToDatabaseAsync(products);
        }
    }
}
