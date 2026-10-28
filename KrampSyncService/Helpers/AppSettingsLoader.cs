using KrampSyncService.Constants;
using KrampSyncService.Settings;
using System;
using TechagroSyncServices.Shared.Helpers;

namespace KrampSyncService.Helpers
{
    public static class AppSettingsLoader
    {
        public static KrampApiSettings LoadApiSettings()
        {
            return new KrampApiSettings
            {
                BaseUrl = ConfigHelper.GetString("KrampApiBaseUrl"),
                Username = ConfigHelper.GetString("KrampApiUsername"),
                Password = ConfigHelper.GetString("KrampApiPassword"),
                CustomerNumber = ConfigHelper.GetString("KrampApiCustomerNumber"),
                DailyRequestsLimit = ConfigHelper.GetInt("KrampApiDailyRequestsLimit", 500),
                PricesBatchSize = Clamp(ConfigHelper.GetInt("KrampApiPricesBatchSize", ServiceConstants.MaxPricesBatchSize), ServiceConstants.MaxPricesBatchSize),
                AtpBatchSize = Clamp(ConfigHelper.GetInt("KrampApiAtpBatchSize", ServiceConstants.MaxAtpBatchSize), ServiceConstants.MaxAtpBatchSize),
                AtpRequestedQuantity = ConfigHelper.GetInt("KrampApiAtpRequestedQuantity", 20),
                PricesRefreshHours = ConfigHelper.GetInt("KrampApiPricesRefreshHours", 24),
                RequestInterval = ConfigHelper.GetInt("KrampApiRequestInterval", 1000)
            };
        }

        private static int Clamp(int value, int max) => Math.Max(1, Math.Min(value, max));
    }
}
