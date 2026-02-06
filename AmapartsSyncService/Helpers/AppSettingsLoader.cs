using AmapartsSyncService.Settings;
using System;
using System.Collections.Generic;
using System.Configuration;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Settings;

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

        public static string GetEmailsToNotify() => ConfigHelper.GetString("EmailsToNotify");

        public static int GetLogsExpirationDays() => ConfigHelper.GetInt("LogsExpirationDays", 14);
        public static int GetFilesExpirationDays() => ConfigHelper.GetInt("FilesExpirationDays", 31);

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