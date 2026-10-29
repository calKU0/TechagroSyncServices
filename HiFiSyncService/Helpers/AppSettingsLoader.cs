using HiFiSyncService.Settings;
using TechagroSyncServices.Shared.Helpers;

namespace HiFiSyncService.Helpers
{
    public static class AppSettingsLoader
    {
        public static HiFiApiSettings LoadApiSettings()
        {
            return new HiFiApiSettings
            {
                BaseUrl = ConfigHelper.GetString("HiFiApiBaseUrl"),
                ApiKey = ConfigHelper.GetString("HiFiApiKey"),
                ApiSecret = ConfigHelper.GetString("HiFiApiSecret"),
                LegalEntity = GetStringOrDefault("HiFiApiLegalEntity", "HFPL"),
                Locale = GetStringOrDefault("HiFiApiLocale", "pl-PL"),
                RequestsPerMinute = ConfigHelper.GetInt("HiFiApiRequestsPerMinute", 30),
                MaxRetries = ConfigHelper.GetInt("HiFiApiMaxRetries", 3)
            };
        }

        // Empty value in App.config falls back to default too
        private static string GetStringOrDefault(string key, string defaultValue)
        {
            var value = ConfigHelper.GetString(key, false);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
        }
    }
}
