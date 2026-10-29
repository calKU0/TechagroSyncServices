using System.Text.Json.Serialization;

namespace HiFiSyncService.DTOs
{
    public class PriceResponse
    {
        [JsonPropertyName("reference")]
        public string Reference { get; set; }

        [JsonPropertyName("quantity")]
        public decimal Quantity { get; set; }

        // Net customer price for a single piece
        [JsonPropertyName("unitPrice")]
        public decimal UnitPrice { get; set; }

        [JsonPropertyName("recommendedRetailUnitPrice")]
        public decimal? RecommendedRetailUnitPrice { get; set; }

        [JsonPropertyName("totalPrice")]
        public decimal TotalPrice { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }
    }
}
