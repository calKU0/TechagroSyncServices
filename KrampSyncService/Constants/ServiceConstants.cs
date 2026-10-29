using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.Helpers;

namespace KrampSyncService.Constants
{
    public static class ServiceConstants
    {
        public const IntegrationCompany Company = IntegrationCompany.KRAMP;

        // Catalogue codes in database: "<sku>KR"
        public static readonly ProductCodeSuffix CodeSuffix = new ProductCodeSuffix("KR");

        // API limits
        public const int MaxPricesBatchSize = 25;
        public const int MaxAtpBatchSize = 20;

        // Paths
        public const string ImportCodesFilePath = "Import/numery_katalogowe.txt";
        public const string RequestsStateFilePath = "Data/requests-state.json";
        public const string ProductsStateFilePath = "Data/products-state.json";
    }
}
