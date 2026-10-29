using HermonSyncService.Constants;
using HermonSyncService.DTOs;
using HermonSyncService.Helpers;
using HermonSyncService.Services;
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

namespace HermonSyncService
{
    public partial class HermonSyncService : SyncServiceBase
    {
        // Services
        private readonly ProductService _productService;

        private readonly IProductSyncService _productSyncService;
        private readonly IEmailService _emailService;
        private readonly NewProductsNotifier _newProductsNotifier;

        private DateTime _lastSnapshotSave = DateTime.MinValue;
        private DateTime _lastImagesSync = DateTime.MinValue;

        public HermonSyncService()
        {
            // Settings
            var smtpSettings = CommonSettingsLoader.LoadSmtpSettings();

            // Repositories
            string connectionString = ConfigHelper.GetConnenctionString();
            var productRepository = new ProductRepository(connectionString);

            // Services
            _productService = new ProductService();
            _productSyncService = new ProductSyncService(productRepository);
            _emailService = new EmailService(smtpSettings);
            _newProductsNotifier = new NewProductsNotifier(_emailService, "Hermon");

            InitializeComponent();
        }

        protected override async Task RunSyncAsync()
        {
            var images = new List<FtpImage>();

            // Import list (codes with or without "HR" suffix)
            var importFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ServiceConstants.ImportCodesFilePath);
            var importCodes = FileUtils.ReadImportCodes(importFilePath, ServiceConstants.CodeSuffix);

            // 1. Getting default info about products
            var basicProductData = await _productService.SyncProductsFromFtp();

            // 2. Getting product images once a day, only for products from import list
            // FTP errors only postpone images, prices and stock are synced anyway
            if (_lastImagesSync < DateTime.Today && importCodes.Any())
            {
                try
                {
                    images = _productService.SyncImagesFromFtp(importCodes.Select(c => c.SupplierCode));
                    _lastImagesSync = DateTime.Today;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to download images from FTP. They will be downloaded in next run.");
                }
            }

            // 3. Getting detailed info about products from API
            var detailsResult = await _productService.FetchProductDetailsFromApi(basicProductData);

            // FTP errors return an empty product list
            bool isFetchComplete = basicProductData.Any() && detailsResult.IsComplete;

            // 4. Building full product data
            var products = await _productService.BuildProductDtos(basicProductData, detailsResult.Items, images);

            // 5. Report new products once a day (errors never stop the sync)
            if (_lastSnapshotSave < DateTime.Today && DateTime.Now.Hour >= 6 && await _newProductsNotifier.DetectAndNotifyAsync(products, isFetchComplete))
                _lastSnapshotSave = DateTime.Today;

            // 6. Filter by import list
            var allowedCodes = importCodes
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

            // 7.1. Delete products not in the current import list or no longer returned by API
            await _productSyncService.DeleteNotSyncedProducts(ServiceConstants.Company, allowedCodes, fetchedCodes, isFetchComplete);

            // 7.2. Sync current products
            await _productSyncService.SyncToDatabaseAsync(products);
        }
    }
}