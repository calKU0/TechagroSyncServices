using System;
using System.Text.Json.Serialization;

namespace KrampSyncService.DTOs
{
    // Last known Kramp data for a single SKU, persisted between runs
    public class ProductState
    {
        public string Sku { get; set; }
        public decimal? NetPrice { get; set; }
        public decimal? ListPrice { get; set; }
        public decimal? Stock { get; set; }
        public DateTime? PriceUpdatedAt { get; set; }
        public DateTime? StockUpdatedAt { get; set; }

        [JsonIgnore]
        public bool IsComplete => NetPrice.HasValue && Stock.HasValue;
    }
}
