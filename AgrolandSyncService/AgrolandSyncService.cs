using AgrolandSyncService.Helpers;
using AgrolandSyncService.Services;
using Serilog;
using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Logging;
using TechagroSyncServices.Shared.Repositories;

namespace AgrolandSyncService
{
    public partial class AgrolandSyncService : ServiceBase
    {
        // Settings
        private readonly TimeSpan _interval;

        // Repo
        private readonly IProductRepository _productRepository;

        // Services
        private readonly ApiService _apiService;

        private Timer _timer;

        public AgrolandSyncService()
        {
            // Serilog configuration and initialization
            int logsExpirationDays = AppSettingsLoader.GetLogsExpirationDays();
            LogConfig.Configure(logsExpirationDays);

            _interval = AppSettingsLoader.GetFetchInterval();

            // Repositories
            string connectionString = ConfigHelper.GetConnenctionString();
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
                // 1. Getting default info about products

                await _apiService.SyncProducts();
                Log.Information("Product sync completed.");
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