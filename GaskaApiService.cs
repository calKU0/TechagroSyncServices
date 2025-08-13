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
using TeachagroApiSync.DTOs;
using TeachagroApiSync.Helpers;
using TeachagroApiSync.Logging;
using TechagroApiSync.Services;

namespace TeachagroApiSync
{
    public partial class GaskaApiService : ServiceBase
    {
        // Settings
        private readonly GaskaApiSettings _apiSettings;

        private readonly TimeSpan _interval;
        private readonly int _logsExpirationDays;
        private readonly string _connectionString;

        // Services
        private readonly ApiService _apiService;

        private Timer _timer;
        private DateTime _lastProductDetailsSyncDate = DateTime.MinValue;
        private DateTime _lastRunTime;

        public GaskaApiService()
        {
            // App Settings initialization
            _apiSettings = AppSettingsLoader.LoadApiSettings();
            _interval = AppSettingsLoader.GetFetchInterval();
            _logsExpirationDays = AppSettingsLoader.GetLogsExpirationDays();
            _connectionString = AppSettingsLoader.GetConnenctionString();

            // Services initialization
            _apiService = new ApiService(_apiSettings, _connectionString);

            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // Serilog configuration and initialization
            LogConfig.Configure(_logsExpirationDays);

            _timer = new Timer(
                async _ => await TimerTickAsync(),
                null,
                TimeSpan.Zero,
                _interval
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
                //if (_lastProductDetailsSyncDate.Date < DateTime.Today)
                //{
                //    await _apiService.SyncProductDetails();
                //    _lastProductDetailsSyncDate = DateTime.Today;

                //    Log.Information("Detailed product sync completed.");
                //}

                DateTime nextRun = _lastRunTime.Add(_interval);
                Log.Information("All processes completed. Next run scheduled at: {NextRun}", nextRun);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during API synchronization.");
            }
        }
    }
}