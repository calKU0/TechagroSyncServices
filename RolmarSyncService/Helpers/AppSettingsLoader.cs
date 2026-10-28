using RolmarSyncService.Settings;
using TechagroSyncServices.Shared.Helpers;

namespace RolmarSyncService.Helpers
{
    public static class AppSettingsLoader
    {
        public static RolmarApiSettings LoadApiSettings()
        {
            return new RolmarApiSettings
            {
                BaseUrl = ConfigHelper.GetString("RolmarApiBaseUrl"),
                ApiKey = ConfigHelper.GetString("RolmarApiKey"),
            };
        }
    }
}