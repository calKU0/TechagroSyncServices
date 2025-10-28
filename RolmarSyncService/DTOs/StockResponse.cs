using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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
