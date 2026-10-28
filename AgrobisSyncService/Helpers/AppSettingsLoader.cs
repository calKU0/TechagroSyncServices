using AgrobisSyncService.Settings;
using TechagroSyncServices.Shared.Helpers;

namespace AgrobisSyncService.Helpers
{
    public static class AppSettingsLoader
    {
        public static AgrobisApiSettings LoadApiSettings()
        {
            return new AgrobisApiSettings
            {
                BaseUrl = ConfigHelper.GetString("AgrobisApiBaseUrl"),
                ApiKey = ConfigHelper.GetString("AgrobisApiKey"),
                ProductsPerPage = ConfigHelper.GetInt("AgrobisApiProductsPerPage", 1000),
            };
        }

        public static decimal GetSupplierDiscount() => ConfigHelper.GetDecimal("SupplierDiscount", 0m);
    }
}
