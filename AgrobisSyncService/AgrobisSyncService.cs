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
        private readonly NewProductsNotifier _newProductsNotifier;

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
            _newProductsNotifier = new NewProductsNotifier(_emailService, "Agrobis", maxNewProducts: 1500);
            _apiService = new ProductsSyncService(_httpClient);

            InitializeComponent();
        }

        protected override async Task RunSyncAsync()
        {
            // 1. Get AppSettings
            var defaultMargin = CommonSettingsLoader.GetDefaultMargin();
            var marginRanges = CommonSettingsLoader.GetMarginRanges();

            // 2. Getting info about products
            var fetchResult = await _apiService.FetchProducts();

            var products = fetchResult.Items.Select(p => BuildHelper.BuildProductDto(p, defaultMargin, marginRanges)).ToList();

            // Step 3: Report new products once a day (errors never stop the sync)
            if (_lastSnapshotSave < DateTime.Today && DateTime.Now.Hour >= 6 && await _newProductsNotifier.DetectAndNotifyAsync(products, fetchResult.IsComplete))
                _lastSnapshotSave = DateTime.Today;

            // Step 5: Filter by import list (codes with or without "AB" prefix)
            var importFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ServiceConstants.ImportCodesFilePath);

            var allowedCodes = FileUtils.ReadImportCodes(importFilePath, ServiceConstants.CodePrefix)
                .Select(c => c.DatabaseCode)
                .ToList();

            if (!allowedCodes.Any())
            {
                Log.Warning("Import file is empty or missing. Aborting import");
                return;
            }

            var fetchedCodes = products.Select(p => p.Code).ToList();
            products = ImportFilterHelper.FilterByAllowedCodes(products, allowedCodes, p => p.Code, out var missingCodes).ToList();

            if (missingCodes.Any())
            {
                Log.Warning("Missing {Count} product codes", missingCodes.Count);
                foreach (var code in missingCodes)
                    Log.Warning("Missing: {Code}", code);
            }

            // Step 6: Delete products not in the current import list or no longer returned by API
            await _productSyncService.DeleteNotSyncedProducts(ServiceConstants.Company, allowedCodes, fetchedCodes, fetchResult.IsComplete);

            // Step 7: Sync current products
            await _productSyncService.SyncToDatabaseAsync(products);
        }
    }
}
