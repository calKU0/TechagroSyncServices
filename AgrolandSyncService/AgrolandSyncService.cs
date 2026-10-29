using AgrolandSyncService.Constants;
using AgrolandSyncService.Helpers;
using AgrolandSyncService.Services;
using Serilog;
using System;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;
using TechagroSyncServices.Shared.Services;

namespace AgrolandSyncService
{
    public partial class AgrolandSyncService : SyncServiceBase
    {
        // Repo
        private readonly IProductRepository _productRepository;

        // Services
        private readonly ApiService _apiService;
        private readonly IProductSyncService _productSyncService;

        public AgrolandSyncService()
        {
            // Repositories
            string connectionString = ConfigHelper.GetConnenctionString();
            _productRepository = new ProductRepository(connectionString);

            // Services
            _productSyncService = new ProductSyncService(_productRepository);
            _apiService = new ApiService(_productSyncService);

            InitializeComponent();
        }

        protected override async Task RunSyncAsync()
        {
            // 1. Getting default info about products

            var fetchResult = await _apiService.SyncProducts();
            Log.Information("Product sync completed.");

            // 2. Delete products no longer returned by API (all API products are synced, so there is no import list)
            await _productSyncService.DeleteNotSyncedProducts(ServiceConstants.Company, null, fetchResult.Items, fetchResult.IsComplete);
        }
    }
}