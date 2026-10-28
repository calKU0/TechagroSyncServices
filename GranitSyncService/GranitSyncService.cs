using GranitSyncService.Clients;
using GranitSyncService.Constants;
using GranitSyncService.Helpers;
using GranitSyncService.Services;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;
using TechagroSyncServices.Shared.Services;

namespace GranitSyncService
{
    public partial class GranitSyncService : SyncServiceBase
    {
        // Services
        private readonly SyncService _syncService;
        private readonly IProductSyncService _productSyncService;

        public GranitSyncService()
        {
            // Repositories
            string connectionString = ConfigHelper.GetConnenctionString();
            var productRepository = new ProductRepository(connectionString);

            var apiSettings = AppSettingsLoader.LoadApiSettings();

            // Services
            _productSyncService = new ProductSyncService(productRepository);
            var granitApiClient = new GranitApiClient(apiSettings);
            _syncService = new SyncService(granitApiClient, apiSettings);

            InitializeComponent();
        }

        protected override async Task RunSyncAsync()
        {
            // 1. Get AppSettings
            var defaultMargin = CommonSettingsLoader.GetDefaultMargin();
            var marginRanges = CommonSettingsLoader.GetMarginRanges();

            // 2. Filter by import list
            var importFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ServiceConstants.ImportCodesFilePath);
            var allowedCodes = FileUtils.ReadImportList(importFilePath);

            if (!allowedCodes.Any())
            {
                Log.Warning("Import file is empty or missing. Aborting import");
                return;
            }

            // 2. Getting info about products
            var productDtos = await _syncService.FetchProductStockAndPrices(allowedCodes);
            var products = productDtos.Select(p => BuildHelper.BuildProductDto(p, defaultMargin, marginRanges)).ToList();

            // 3. Delete products not in the current import list
            await _productSyncService.DeleteNotSyncedProducts(allowedCodes, ServiceConstants.Company);

            // 4. Sync current products
            await _productSyncService.SyncToDatabaseAsync(products);
        }
    }
}
