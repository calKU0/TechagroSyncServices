namespace GranitSyncService.Settings
{
    public class GranitApiSettings
    {
        public string BaseUrl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int RequestInterval { get; set; }
        public int MaxProductsBatch { get; set; }
    }
}
