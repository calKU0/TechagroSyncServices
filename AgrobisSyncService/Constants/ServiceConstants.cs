using TechagroApiSync.Shared.Enums;

namespace AgrobisSyncService.Constants
{
    public class ServiceConstants
    {
        public const IntegrationCompany Company = IntegrationCompany.AGROBIS;

        // Paths
        public const string ExportFolder = "Export";
        public const string NewProductsFolder = "Nowe";
        public const string ImportCodesFilePath = "Import/numery_katalogowe.txt";
        public const string SnapshotFileName = "products.json";
        public const string NewProductsFileNameFormat = "nowe-produkty-{0}.csv";
    }
}
