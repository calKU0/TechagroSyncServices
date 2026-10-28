using TechagroApiSync.Shared.Enums;

namespace KrampSyncService.Constants
{
    public static class ServiceConstants
    {
        public const IntegrationCompany Company = IntegrationCompany.KRAMP;

        // API limits
        public const int MaxPricesBatchSize = 25;
        public const int MaxAtpBatchSize = 20;

        // Paths
        public const string ImportCodesFilePath = "Import/numery_katalogowe.txt";
        public const string RequestsStateFilePath = "Data/requests-state.json";
        public const string ProductsStateFilePath = "Data/products-state.json";
    }
}
