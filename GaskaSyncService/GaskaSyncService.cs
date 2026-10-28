using GaskaSyncService.Helpers;
using GaskaSyncService.Services;
using Serilog;
using System;
using System.Threading.Tasks;
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

        private DateTime _lastProductDetailsSyncDate = DateTime.MinValue;
        private DateTime _lastRunTime;

        public GaskaApiService()
        {
            // Repositories
            string connectionString = ConfigHelper.GetConnenctionString();
            _productRepository = new ProductRepository(connectionString);

            // Services
            _apiService = new ApiService(_productRepository);

            InitializeComponent();
        }

        protected override async Task RunSyncAsync()
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
    }
}

//1230