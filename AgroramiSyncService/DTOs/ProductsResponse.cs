using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AgroramiSyncService.DTOs
{
    public class ProductsResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("sku")]
        public string Sku { get; set; }

        [JsonPropertyName("ean")]
        public string Ean { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("catalog_number")]
        public string CatalogNumber { get; set; }

        [JsonPropertyName("description")]
        public HtmlContent Description { get; set; }

        [JsonPropertyName("short_description")]
        public HtmlContent ShortDescription { get; set; }

        [JsonPropertyName("manufacturer")]
        public int? Manufacturer { get; set; }

        [JsonIgnore]
        public string ManufacturerLabel { get; set; }

        [JsonPropertyName("weight")]
        public decimal? Weight { get; set; }

        [JsonPropertyName("stock_status")]
        public string StockStatus { get; set; }

        [JsonPropertyName("stock_availability")]
        public StockAvailability StockAvailability { get; set; }

        [JsonPropertyName("unit")]
        public int? Unit { get; set; }

        [JsonIgnore]
        public string UnitLabel { get; set; }

        [JsonPropertyName("price_range")]
        public PriceRange PriceRange { get; set; }

        [JsonPropertyName("media_gallery")]
        public List<MediaGalleryItem> MediaGallery { get; set; }
    }

    public class HtmlContent
    {
        [JsonPropertyName("html")]
        public string Html { get; set; }
    }

    public class StockAvailability
    {
        [JsonPropertyName("in_stock")]
        public int InStock { get; set; }

        [JsonPropertyName("in_stock_real")]
        public string InStockReal { get; set; }
    }

    public class PriceRange
    {
        [JsonPropertyName("minimum_price")]
        public MinimumPrice MinimumPrice { get; set; }
    }

    public class MinimumPrice
    {
        [JsonPropertyName("individual_price")]
        public IndividualPrice IndividualPrice { get; set; }
    }

    public class IndividualPrice
    {
        [JsonPropertyName("net")]
        public decimal Net { get; set; }

        [JsonPropertyName("gross")]
        public decimal Gross { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }
    }

    public class MediaGalleryItem
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; }

        [JsonPropertyName("position")]
        public int Position { get; set; }
    }
}