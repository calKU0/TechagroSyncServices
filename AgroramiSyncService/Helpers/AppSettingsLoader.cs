using AgroramiSyncService.Settings;
using TechagroSyncServices.Shared.Helpers;

namespace AgroramiSyncService.Helpers
{
    public static class AppSettingsLoader
    {
        public static AgroramiApiSettings LoadApiSettings()
        {
            return new AgroramiApiSettings
            {
                BaseUrl = ConfigHelper.GetString("AgroramiApiBaseUrl"),
                Login = ConfigHelper.GetString("AgroramiLogin"),
                Password = ConfigHelper.GetString("AgroramiPassword"),
            };
        }
    }
}