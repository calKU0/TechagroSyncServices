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
        private readonly NewProductsNotifier _newProductsNotifier;

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
            _newProductsNotifier = new NewProductsNotifier(_emailService, "Ama");

            InitializeComponent();
        }

        protected override async Task RunSyncAsync()
        {
            var defaultMargin = CommonSettingsLoader.GetDefaultMargin();
            var marginRanges = CommonSettingsLoader.GetMarginRanges();

            // Step 1: Fetch products from API
            Log.Information($"Fetching products from API...");
            var fetchResult = await _apiService.FetchProductsDetails();
            var products = fetchResult.Items;
            if (products.Count == 0)
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

            // Step 5: Report new products once a day (errors never stop the sync)
            if (_lastSnapshotSave < DateTime.Today && DateTime.Now.Hour >= 6 && await _newProductsNotifier.DetectAndNotifyAsync(fullProducts, fetchResult.IsComplete))
                _lastSnapshotSave = DateTime.Today;

            // Step 7: Filter by import list
            Log.Information($"Filtering products by import list...");
            var importFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ServiceConstants.ImportCodesFilePath);
            var allowedCodes = FileUtils.ReadImportList(importFilePath);

            if (!allowedCodes.Any())
            {
                Log.Warning("Import file is empty or missing. Aborting import");
                return;
            }

            var fetchedCodes = fullProducts.Select(p => p.Code).ToList();
            fullProducts = ImportFilterHelper.FilterByAllowedCodes(fullProducts, allowedCodes, p => p.Code, out var missingCodes).ToList();

            if (missingCodes.Any())
            {
                Log.Warning("Missing {Count} product codes", missingCodes.Count);
                foreach (var code in missingCodes)
                    Log.Warning("Missing: {Code}", code);
            }

            // Step 8.1: Delete products not in the current import list or no longer returned by API
            await _productSyncService.DeleteNotSyncedProducts(ServiceConstants.Company, allowedCodes, fetchedCodes, fetchResult.IsComplete);

            // Step 8.2: Sync current products
            await _productSyncService.SyncToDatabaseAsync(fullProducts);
        }
    }
}