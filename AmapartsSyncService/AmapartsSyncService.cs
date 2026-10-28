using AmapartsSyncService.Constants;
using AmapartsSyncService.Helpers;
using AmapartsSyncService.Services;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Helpers;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;
using TechagroSyncServices.Shared.Services;

namespace AmapartsSyncService
{
    public partial class AmapartsSyncService : SyncServiceBase
    {
        // Services
        private readonly ApiService _apiService;

        private readonly IProductSyncService _productSyncService;
        private readonly IEmailService _emailService;

        private readonly HttpClient _httpClient;

        private DateTime _lastSnapshotSave = DateTime.MinValue;

        public AmapartsSyncService()
        {
            // Settings
            var smtpSettings = CommonSettingsLoader.LoadSmtpSettings();

            // Repositories
            string connectionString = ConfigHelper.GetConnenctionString();
            var productRepository = new ProductRepository(connectionString);

            _httpClient = new HttpClient();

            // Services
            _apiService = new ApiService(_httpClient);
            _productSyncService = new ProductSyncService(productRepository);
            _emailService = new EmailService(smtpSettings);

            InitializeComponent();
        }

        protected override async Task RunSyncAsync()
        {
            var defaultMargin = CommonSettingsLoader.GetDefaultMargin();
            var marginRanges = CommonSettingsLoader.GetMarginRanges();

            // Step 1: Fetch products from API
            Log.Information($"Fetching products from API...");
            var products = await _apiService.FetchProductsDetails();
            if (products == null || products.Count == 0)
            {
                Log.Warning("No products fetched from API. Aborting synchronization.");
                return;
            }
            Log.Information("Fetched {Count} products from API.", products.Count);

            // Step 2: Fetch parameters from API
            Log.Information($"Fetching parameters from API...");
            var parameters = await _apiService.FetchParameters();
            if (parameters == null || parameters.Count == 0)
            {
                Log.Warning("No parameters fetched from API. Aborting synchronization.");
                return;
            }
            Log.Information("Fetched {Count} parameters from API.", parameters.Count);

            // Step 3: Group parameters by product code
            Log.Information("Groupping parameters...");
            var parametersGrouped = GroupHelper.GroupParameters(parameters);

            // Step 4: Build full product data
            Log.Information($"Building product data...");
            var fullProducts = products.Select(p => BuildHelper.BuildProductDto(p, parametersGrouped, defaultMargin, marginRanges)).ToList();

            if (_lastSnapshotSave < DateTime.Today && DateTime.Now.Hour >= 6)
            {
                // Step 5.1: Detect newly added products
                var exportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ServiceConstants.ExportFolder);
                var newProductsFolder = Path.Combine(exportPath, ServiceConstants.NewProductsFolder);
                var newProductsFileName = string.Format(ServiceConstants.NewProductsFileNameFormat, DateTime.Today.ToString("dd-MM-yyyy"));
                var snapshotPath = Path.Combine(exportPath, ServiceConstants.SnapshotFileName);
                var newProducts = await SnapshotChangeDetector.DetectNewAsync(snapshotPath, fullProducts, p => p.Code);

                if (newProducts.Any())
                {
                    // Step 5.2: Send notification email about new products
                    var to = CommonSettingsLoader.GetEmailsToNotify();
                    await BatchEmailNotifier.SendAsync(
                        newProducts,
                        100,
                        batch => $"Nowe produkty Ama ({newProducts.Count})",
                        batch => HtmlHelper.BuildNewProductsEmailHtml(batch, "Ama"),
                        recipients: to,
                        from: "Ama Sync Service",
                        emailService: _emailService);

                    Log.Information("Detected {Count} new products. Notification email sent to: {Recipients}", newProducts.Count, string.Join(", ", to));

                    // Step 5.3: Save CSV snapshot of new products
                    var exportedFileName = await SnapshotChangeDetector.SaveProductsSnapshotCsvAsync(newProductsFolder, newProductsFileName, newProducts);
                    Log.Information("CSV snapshot of new products saved at {Path}", exportedFileName);

                    // Step 5.4: Clean old snapshots
                    var daysToKeep = CommonSettingsLoader.GetFilesExpirationDays();
                    SnapshotChangeDetector.CleanOldSnapshots(newProductsFolder, daysToKeep);
                }
                else
                {
                    Log.Information("No new products detected.");
                }

                // Step 6: Export to JSON
                Log.Information($"Exporting product data to json file...");
                await SnapshotChangeDetector.SaveSnapshotAsync(snapshotPath, fullProducts);
                Log.Information("JSON file created at {Path}", snapshotPath);
                _lastSnapshotSave = DateTime.Today;
            }

            // Step 7: Filter by import list
            Log.Information($"Filtering products by import list...");
            var importFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ServiceConstants.ImportCodesFilePath);
            var allowedCodes = FileUtils.ReadImportList(importFilePath);

            if (!allowedCodes.Any())
            {
                Log.Warning("Import file is empty or missing. Aborting import");
                return;
            }

            fullProducts = ImportFilterHelper.FilterByAllowedCodes(fullProducts, allowedCodes, p => p.Code, out var missingCodes).ToList();

            if (missingCodes.Any())
            {
                Log.Warning("Missing {Count} product codes", missingCodes.Count);
                foreach (var code in missingCodes)
                    Log.Warning("Missing: {Code}", code);
            }

            // Step 8.1: Delete products not in the current import list
            await _productSyncService.DeleteNotSyncedProducts(allowedCodes, ServiceConstants.Company);

            // Step 8.2: Sync current products
            await _productSyncService.SyncToDatabaseAsync(fullProducts);
        }
    }
}