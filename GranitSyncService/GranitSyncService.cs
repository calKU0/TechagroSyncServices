using GranitSyncService.Clients;
using GranitSyncService.Constants;
using GranitSyncService.Helpers;
using GranitSyncService.Services;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Logging;
using TechagroSyncServices.Shared.Repositories;

namespace GranitSyncService
{
    public partial class GranitSyncService : ServiceBase
    {
        private readonly TimeSpan _interval;

        // Services
        private readonly SyncService _syncService;
        private readonly IProductSyncService _productSyncService;

        private Timer _timer;

        public GranitSyncService()
        {
            int logsExpirationDays = AppSettingsLoader.GetLogsExpirationDays();
            LogConfig.Configure(logsExpirationDays);

            _interval = AppSettingsLoader.GetFetchInterval();

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
                // 1. Get AppSettings
                var defaultMargin = AppSettingsLoader.GetDefaultMargin();
                var marginRanges = AppSettingsLoader.GetMarginRanges();

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
