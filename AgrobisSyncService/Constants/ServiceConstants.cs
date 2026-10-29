using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.Helpers;

namespace AgrobisSyncService.Constants
{
    public class ServiceConstants
    {
        public const IntegrationCompany Company = IntegrationCompany.AGROBIS;

        // Catalogue codes in database: "AB<index>"
        public static readonly ProductCodePrefix CodePrefix = new ProductCodePrefix("AB");

        // Paths
        public const string ImportCodesFilePath = "Import/numery_katalogowe.txt";
    }
}
