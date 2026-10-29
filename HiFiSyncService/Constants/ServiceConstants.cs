using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.Helpers;

namespace HiFiSyncService.Constants
{
    public static class ServiceConstants
    {
        public const IntegrationCompany Company = IntegrationCompany.HIFI;

        // Catalogue codes in database: "<reference>HF"
        public static readonly ProductCodeSuffix CodeSuffix = new ProductCodeSuffix("HF");

        // API limits
        public const int MaxLiveBatchSize = 20;

        // Paths
        public const string ImportCodesFilePath = "Import/numery_katalogowe.txt";
    }
}
