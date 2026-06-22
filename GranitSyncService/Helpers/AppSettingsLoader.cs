using GranitSyncService.Settings;
using System;
using System.Collections.Generic;
using System.Configuration;
using TechagroSyncServices.Shared.DTOs;
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