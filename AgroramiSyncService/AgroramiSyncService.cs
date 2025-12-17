using AgroramiSyncService.Helpers;
using AgroramiSyncService.Services;
using Serilog;
using System;
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

namespace AgroramiSyncService
{
    public partial class AgroramiSyncService : ServiceBase
    {
        private readonly TimeSpan _interval;

        // Services
        private readonly ApiService _apiService;

        private readonly IProductSyncService _productSyncService;
        private readonly IEmailService _emailService;

        private Timer _timer;
        private DateTime _lastSnapshotSave = DateTime.MinValue;

        public AgroramiSyncService()
        {
            // Serilog configuration and initialization
            int logsExpirationDays = AppSettingsLoader.GetLogsExpirationDays();
            LogConfig.Configure(logsExpirationDays);

            _interval = AppSettingsLoader.GetFetchInterval();

            // Repositories
            string connectionString = ConfigHelper.GetConnenctionString();
            var productRepository = new ProductRepository(connectionString);

            // Services
            _productSyncService = new ProductSyncService(productRepository);
            _emailService = new EmailService(AppSettingsLoader.LoadSmtpSettings());
            _apiService = new ApiService();

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
                // 1. Getting info about products
                var products = await _apiService.SyncProducts();

                if (_lastSnapshotSave.Date < DateTime.Today && DateTime.Now.Hour >= 6)
                {
                    // Step 2: Detect newly added products
                    var snapshotPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Export", $"products.json");
                    var newProducts = await SnapshotChangeDetector.DetectNewAsync(snapshotPath, products, p => p.Code);

                    if (newProducts.Any())
                    {
                        var to = AppSettingsLoader.GetEmailsToNotify();

                        await BatchEmailNotifier.SendAsync(
                            newProducts,
                            100,
                            batch => $"Nowe produkty ({newProducts.Count})",
                            batch => HtmlHelper.BuildNewProductsEmailHtml(batch, "Agrorami"),
                            recipients: to,
                            from: "Agrorami Sync Service",
                            emailService: _emailService);
                    }
                    else
                    {
                        Log.Information("No new products detected.");
                    }

                    // Step 3: Export to JSON
                    await SnapshotChangeDetector.SaveSnapshotAsync(snapshotPath, products);
                    Log.Information("JSON file created at {Path}", snapshotPath);
                    _lastSnapshotSave = DateTime.Today;
                }

                // Step 4: Filter by import list
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

                // Step 5: Delete products not in the current import list
                await _productSyncService.DeleteNotSyncedProducts(allowedCodes, IntegrationCompany.AGRORAMI);

                // Step 6: Sync current products
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