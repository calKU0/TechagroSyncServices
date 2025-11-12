using HermonSyncService.Settings;
using System;
using System.Collections.Generic;
using System.Configuration;
using TechagroSyncServices.Shared.DTOs;
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