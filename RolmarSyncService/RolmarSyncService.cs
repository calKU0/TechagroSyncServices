using RolmarSyncService.Constants;
using RolmarSyncService.DTOs;
using RolmarSyncService.Helpers;
using RolmarSyncService.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Helpers;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;
using TechagroSyncServices.Shared.Services;

namespace RolmarSyncService
{
    public partial class RolmarSyncService : SyncServiceBase
    {
        // Services
        private readonly ApiService _apiService;

        private readonly IProductSyncService _productSyncService;
        private readonly IEmailService _emailService;
        private readonly NewProductsNotifier _newProductsNotifier;
        private readonly ISyncStateService _syncStateService;

        private readonly HttpClient _httpClient;

        private DateTime _lastSnapshotSave = DateTime.MinValue;

        public RolmarSyncService()
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
            _newProductsNotifier = new NewProductsNotifier(_emailService, "Rolmar");
            _syncStateService = new FileSyncStateService();

            InitializeComponent();
        }

        protected override async Task RunSyncAsync()
        {
            Log.Information($"Starting synchronization...");

            var defaultMargin = CommonSettingsLoader.GetDefaultMargin();
            var marginRanges = CommonSettingsLoader.GetMarginRanges();

            // Step 1: Fetch products from API
            Log.Information($"Fetching products from API...");
            var products = await _apiService.FetchProducts();
            if (products == null || !products.Any())
            {
                Log.Warning("No products found in API response. Aborting sync");
                return;
            }
            Log.Information("Fetched {Count} products from API.", products.Count);

            // Step 2: Fetch stock from API
            Log.Information($"Fetching stock from API...");
            var stock = await _apiService.FetchProductStock();
            if (stock == null || !stock.Any())
            {
                Log.Warning("No stock found in API response. Aborting sync");
                return;
            }
            Log.Information("Fetched {Count} stock from API.", stock.Count);

            // Step 3: Fetch images from API
            var importFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ServiceConstants.ImportCodesFilePath);
            int previousProductsCount = _syncStateService.GetLastProductsCount();
            int currentProductsCount = FileUtils.ReadImportList(importFilePath).Count;
            bool productsChanged = previousProductsCount == 0 || previousProductsCount != currentProductsCount;

            List<PhotoItem> images = new List<PhotoItem>();
            if (productsChanged)
            {
                Log.Information($"Fetching images from API...");
                images = await _apiService.FetchProductImages();
                Log.Information("Fetched {Count} images from API.", images.Count);
                _syncStateService.SetLastProductsCount(currentProductsCount);
            }

            // Step 4: Build full products data
            Log.Information($"Building product data...");
            var fullProducts = BuildHelper.BuildProductDtos(products, stock, images, defaultMargin, marginRanges);

            // Step 5: Report new products once a day (errors never stop the sync)
            if (_lastSnapshotSave < DateTime.Today && DateTime.Now.Hour >= 6 && await _newProductsNotifier.DetectAndNotifyAsync(fullProducts, true))
                _lastSnapshotSave = DateTime.Today;

            // Step 7: Filter by import list
            Log.Information($"Filtering products by import list...");
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

            // Step 8.1: Delete products not in the current import list or no longer returned by API.
            // Products and stock are single requests and their errors abort the sync above, so here the data is complete
            await _productSyncService.DeleteNotSyncedProducts(ServiceConstants.Company, allowedCodes, fetchedCodes, true);

            // Step 8.2: Sync current products
            await _productSyncService.SyncToDatabaseAsync(fullProducts);
        }
    }
}