using AgroramiSyncService.Settings;
using System;
using System.Collections.Generic;
using System.Configuration;
using TechagroSyncServices.Shared.DTOs;
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