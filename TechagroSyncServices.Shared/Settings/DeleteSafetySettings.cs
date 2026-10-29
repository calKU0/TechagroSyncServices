namespace TechagroSyncServices.Shared.Settings
{
    // Protects products from being deleted because of API errors
    public class DeleteSafetySettings
    {
        // Above this share of missing products the API response is treated as broken and nothing is deleted
        public decimal MaxMissingProductsPercent { get; set; }

        // How many complete fetches in a row a product has to be missing in API before it is deleted
        public int MissingRunsBeforeDelete { get; set; }

        // File with counters of consecutive misses
        public string MissingProductsFilePath { get; set; }
    }
}
