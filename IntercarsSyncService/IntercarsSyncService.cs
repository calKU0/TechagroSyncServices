using IntercarsSyncService.Helpers;
using IntercarsSyncService.Services;
using Serilog;
using System;
using System.Runtime;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;
using TechagroSyncServices.Shared.Services;

namespace IntercarsSyncService
{
    public partial class IntercarsSyncService : SyncServiceBase
    {
        // Repo
        private readonly IProductRepository _productRepository;

        // Services
        private readonly FileService _apiService;

        private readonly IProductSyncService _productSyncService;
        private readonly IEmailService _emailService;

        public IntercarsSyncService()
        {
            var apiSettings = AppSettingsLoader.LoadApiSettings();

            // Repositories
            string connectionString = ConfigHelper.GetConnenctionString();
            _productRepository = new ProductRepository(connectionString);

            // Services
            var imageClient = HttpClientHelper.CreateImageClient();
            var productsClient = HttpClientHelper.CreateAuthorizedClient(apiSettings.Username, apiSettings.Password);
            var smtpSettings = CommonSettingsLoader.LoadSmtpSettings();
            _productSyncService = new ProductSyncService(_productRepository, imageClient);
            _emailService = new EmailService(smtpSettings);
            _apiService = new FileService(_productSyncService, _emailService, productsClient);

            InitializeComponent();
        }

        protected override async Task RunSyncAsync()
        {
            await _apiService.SyncProducts();
        }

        protected override void OnSyncFinished()
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}