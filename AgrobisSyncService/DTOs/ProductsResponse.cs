using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AgrobisSyncService.DTOs
{
    public class ProductsResponse
    {
        [JsonPropertyName("quantity")]
        public decimal Quantity { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("weight")]
        public decimal Weight { get; set; }

        [JsonPropertyName("saleUnit")]
        public string SaleUnit { get; set; }

        [JsonPropertyName("photos")]
        public List<string> Photos { get; set; }

        [JsonPropertyName("indexCatalog")]
        public string Index { get; set; }

        [JsonPropertyName("unit")]
        public string Unit { get; set; }

        [JsonPropertyName("ean")]
        public string Ean { get; set; }

        [JsonPropertyName("price")]
        public decimal PriceNet { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("idProduct")]
        public int Id { get; set; }

        [JsonPropertyName("field8")]
        public string DeliveryType { get; set; }

        [JsonPropertyName("categories")]
        public Categories Categories { get; set; }

        [JsonPropertyName("saleUnitConverter")]
        public int SaleUnitConverter { get; set; }

        [JsonPropertyName("field4")]
        public string Brand { get; set; }
    }
    public class Categories
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("child")]
        public Child Child { get; set; }
    }

    public class Child
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("child")]
        public Child ChildCategory { get; set; }
    }
}
