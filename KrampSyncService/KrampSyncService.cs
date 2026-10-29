using KrampSyncService.Clients;
using KrampSyncService.Constants;
using KrampSyncService.Helpers;
using KrampSyncService.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;
using TechagroSyncServices.Shared.Services;

namespace KrampSyncService
{
    public partial class KrampSyncService : SyncServiceBase
    {
        // Services
        private readonly SyncService _syncService;
        private readonly IProductSyncService _productSyncService;

        public KrampSyncService()
        {
            // Repositories
            string connectionString = ConfigHelper.GetConnenctionString();
            var productRepository = new ProductRepository(connectionString);

            var apiSettings = AppSettingsLoader.LoadApiSettings();
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Services
            _productSyncService = new ProductSyncService(productRepository);
            var limiter = new RequestLimiter(apiSettings, Path.Combine(baseDirectory, ServiceConstants.RequestsStateFilePath));
            var krampApiClient = new KrampApiClient(apiSettings, limiter);
            _syncService = new SyncService(
                krampApiClient,
                limiter,
                apiSettings,
                Path.Combine(baseDirectory, ServiceConstants.ProductsStateFilePath));

            InitializeComponent();
        }

        protected override async Task RunSyncAsync()
        {
            // 1. Get AppSettings
            var defaultMargin = CommonSettingsLoader.GetDefaultMargin();
            var marginRanges = CommonSettingsLoader.GetMarginRanges();

            // 2. Filter by import list (codes with or without "KR" suffix).
            // Kramp SKUs are case sensitive and always upper case (e.g. "62042RS").
            var importFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ServiceConstants.ImportCodesFilePath);
            var importCodes = FileUtils.ReadImportCodes(importFilePath, ServiceConstants.CodeSuffix, code => code.ToUpperInvariant());

            if (!importCodes.Any())
            {
                Log.Warning("Import file is empty or missing. Aborting import");
                return;
            }

            // 3. Getting stock and prices for products from the import list - Kramp API knows only codes without suffix
            var skus = importCodes.Select(c => c.SupplierCode).ToList();
            var productStates = await _syncService.RefreshProductsAsync(skus);
            var products = productStates.Select(p => BuildHelper.BuildProductDto(p, defaultMargin, marginRanges)).ToList();

            // 4. Delete products not in the current import list or no longer sold by Kramp.
            // Prices are refreshed in parts because of the daily limit, so only SKUs confirmed as missing are deleted.
            var missingSkus = new HashSet<string>(_syncService.GetMissingSkus(), StringComparer.OrdinalIgnoreCase);
            var availableCodes = importCodes.Where(c => !missingSkus.Contains(c.SupplierCode)).Select(c => c.DatabaseCode);
            var allowedCodes = importCodes.Select(c => c.DatabaseCode).ToList();
            await _productSyncService.DeleteNotSyncedProducts(ServiceConstants.Company, allowedCodes, availableCodes, true);

            // 5. Sync refreshed products
            await _productSyncService.SyncToDatabaseAsync(products);
        }
    }
}
