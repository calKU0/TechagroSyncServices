using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechagroApiSync.Shared.Helpers
{
    public static class ConfigHelper
    {
        public static string GetConnenctionString() => ConfigurationManager.ConnectionStrings["DefaultConnectionString"].ToString();

        public static string GetString(string key, bool required = true)
        {
            var value = ConfigurationManager.AppSettings[key];

            if (required && string.IsNullOrWhiteSpace(value))
                throw new ConfigurationErrorsException($"Missing required appSetting: '{key}'");

            return value;
        }

        public static int GetInt(string key, int defaultValue)
        {
            var raw = ConfigurationManager.AppSettings[key];
            if (int.TryParse(raw, out int result))
                return result;

            return defaultValue;
        }

        public static decimal GetDecimal(string key, decimal defaultValue)
        {
            var raw = ConfigurationManager.AppSettings[key];
            if (decimal.TryParse(raw, out decimal result))
                return result;

            return defaultValue;
        }
    }
}