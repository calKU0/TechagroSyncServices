using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.Helpers;

namespace HermonSyncService.Constants
{
    public static class ServiceConstants
    {
        public const IntegrationCompany Company = IntegrationCompany.HERMON;

        // Catalogue codes in database: "<code>HR"
        public static readonly ProductCodeSuffix CodeSuffix = new ProductCodeSuffix("HR");

        // Paths
        public const string ImportCodesFilePath = "Import/numery_katalogowe.txt";
    }
}
