using AgrolandSyncService.Settings;
using System;
using System.Collections.Generic;
using System.Configuration;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.DTOs;

namespace AgrolandSyncService.Helpers
{
    public static class AppSettingsLoader
    {
        public static AgrolandApiSettings LoadApiSettings()
        {
            return new AgrolandApiSettings
            {
                BaseUrl = ConfigHelper.GetString("AgrolandApiBaseUrl"),
                ApiKey = ConfigHelper.GetString("AgrolandApiKey"),
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