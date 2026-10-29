using System.Text.Json.Serialization;

namespace HiFiSyncService.DTOs
{
    public class PriceRequest
    {
        [JsonPropertyName("reference")]
        public string Reference { get; set; }

        [JsonPropertyName("quantity")]
        public decimal Quantity { get; set; }
    }
}
