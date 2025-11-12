using HermonSyncService.DTOs;
using HermonSyncService.Helpers;
using HermonSyncService.Services;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Logging;
using TechagroSyncServices.Shared.Repositories;

namespace HermonSyncService
{
    public partial class HermonSyncService : ServiceBase
    {
        private readonly TimeSpan _interval;

        // Repo
        private readonly IProductRepository _productRepository;

        // Services
        private readonly ProductService _productService;

        private readonly ProductSyncService _productSyncService;

        private Timer _timer;
        private DateTime _lastProductDetailsSyncDate = DateTime.MinValue;

        public HermonSyncService()
        {
            // Serilog configuration and initialization
            int logsExpirationDays = AppSettingsLoader.GetLogsExpirationDays();
            LogConfig.Configure(logsExpirationDays);

            _interval = AppSettingsLoader.GetFetchInterval();

            // Repositories
            string connectionString = ConfigHelper.GetConnenctionString();
            _productRepository = new ProductRepository(connectionString);

            // Services
            _productService = new ProductService(_productRepository);
            _productSyncService = new ProductSyncService(_productRepository);

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
                if (_lastProductDetailsSyncDate.Date < DateTime.Today)
                {
                    images = _productService.SyncImagesFromFtp();
                }

                // 3. Getting detailed info about products from API
                var detailedProductData = await _productService.FetchProductDetailsFromApi(basicProductData);

                // 4. Building full product data
                var fullProductData = _productService.BuildFullProducts(basicProductData, detailedProductData, images);

                // 5. Updating database with synchronized data
                await _productSyncService.SyncToDatabaseAsync(fullProductData);
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