using TechagroApiSync.Shared.Enums;

namespace AmapartsSyncService.Constants
{
    public static class ServiceConstants
    {
        public const IntegrationCompany Company = IntegrationCompany.AMA;

        // Paths
        public const string ExportFolder = "Export";
        public const string NewProductsFolder = "Nowe";
        public const string ImportCodesFilePath = "Import/numery_katalogowe.txt";
        public const string SnapshotFileName = "products.json";
        public const string NewProductsFileNameFormat = "nowe-produkty-{0}.csv"; // {0} = date
    }
}
