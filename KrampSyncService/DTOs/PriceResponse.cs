using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace KrampSyncService.DTOs
{
    public class PriceResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("requested_quantity")]
        public decimal RequestedQuantity { get; set; }

        [JsonPropertyName("prices")]
        public List<PriceItem> Prices { get; set; } = new List<PriceItem>();

        // "gross" is the Kramp list price, "net" is the customer price
        [JsonIgnore]
        public decimal? NetPrice => GetUnitPrice("net");

        [JsonIgnore]
        public decimal? ListPrice => GetUnitPrice("gross");

        private decimal? GetUnitPrice(string priceType)
        {
            var price = Prices?.FirstOrDefault(p => string.Equals(p.PriceType, priceType, StringComparison.OrdinalIgnoreCase));
            if (price == null)
                return null;

            // Value is given for price_quantity pieces
            return price.PriceQuantity > 0 ? price.Value / price.PriceQuantity : price.Value;
        }
    }

    public class PriceItem
    {
        [JsonPropertyName("price_type")]
        public string PriceType { get; set; }

        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("price_quantity")]
        public decimal PriceQuantity { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }
    }
}
