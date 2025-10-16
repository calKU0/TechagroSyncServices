using System;
using System.Configuration;
using GaskaSyncService.Settings;

namespace GaskaSyncService.Helpers
{
    public static class AppSettingsLoader
    {
        public static GaskaApiSettings LoadApiSettings()
        {
            return new GaskaApiSettings
            {
                BaseUrl = GetString("GaskaApiBaseUrl"),
                Acronym = GetString("GaskaApiAcronym"),
                Person = GetString("GaskaApiPerson"),
                Password = GetString("GaskaApiPassword"),
                ApiKey = GetString("GaskaApiKey"),
                ProductsPerPage = GetInt("GaskaApiProductsPerPage", 1000),
                ProductsInterval = GetInt("GaskaApiProductsInterval", 1),
                ProductPerDay = GetInt("GaskaApiProductPerDay", 500),
                ProductInterval = GetInt("GaskaApiProductInterval", 10)
            };
        }

        public static int GetLogsExpirationDays() => GetInt("LogsExpirationDays", 14);

        public static int GetMargin() => GetInt("Margin%", 25);

        public static TimeSpan GetFetchInterval() => TimeSpan.FromMinutes(GetInt("FetchIntervalMinutes", 60));

        public static string GetConnenctionString() => ConfigurationManager.ConnectionStrings["DefaultConnectionString"].ToString();

        private static string GetString(string key, bool required = true)
        {
            var value = ConfigurationManager.AppSettings[key];

            if (required && string.IsNullOrWhiteSpace(value))
                throw new ConfigurationErrorsException($"Missing required appSetting: '{key}'");

            return value;
        }

        private static int GetInt(string key, int defaultValue)
        {
            var raw = ConfigurationManager.AppSettings[key];
            if (int.TryParse(raw, out int result))
                return result;

            return defaultValue;
        }
    }
}