using GranitSyncService.Settings;
using TechagroSyncServices.Shared.Helpers;

namespace GranitSyncService.Helpers
{
    public static class AppSettingsLoader
    {
        public static GranitApiSettings LoadApiSettings()
        {
            return new GranitApiSettings
            {
                BaseUrl = ConfigHelper.GetString("GranitApiBaseUrl"),
                Username = ConfigHelper.GetString("GranitApiUsername"),
                Password = ConfigHelper.GetString("GranitApiPassword"),
                RequestInterval = ConfigHelper.GetInt("GranitApiRequestInterval", 1000),
                MaxProductsBatch = ConfigHelper.GetInt("GranitApiMaxProductsBatch", 100)
            };
        }
    }
}