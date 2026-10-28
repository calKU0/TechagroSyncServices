using System;
using System.Collections.Generic;
using System.Configuration;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Settings;

namespace TechagroSyncServices.Shared.Helpers
{
    // Settings shared by all sync services
    public static class CommonSettingsLoader
    {
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
        public static int GetFilesExpirationDays() => ConfigHelper.GetInt("FilesExpirationDays", 31);

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
