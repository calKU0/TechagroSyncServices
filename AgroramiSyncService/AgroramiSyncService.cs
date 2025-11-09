using AgroramiSyncService.Helpers;
using AgroramiSyncService.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Logging;
using TechagroSyncServices.Shared.Repositories;

namespace AgroramiSyncService
{
    public partial class AgroramiSyncService : ServiceBase
    {
        private readonly TimeSpan _interval;

        // Repo
        private readonly IProductRepository _productRepository;

        // Services
        private readonly ApiService _apiService;

        private Timer _timer;
        private DateTime _lastProductDetailsSyncDate = DateTime.MinValue;

        public AgroramiSyncService()
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
                if (_lastProductDetailsSyncDate.Date < DateTime.Today)
                {
                    await _apiService.SyncProducts();
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