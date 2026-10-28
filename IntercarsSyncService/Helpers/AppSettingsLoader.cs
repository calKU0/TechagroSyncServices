using IntercarsSyncService.Settings;
using TechagroSyncServices.Shared.Helpers;

namespace IntercarsSyncService.Helpers
{
    public static class AppSettingsLoader
    {
        public static IntercarsApiSettings LoadApiSettings()
        {
            return new IntercarsApiSettings
            {
                BaseUrl = ConfigHelper.GetString("IntercarsApiBaseUrl"),
                Username = ConfigHelper.GetString("IntercarsApiUsername"),
                Password = ConfigHelper.GetString("IntercarsApiPassword"),
            };
        }
    }
}