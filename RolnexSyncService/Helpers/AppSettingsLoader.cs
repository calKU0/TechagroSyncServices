using RolnexSyncService.Settings;
using TechagroSyncServices.Shared.Helpers;

namespace RolnexSyncService.Helpers
{
    public static class AppSettingsLoader
    {
        public static RolnexApiSettings LoadApiSettings()
        {
            return new RolnexApiSettings
            {
                BaseUrl = ConfigHelper.GetString("RolnexApiBaseUrl"),
                ApiKey = ConfigHelper.GetString("RolnexApiKey"),
            };
        }
    }
}