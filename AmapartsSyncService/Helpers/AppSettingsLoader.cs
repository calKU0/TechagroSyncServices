using AmapartsSyncService.Settings;
using System.Configuration;
using TechagroSyncServices.Shared.Helpers;

namespace AmapartsSyncService.Helpers
{
    public static class AppSettingsLoader
    {
        public static AmapartsApiSettings LoadApiSettings()
        {
            return new AmapartsApiSettings
            {
                BaseUrl = ConfigHelper.GetString("ApiBaseUrl"),
                ApiKey = ConfigHelper.GetString("ApiKey"),
            };
        }

        public static string GetConnenctionString() => ConfigurationManager.ConnectionStrings["DefaultConnectionString"].ToString();
    }
}