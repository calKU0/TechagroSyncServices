using GaskaSyncService.Settings;
using System;
using System.Collections.Generic;
using System.Configuration;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;

namespace GaskaSyncService.Helpers
{
    public static class AppSettingsLoader
    {
        public static GaskaApiSettings LoadApiSettings()
        {
            return new GaskaApiSettings
            {
                BaseUrl = ConfigHelper.GetString("GaskaApiBaseUrl"),
                Acronym = ConfigHelper.GetString("GaskaApiAcronym"),
                Person = ConfigHelper.GetString("GaskaApiPerson"),
                Password = ConfigHelper.GetString("GaskaApiPassword"),
                ApiKey = ConfigHelper.GetString("GaskaApiKey"),
                ProductsPerPage = ConfigHelper.GetInt("GaskaApiProductsPerPage", 1000),
                ProductsInterval = ConfigHelper.GetInt("GaskaApiProductsInterval", 1),
                ProductPerDay = ConfigHelper.GetInt("GaskaApiProductPerDay", 500),
                ProductInterval = ConfigHelper.GetInt("GaskaApiProductInterval", 10)
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