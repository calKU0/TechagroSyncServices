using HermonSyncService.DTOs;
using HermonSyncService.Helpers;
using HermonSyncService.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Enums;
using TechagroApiSync.Shared.Helpers;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Logging;
using TechagroSyncServices.Shared.Repositories;
using TechagroSyncServices.Shared.Services;

namespace HermonSyncService
{
    public partial class HermonSyncService : ServiceBase
    {
        private readonly TimeSpan _interval;

        // Services
        private readonly ProductService _productService;

        private readonly IProductSyncService _productSyncService;
        private readonly IEmailService _emailService;

        private Timer _timer;
        private DateTime _lastSnapshotSave = DateTime.MinValue;

        public HermonSyncService()
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

            // Services
            _productService = new ProductService();
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
                var images = new List<FtpImage>();

                // 1. Getting default info about products
                var basicProductData = await _productService.SyncProductsFromFtp();

                // 2.Getting product images
                if (_lastSnapshotSave < DateTime.Today)
                {
                    images = _productService.SyncImagesFromFtp();
                }

                // 3. Getting detailed info about products from API
                var detailedProductData = await _productService.FetchProductDetailsFromApi(basicProductData);

                // 4. Building full product data
                var products = await _productService.BuildProductDtos(basicProductData, detailedProductData, images);

                if (_lastSnapshotSave < DateTime.Today && DateTime.Now.Hour >= 6)
                {
                    // Step 5.1: Detect newly added products
                    var exportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Export");
                    var newProductsFolder = Path.Combine(exportPath, "Nowe");
                    var newProductsFileName = $"nowe-produkty-{DateTime.Today.ToString("dd-MM-yyyy")}.csv";
                    var snapshotPath = Path.Combine(exportPath, $"products.json");
                    var newProducts = await SnapshotChangeDetector.DetectNewAsync(snapshotPath, products, p => p.Code);

                    if (newProducts.Any())
                    {
                        // Step 5.2: Send notification email about new products
                        var to = AppSettingsLoader.GetEmailsToNotify();
                        await BatchEmailNotifier.SendAsync(
                            newProducts,
                            100,
                            batch => $"Nowe produkty Hermon ({newProducts.Count})",
                            batch => HtmlHelper.BuildNewProductsEmailHtml(batch, "Hermon"),
                            recipients: to,
                            from: "Hermon Sync Service",
                            emailService: _emailService);

                        Log.Information("Detected {Count} new products. Notification email sent to: {Recipients}", newProducts.Count, string.Join(", ", to));

                        // Step 5.3: Save CSV snapshot of new products
                        var exportedFileName = await SnapshotChangeDetector.SaveProductsSnapshotCsvAsync(newProductsFolder, newProductsFileName, newProducts);
                        Log.Information("CSV snapshot of new products saved at {Path}", exportedFileName);

                        // Step 5.4: Clean old snapshots
                        var daysToKeep = AppSettingsLoader.GetFilesExpirationDays();
                        SnapshotChangeDetector.CleanOldSnapshots(newProductsFolder, daysToKeep);
                    }
                    else
                    {
                        Log.Information("No new products detected.");
                    }

                    // Step 6: Export to JSON
                    await SnapshotChangeDetector.SaveSnapshotAsync(snapshotPath, products);
                    Log.Information("JSON file created at {Path}", snapshotPath);
                    _lastSnapshotSave = DateTime.Today;
                }

                // Step 7: Filter by import list
                var importFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Import", "numery_katalogowe.txt");

                var allowedCodes = FileUtils.ReadImportList(importFilePath);

                if (!allowedCodes.Any())
                {
                    Log.Warning("Import file is empty or missing. Aborting import");
                    return;
                }

                products = ImportFilterHelper.FilterByAllowedCodes(products, allowedCodes, p => p.Code, out var missingCodes).ToList();

                if (missingCodes.Any())
                {
                    Log.Warning("Missing {Count} product codes", missingCodes.Count);
                    foreach (var code in missingCodes)
                        Log.Warning("Missing: {Code}", code);
                }

                // Step 8.1: Delete products not in the current import list
                await _productSyncService.DeleteNotSyncedProducts(allowedCodes, IntegrationCompany.HERMON);

                // Step 8.2: Sync current products
                await _productSyncService.SyncToDatabaseAsync(products);
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