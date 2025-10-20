using AmapartsSyncService.Settings;
using System;
using System.Collections.Generic;
using System.Configuration;
using TechagroApiSync.Shared.Helpers;
using TechagroSyncServices.Shared.DTOs;

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

        public static int GetLogsExpirationDays() => ConfigHelper.GetInt("LogsExpirationDays", 14);

        public static decimal GetDefaultMargin() => ConfigHelper.GetDecimal("DefaultMargin", 25m);

        public static List<MarginRange> GetMarginRanges()
        {
            string raw = ConfigurationManager.AppSettings["MarginRanges"] ?? "";
            return MarginHelper.ParseMarginRanges(raw);
        }

        public static TimeSpan GetFetchInterval() => TimeSpan.FromMinutes(ConfigHelper.GetInt("FetchIntervalMinutes", 60));

        public static string GetConnenctionString() => ConfigurationManager.ConnectionStrings["DefaultConnectionString"].ToString();
    }
}