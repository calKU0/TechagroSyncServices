using HiFiSyncService.Clients;
using HiFiSyncService.Constants;
using HiFiSyncService.Helpers;
using HiFiSyncService.Services;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;
using TechagroSyncServices.Shared.Services;

namespace HiFiSyncService
{
    public partial class HiFiSyncService : SyncServiceBase
    {
        // Services
        private readonly SyncService _syncService;
        private readonly IProductSyncService _productSyncService;

        public HiFiSyncService()
        {
            // Repositories
            string connectionString = ConfigHelper.GetConnenctionString();
            var productRepository = new ProductRepository(connectionString);

            var apiSettings = AppSettingsLoader.LoadApiSettings();

            // Services
            _productSyncService = new ProductSyncService(productRepository);
            var throttler = new RequestThrottler(apiSettings.RequestsPerMinute);
            var hifiApiClient = new HiFiApiClient(apiSettings, throttler);
            _syncService = new SyncService(hifiApiClient);

            InitializeComponent();
        }

        protected override async Task RunSyncAsync()
        {
            // 1. Get AppSettings
            var defaultMargin = CommonSettingsLoader.GetDefaultMargin();
            var marginRanges = CommonSettingsLoader.GetMarginRanges();

            // 2. Filter by import list (codes with or without "HF" suffix).
            // HIFI references are case sensitive and always upper case (e.g. "SN 001").
            var importFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ServiceConstants.ImportCodesFilePath);
            var importCodes = FileUtils.ReadImportCodes(importFilePath, ServiceConstants.CodeSuffix, code => code.ToUpperInvariant());

            if (!importCodes.Any())
            {
                Log.Warning("Import file is empty or missing. Aborting import");
                return;
            }

            // 3. Getting live prices and stock only for products from the import list, so no filtering is needed later
            var references = importCodes.Select(c => c.SupplierCode).ToList();

            var fetchResult = await _syncService.FetchProductsAsync(references);
            var products = fetchResult.Items.Select(p => BuildHelper.BuildProductDto(p, defaultMargin, marginRanges)).ToList();

            // 4. Delete products not in the current import list or no longer returned by API
            var allowedCodes = importCodes.Select(c => c.DatabaseCode).ToList();
            await _productSyncService.DeleteNotSyncedProducts(ServiceConstants.Company, allowedCodes, products.Select(p => p.Code), fetchResult.IsComplete);

            // 5. Sync current products
            await _productSyncService.SyncToDatabaseAsync(products);
        }
    }
}
