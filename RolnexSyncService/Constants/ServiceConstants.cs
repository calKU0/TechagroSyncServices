using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.Helpers;

namespace RolnexSyncService.Constants
{
    public static class ServiceConstants
    {
        public const IntegrationCompany Company = IntegrationCompany.ROLNEX;

        // Catalogue codes in database: "RL<id>"
        public static readonly ProductCodePrefix CodePrefix = new ProductCodePrefix("RL");

        // Paths
        public const string ImportCodesFilePath = "Import/numery_katalogowe.txt";
    }
}
