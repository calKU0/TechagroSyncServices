using System.Text.Json.Serialization;

namespace HiFiSyncService.DTOs
{
    public class AvailabilityResponse
    {
        [JsonPropertyName("reference")]
        public string Reference { get; set; }

        [JsonPropertyName("available")]
        public bool Available { get; set; }

        // Stock in the HIFI FILTER warehouse serving our legal entity (HFPL), only with the required permission
        [JsonPropertyName("stockQuantity")]
        public decimal? StockQuantity { get; set; }

        // Central warehouse, products delivered after LeadTimeDays - not counted to our stock
        [JsonPropertyName("supplyHub")]
        public SupplyHubAvailability SupplyHub { get; set; }
    }

    public class SupplyHubAvailability
    {
        [JsonPropertyName("available")]
        public bool Available { get; set; }

        [JsonPropertyName("leadTimeDays")]
        public string LeadTimeDays { get; set; }

        [JsonPropertyName("stockQuantity")]
        public decimal? StockQuantity { get; set; }
    }
}
