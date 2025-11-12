using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RolmarSyncService.DTOs
{
    public class StockResponse
    {
        [JsonPropertyName("result")]
        public List<StockItem> Result { get; set; }
    }

    public class StockItem
    {
        [JsonPropertyName("stock")]
        public int Stock { get; set; }

        [JsonPropertyName("unit")]
        public string Unit { get; set; }

        [JsonPropertyName("index")]
        public string Index { get; set; }
    }
}