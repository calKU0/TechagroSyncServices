using TechagroApiSync.Shared.Enums;

namespace RolnexSyncService.Constants
{
    public static class ServiceConstants
    {
        public const IntegrationCompany Company = IntegrationCompany.ROLNEX;

        // Paths
        public const string ExportFolder = "Export";
        public const string NewProductsFolder = "Nowe";
        public const string ImportCodesFilePath = "Import/numery-katalogowe.txt";
        public const string SnapshotFileName = "products.json";
        public const string NewProductsFileNameFormat = "nowe-produkty-{0}.csv"; // {0} = date
    }
}
