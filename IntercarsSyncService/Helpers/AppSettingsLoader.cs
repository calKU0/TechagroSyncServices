using IntercarsSyncService.Settings;
using System;
using System.Collections.Generic;
using System.Configuration;
using TechagroSyncServices.Shared.DTOs;

namespace IntercarsSyncService.Helpers
{
    public static class AppSettingsLoader
    {
        public static IntercarsApiSettings LoadApiSettings()
        {
            return new IntercarsApiSettings
            {
                BaseUrl = GetString("IntercarsApiBaseUrl"),
                Username = GetString("IntercarsApiUsername"),
                Password = GetString("IntercarsApiPassword"),
            };
        }

        public static int GetLogsExpirationDays() => GetInt("LogsExpirationDays", 14);

        public static decimal GetDefaultMargin() => GetDecimal("DefaultMargin", 25m);

        public static List<MarginRange> GetMarginRanges()
        {
            string raw = ConfigurationManager.AppSettings["MarginRanges"] ?? "";
            return ParseMarginRanges(raw);
        }

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

        private static decimal GetDecimal(string key, decimal defaultValue)
        {
            var raw = ConfigurationManager.AppSettings[key];
            if (decimal.TryParse(raw, out decimal result))
                return result;

            return defaultValue;
        }

        private static List<MarginRange> ParseMarginRanges(string raw)
        {
            var list = new List<MarginRange>();

            // Poprawka: użyj tablicy znaków zamiast pojedynczego chara
            var entries = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var entry in entries)
            {
                var parts = entry.Split(':');
                if (parts.Length != 2) continue;

                // Poprawka: użyj tablicy znaków zamiast pojedynczego chara
                var rangeParts = parts[0].Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                if (rangeParts.Length != 2) continue;

                if (decimal.TryParse(rangeParts[0], out decimal min) &&
                    decimal.TryParse(rangeParts[1], out decimal max) &&
                    decimal.TryParse(parts[1], out decimal margin))
                {
                    list.Add(new MarginRange { Min = min, Max = max, Margin = margin });
                }
            }

            return list;
        }
    }
}