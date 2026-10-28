using AgrolandSyncService.Helpers;
using AgrolandSyncService.Services;
using Serilog;
using System;
using System.Threading.Tasks;
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

        public AgrolandSyncService()
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
            // 1. Getting default info about products

            await _apiService.SyncProducts();
            Log.Information("Product sync completed.");
        }
    }
}