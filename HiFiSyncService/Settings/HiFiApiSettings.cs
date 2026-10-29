namespace HiFiSyncService.Settings
{
    public class HiFiApiSettings
    {
        public string BaseUrl { get; set; }
        public string ApiKey { get; set; }
        public string ApiSecret { get; set; }

        // Customer account is taken from GET /customer, legal entity picks the right one
        public string LegalEntity { get; set; }
        public string Locale { get; set; }

        // Rate limits apply per user (default 2 RPS / 30 RPM), requests are spaced to stay under RPM
        public int RequestsPerMinute { get; set; }
        public int MaxRetries { get; set; }
    }
}
