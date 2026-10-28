using HermonSyncService.Settings;
using TechagroSyncServices.Shared.Helpers;

namespace HermonSyncService.Helpers
{
    public static class AppSettingsLoader
    {
        public static HermonFtpSettings LoadFtpSettings()
        {
            return new HermonFtpSettings
            {
                BaseUrl = ConfigHelper.GetString("HermonFtpBaseUrl"),
                Port = ConfigHelper.GetInt("HermonFtpPort", 21),
                Username = ConfigHelper.GetString("HermonFtpUsername"),
                Password = ConfigHelper.GetString("HermonFtpPassword"),
            };
        }

        public static HermonApiSettings LoadApiSettings()
        {
            return new HermonApiSettings
            {
                BaseUrl = ConfigHelper.GetString("HermonApiBaseUrl"),
                Username = ConfigHelper.GetString("HermonApiUsername"),
                Password = ConfigHelper.GetString("HermonApiPassword"),
            };
        }
    }
}