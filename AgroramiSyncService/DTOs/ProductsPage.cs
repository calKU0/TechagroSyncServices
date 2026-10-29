using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AgroramiSyncService.DTOs
{
    // One page of products query - total_count tells when paging ends, because asking for a page after the last one is an API error
    public class ProductsPage
    {
        [JsonPropertyName("total_count")]
        public int TotalCount { get; set; }

        [JsonPropertyName("items")]
        public List<ProductsResponse> Items { get; set; } = new List<ProductsResponse>();
    }
}
