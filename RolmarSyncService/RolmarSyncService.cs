using RolmarSyncService.DTOs;
using RolmarSyncService.Helpers;
using RolmarSyncService.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using TechagroApiSync.Shared.DTOs;
using TechagroApiSync.Shared.Enums;
using TechagroApiSync.Shared.Helpers;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Logging;
using TechagroSyncServices.Shared.Repositories;
using TechagroSyncServices.Shared.Services;

namespace RolmarSyncService
{
    public partial class RolmarSyncService : ServiceBase
    {
        private readonly TimeSpan _interval;

        // Services
        private readonly ApiService _apiService;

        private readonly IProductSyncService _productSyncService;
        private readonly IEmailService _emailService;

        private readonly HttpClient _httpClient;

        private Timer _timer;
        private DateTime _lastProductDetailsSyncDate = DateTime.MinValue;

        public RolmarSyncService()
        {
            // Serilog configuration and initialization
            int logsExpirationDays = AppSettingsLoader.GetLogsExpirationDays();
            LogConfig.Configure(logsExpirationDays);

            _interval = AppSettingsLoader.GetFetchInterval();

            // Settings
            var smtpSettings = AppSettingsLoader.LoadSmtpSettings();

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

        protected override void OnStart(string[] args)
        {
            _timer = new Timer(
                async _ => await TimerTickAsync(),
                null,
                TimeSpan.Zero,
                Timeout.InfiniteTimeSpan
            );

            Log.Information("Service started. First run immediately. Interval: {Interval}", _interval);
        }

        protected override void OnStop()
        {
            Log.Information("Service stopped.");
            Log.CloseAndFlush();
        }

        private async Task TimerTickAsync()
        {
            try
            {
                Log.Information($"Starting synchronization...");

                var defaultMargin = AppSettingsLoader.GetDefaultMargin();
                var marginRanges = AppSettingsLoader.GetMarginRanges();

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
                List<PhotoItem> images = new List<PhotoItem>();
                //if (DateTime.Now.Day % 20 == 0)
                //{
                //    Log.Information($"Fetching images from API...");
                //    images = await _apiService.FetchProductImages();
                //    Log.Information("Fetched {Count} images from API.", images.Count);
                //}

                // Step 4: Build full products data
                Log.Information($"Building product data...");
                var fullProducts = BuildHelper.BuildProductDtos(products, stock, images, defaultMargin, marginRanges);

                if (_lastProductDetailsSyncDate.Date < DateTime.Today && DateTime.Now.Hour >= 6)
                {
                    // Step 5.1: Detect newly added products
                    Log.Information($"Detecting new products...");
                    var snapshotPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Export", $"products.json");
                    var newProducts = await SnapshotChangeDetector.DetectNewAsync(snapshotPath, fullProducts, p => p.Code);

                    // Step 5.2: Send notification email about new products
                    if (newProducts.Any())
                    {
                        Log.Information($"Sending notification emails...");
                        var to = AppSettingsLoader.GetEmailsToNotify();

                        await BatchEmailNotifier.SendAsync(
                            newProducts,
                            100,
                            batch => $"Nowe produkty Rolmar ({newProducts.Count})",
                            batch => HtmlHelper.BuildNewProductsEmailHtml(batch, "Rolmar"),
                            recipients: to,
                            from: "Rolmar Sync Service",
                            emailService: _emailService);
                    }
                    else
                    {
                        Log.Information("No new products detected.");
                    }

                    // Step 6: Export to JSON
                    Log.Information($"Exporting product data to json file...");
                    await SnapshotChangeDetector.SaveSnapshotAsync(snapshotPath, fullProducts);
                    Log.Information("JSON file created at {Path}", snapshotPath);
                    _lastProductDetailsSyncDate = DateTime.Today;
                }

                // Step 7: Filter by import list
                Log.Information($"Filtering products by import list...");
                var importFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Import", "numery_katalogowe.txt");
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
                await _productSyncService.DeleteNotSyncedProducts(allowedCodes, IntegrationCompany.ROLMAR);

                // Step 8.2: Sync current products
                await _productSyncService.SyncToDatabaseAsync(fullProducts);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during API synchronization.");
            }
            finally
            {
                DateTime nextRun = DateTime.Now.AddHours(_interval.TotalHours);
                _timer.Change(_interval, Timeout.InfiniteTimeSpan);

                Log.Information("All processes completed. Next run scheduled at: {NextRun}", nextRun);
            }
        }
    }
}