using System.Text.Json.Serialization;

namespace HiFiSyncService.DTOs
{
    public class CustomerResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("legalEntity")]
        public string LegalEntity { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }
    }
}
