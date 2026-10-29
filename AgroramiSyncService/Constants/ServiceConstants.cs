using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.Helpers;

namespace AgroramiSyncService.Constants
{
    public static class ServiceConstants
    {
        public const IntegrationCompany Company = IntegrationCompany.AGRORAMI;

        // Catalogue codes in database: "<sku>AR"
        public static readonly ProductCodeSuffix CodeSuffix = new ProductCodeSuffix("AR");

        // Express shipping variant: "<sku>AR-EXP"
        public const string ExpressSuffix = "-EXP";

        // API
        public const int SkuBatchSize = 100;
        public const int PageSize = 750;
        public const int RequestDelayMs = 1200;

        // Paths
        public const string ImportCodesFilePath = "Import/numery_katalogowe.txt";
        public const string NewProductsCheckFilePath = "Data/new-products-check.json";
        public const string LegacySnapshotFilePath = "Export/products.json";
    }
}
