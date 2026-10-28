using GaskaSyncService.Settings;
using TechagroSyncServices.Shared.Helpers;

namespace GaskaSyncService.Helpers
{
    public static class AppSettingsLoader
    {
        public static GaskaApiSettings LoadApiSettings()
        {
            return new GaskaApiSettings
            {
                BaseUrl = ConfigHelper.GetString("GaskaApiBaseUrl"),
                Acronym = ConfigHelper.GetString("GaskaApiAcronym"),
                Person = ConfigHelper.GetString("GaskaApiPerson"),
                Password = ConfigHelper.GetString("GaskaApiPassword"),
                ApiKey = ConfigHelper.GetString("GaskaApiKey"),
                ProductsPerPage = ConfigHelper.GetInt("GaskaApiProductsPerPage", 1000),
                ProductsInterval = ConfigHelper.GetInt("GaskaApiProductsInterval", 1),
                ProductPerDay = ConfigHelper.GetInt("GaskaApiProductPerDay", 500),
                ProductInterval = ConfigHelper.GetInt("GaskaApiProductInterval", 10)
            };
        }
    }
}