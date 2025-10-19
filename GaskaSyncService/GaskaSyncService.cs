using GaskaSyncService.Helpers;
using GaskaSyncService.Services;
using Serilog;
using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using TechagroSyncServices.Shared.Logging;
using TechagroSyncServices.Shared.Repositories;

namespace GaskaSyncService
{
    public partial class GaskaApiService : ServiceBase
    {
        // Settings
        private readonly TimeSpan _interval;

        // Repo
        private readonly IProductRepository _productRepository;

        // Services
        private readonly ApiService _apiService;

        private Timer _timer;
        private DateTime _lastProductDetailsSyncDate = DateTime.MinValue;
        private DateTime _lastRunTime;

        public GaskaApiService()
        {
            // Serilog configuration and initialization
            int logsExpirationDays = AppSettingsLoader.GetLogsExpirationDays();
            LogConfig.Configure(logsExpirationDays);

            _interval = AppSettingsLoader.GetFetchInterval();

            // Repositories
            string connectionString = AppSettingsLoader.GetConnenctionString();
            _productRepository = new ProductRepository(connectionString);

            // Services
            _apiService = new ApiService(_productRepository);

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
                _lastRunTime = DateTime.Now;

                // 1. Getting default info about products
                await _apiService.SyncProducts();
                Log.Information("Basic product sync completed.");

                // 2.Getting detailed info about products that are not in db yet
                if (_lastProductDetailsSyncDate.Date < DateTime.Today && DateTime.Now.Hour >= 3 && DateTime.Now.Hour <= 8)
                {
                    await _apiService.SyncProductDetails();
                    _lastProductDetailsSyncDate = DateTime.Today;

                    Log.Information("Detailed product sync completed.");
                }
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

//1230