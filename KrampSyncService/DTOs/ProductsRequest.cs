using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KrampSyncService.DTOs
{
    // Request body shared by prices and ATP endpoints
    public class ProductsRequest
    {
        [JsonPropertyName("customer_number")]
        public string CustomerNumber { get; set; }

        [JsonPropertyName("products")]
        public List<ProductRequestItem> Products { get; set; } = new List<ProductRequestItem>();
    }

    public class ProductRequestItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("quantity")]
        public string Quantity { get; set; }
    }
}
