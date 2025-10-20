using IntercarsSyncService.Settings;
using System;
using System.Collections.Generic;
using System.Configuration;
using TechagroApiSync.Shared.Helpers;
using TechagroSyncServices.Shared.DTOs;

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

        public static int GetLogsExpirationDays() => ConfigHelper.GetInt("LogsExpirationDays", 14);

        public static decimal GetDefaultMargin() => ConfigHelper.GetDecimal("DefaultMargin", 25m);

        public static List<MarginRange> GetMarginRanges()
        {
            string raw = ConfigurationManager.AppSettings["MarginRanges"] ?? "";
            return MarginHelper.ParseMarginRanges(raw);
        }

        public static TimeSpan GetFetchInterval() => TimeSpan.FromMinutes(ConfigHelper.GetInt("FetchIntervalMinutes", 60));
    }
}