using AgroramiSyncService.Constants;
using AgroramiSyncService.DTOs;
using AgroramiSyncService.Helpers;
using AgroramiSyncService.Services;
using Serilog;
using System;
using System.Collections.Generic;
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
        private readonly IProductRepository _productRepository;

        private readonly IProductSyncService _productSyncService;
        private readonly IEmailService _emailService;
        private readonly NewProductsNotifier _newProductsNotifier;

        private DateTime _lastSnapshotSave = DateTime.MinValue;

        public AgroramiSyncService()
        {
            // Repositories
            string connectionString = ConfigHelper.GetConnenctionString();
            _productRepository = new ProductRepository(connectionString);

            // Services
            _productSyncService = new ProductSyncService(_productRepository);
            _emailService = new EmailService(CommonSettingsLoader.LoadSmtpSettings());
            _newProductsNotifier = new NewProductsNotifier(_emailService, "Agrorami", maxNewProducts: 1500);
            _apiService = new ApiService();

            InitializeComponent();
        }

        protected override async Task RunSyncAsync()
        {
            // 1. Report new products once a day (errors never stop the sync)
            if (_lastSnapshotSave < DateTime.Today && DateTime.Now.Hour >= 6 && await ReportNewProductsAsync())
                _lastSnapshotSave = DateTime.Today;

            // 2. Import list (codes with or without "AR" suffix)
            var importFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ServiceConstants.ImportCodesFilePath);
            var importCodes = FileUtils.ReadImportCodes(importFilePath, ServiceConstants.CodeSuffix);

            if (!importCodes.Any())
            {
                Log.Warning("Import file is empty or missing. Aborting import");
                return;
            }

            var allowedCodes = importCodes.Select(c => c.DatabaseCode).ToList();

            // 3. Getting prices and stocks of products from import list only
            var fetchResult = await _apiService.FetchProductsBySkusAsync(importCodes.Select(c => c.SupplierCode).ToList());
            var products = fetchResult.Items;

            // Express variants ("-EXP") of products from import list are synced too
            var fetchedCodes = products.Select(p => p.Code).ToList();
            products = ImportFilterHelper.FilterByAllowedCodes(products, allowedCodes, p => p.Code, out var missingCodes).ToList();

            if (missingCodes.Any())
            {
                Log.Warning("Missing {Count} product codes", missingCodes.Count);
                foreach (var code in missingCodes)
                    Log.Warning("Product: {Code} is in import file but wasn't found in API", code);
            }

            // 4. Delete products not in the current import list or no longer returned by API.
            // Express variants exist only while Agrorami offers express shipping, so the ones in database are expected in API too
            // and a variant no longer offered is deleted like any other missing product.
            List<string> expectedCodes;
            try
            {
                expectedCodes = allowedCodes.Concat(await GetExpressCodesInDatabaseAsync(allowedCodes)).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to read express variants from database. Skipping deletion of products.");
                expectedCodes = null;
            }

            if (expectedCodes != null)
                await _productSyncService.DeleteNotSyncedProducts(ServiceConstants.Company, expectedCodes, fetchedCodes, fetchResult.IsComplete);

            // 5. Sync current products
            await _productSyncService.SyncToDatabaseAsync(products);
        }

        private async Task<List<string>> GetExpressCodesInDatabaseAsync(IEnumerable<string> allowedCodes)
        {
            var allowedSet = new HashSet<string>(allowedCodes, StringComparer.OrdinalIgnoreCase);

            return (await _productRepository.GetProductCodesAsync(ServiceConstants.Company))
                .Where(code => code.EndsWith(ServiceConstants.ExpressSuffix, StringComparison.OrdinalIgnoreCase)
                    && allowedSet.Contains(code.Substring(0, code.Length - ServiceConstants.ExpressSuffix.Length)))
                .ToList();
        }

        // Whole offer has over 600k products, so new products are taken by creation date since the last successful check.
        // Returns false when the check failed and has to be repeated.
        private async Task<bool> ReportNewProductsAsync()
        {
            try
            {
                return await CheckNewProductsAsync();
            }
            catch (Exception ex)
            {
                // E.g. damaged or locked state file - prices and stock are synced anyway
                Log.Error(ex, "Failed to check new products. They will be checked again in next run.");
                return false;
            }
        }

        private async Task<bool> CheckNewProductsAsync()
        {
            var statePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ServiceConstants.NewProductsCheckFilePath);
            var state = FileUtils.ReadJson<NewProductsCheckState>(statePath);
            var now = DateTime.UtcNow;

            if (state.LastCheckUtc == null)
            {
                // Before this change new products were detected from the whole offer snapshot - continue from its date
                var legacySnapshotPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ServiceConstants.LegacySnapshotFilePath);
                if (!File.Exists(legacySnapshotPath))
                {
                    Log.Information("First check of new products. Products added from now on will be reported.");
                    FileUtils.WriteJson(statePath, new NewProductsCheckState { LastCheckUtc = now });
                    return true;
                }

                state.LastCheckUtc = File.GetLastWriteTimeUtc(legacySnapshotPath);
            }

            var created = await _apiService.FetchProductsCreatedSinceAsync(state.LastCheckUtc.Value, now);
            if (!created.IsComplete || !await _newProductsNotifier.NotifyAsync(created.Items))
                return false;

            FileUtils.WriteJson(statePath, new NewProductsCheckState { LastCheckUtc = now });
            return true;
        }
    }
}