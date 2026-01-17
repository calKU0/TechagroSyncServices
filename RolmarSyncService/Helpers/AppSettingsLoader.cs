using RolmarSyncService.Settings;
using System;
using System.Collections.Generic;
using System.Configuration;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Settings;

namespace RolmarSyncService.Helpers
{
    public static class AppSettingsLoader
    {
        public static RolmarApiSettings LoadApiSettings()
        {
            return new RolmarApiSettings
            {
                BaseUrl = ConfigHelper.GetString("RolmarApiBaseUrl"),
                ApiKey = ConfigHelper.GetString("RolmarApiKey"),
            };
        }

        public static SmtpSettings LoadSmtpSettings()
        {
            return new SmtpSettings
            {
                Host = ConfigHelper.GetString("SmtpHost"),
                Port = ConfigHelper.GetInt("SmtpPort", 465),
                User = ConfigHelper.GetString("SmtpUser"),
                Password = ConfigHelper.GetString("SmtpPassword"),
            };
        }

        public static int GetLogsExpirationDays() => ConfigHelper.GetInt("LogsExpirationDays", 14);

        public static decimal GetDefaultMargin() => ConfigHelper.GetDecimal("DefaultMargin", 25m);

        public static string GetEmailsToNotify() => ConfigHelper.GetString("EmailsToNotify");

        public static List<MarginRange> GetMarginRanges()
        {
            string raw = ConfigurationManager.AppSettings["MarginRanges"] ?? "";
            return MarginHelper.ParseMarginRanges(raw);
        }

        public static TimeSpan GetFetchInterval() => TimeSpan.FromMinutes(ConfigHelper.GetInt("FetchIntervalMinutes", 60));
    }
}