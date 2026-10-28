using AgrolandSyncService.Settings;
using TechagroSyncServices.Shared.Helpers;

namespace AgrolandSyncService.Helpers
{
    public static class AppSettingsLoader
    {
        public static AgrolandApiSettings LoadApiSettings()
        {
            return new AgrolandApiSettings
            {
                BaseUrl = ConfigHelper.GetString("AgrolandApiBaseUrl"),
                ApiKey = ConfigHelper.GetString("AgrolandApiKey"),
            };
        }
    }
}