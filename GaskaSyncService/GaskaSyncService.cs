using GaskaSyncService.Constants;
using GaskaSyncService.Helpers;
using GaskaSyncService.Services;
using Serilog;
using System;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;
using TechagroSyncServices.Shared.Services;

namespace GaskaSyncService
{
    public partial class GaskaApiService : SyncServiceBase
    {
        // Repo
        private readonly IProductRepository _productRepository;

        // Services
        private readonly ApiService _apiService;
        private readonly IProductSyncService _productSyncService;

        private DateTime _lastProductDetailsSyncDate = DateTime.MinValue;
        private DateTime _lastRunTime;

        public GaskaApiService()
        {
            // Repositories
            string connectionString = ConfigHelper.GetConnenctionString();
            _productRepository = new ProductRepository(connectionString);

            // Services
            _productSyncService = new ProductSyncService(_productRepository);
            _apiService = new ApiService(_productRepository, _productSyncService);

            InitializeComponent();
        }

        protected override async Task RunSyncAsync()
        {
            _lastRunTime = DateTime.Now;

            // 1. Getting default info about products
            var fetchResult = await _apiService.SyncProducts();
            Log.Information("Basic product sync completed.");

            // Delete products no longer returned by API (all API products are synced, so there is no import list)
            await _productSyncService.DeleteNotSyncedProducts(ServiceConstants.Company, null, fetchResult.Items, fetchResult.IsComplete);

            // 2.Getting detailed info about products that are not in db yet
            if (_lastProductDetailsSyncDate.Date < DateTime.Today && DateTime.Now.Hour >= 3 && DateTime.Now.Hour <= 8)
            {
                await _apiService.SyncProductDetails();
                _lastProductDetailsSyncDate = DateTime.Today;

                Log.Information("Detailed product sync completed.");
            }
        }
    }
}

//1230