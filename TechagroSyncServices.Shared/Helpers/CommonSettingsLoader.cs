using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Settings;

namespace TechagroSyncServices.Shared.Helpers
{
    // Settings shared by all sync services
    public static class CommonSettingsLoader
    {
        // Not required - without e-mail settings the service works, only new products e-mails are not sent
        public static SmtpSettings LoadSmtpSettings()
        {
            return new SmtpSettings
            {
                Host = ConfigHelper.GetString("SmtpHost", false),
                Port = ConfigHelper.GetInt("SmtpPort", 465),
                User = ConfigHelper.GetString("SmtpUser", false),
                Password = ConfigHelper.GetString("SmtpPassword", false),
            };
        }

        public static int GetLogsExpirationDays() => ConfigHelper.GetInt("LogsExpirationDays", 14);
        public static int GetFilesExpirationDays() => ConfigHelper.GetInt("FilesExpirationDays", 31);

        public static decimal GetDefaultMargin() => ConfigHelper.GetDecimal("DefaultMargin", 25m);

        // Sale prices are rounded to this step, the same as rounding of price types set in WAPRO (0.01 = grosze, 0.10 = 10 groszy).
        // Both "0.10" and "0,10" are accepted, whatever the system language is.
        public static decimal GetSellPriceRoundingStep()
        {
            var raw = ConfigHelper.GetString("SellPriceRoundingStep", false);
            if (decimal.TryParse((raw ?? string.Empty).Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var step) && step > 0)
                return step;

            return 0.01m;
        }

        public static DeleteSafetySettings LoadDeleteSafetySettings()
        {
            return new DeleteSafetySettings
            {
                MaxMissingProductsPercent = ConfigHelper.GetDecimal("MaxMissingProductsPercent", 20m),
                MissingRunsBeforeDelete = Math.Max(1, ConfigHelper.GetInt("MissingRunsBeforeDelete", 2)),
                MissingProductsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "missing-products.json")
            };
        }

        // Empty when nobody should get e-mails
        public static string GetEmailsToNotify() => ConfigHelper.GetString("EmailsToNotify", false) ?? string.Empty;

        public static List<MarginRange> GetMarginRanges()
        {
            string raw = ConfigurationManager.AppSettings["MarginRanges"] ?? "";
            return MarginHelper.ParseMarginRanges(raw);
        }

        public static TimeSpan GetFetchInterval() => TimeSpan.FromMinutes(ConfigHelper.GetInt("FetchIntervalMinutes", 60));
    }
}
