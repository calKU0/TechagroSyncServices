namespace KrampSyncService.Settings
{
    public class KrampApiSettings
    {
        public string BaseUrl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string CustomerNumber { get; set; }

        // Daily limit shared by prices and ATP, auth is not counted
        public int DailyRequestsLimit { get; set; }

        public int PricesBatchSize { get; set; }
        public int AtpBatchSize { get; set; }
        public int AtpRequestedQuantity { get; set; }
        public int PricesRefreshHours { get; set; }
        public int RequestInterval { get; set; }
    }
}
