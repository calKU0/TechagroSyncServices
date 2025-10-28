using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RolmarSyncService.DTOs
{

        public class ProductsResponse
        {
            [JsonPropertyName("result")]
            public List<Product> Result { get; set; }
        }

        public class Product
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("ean")]
            public string Ean { get; set; }

            [JsonPropertyName("productIndex")]
            public string ProductIndex { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("description")]
            public string Description { get; set; }

            [JsonPropertyName("substitutes")]
            public string Substitutes { get; set; }

            [JsonPropertyName("fits")]
            public string Fits { get; set; }

            [JsonPropertyName("mainPhoto")]
            public string MainPhoto { get; set; }

            [JsonPropertyName("weight")]
            public string Weight { get; set; }

            [JsonPropertyName("unit")]
            public string Unit { get; set; }

            [JsonPropertyName("erp_package")]
            public string ErpPackage { get; set; }

            [JsonPropertyName("brand")]
            public string Brand { get; set; }

            [JsonPropertyName("cn")]
            public string Cn { get; set; }

            [JsonPropertyName("specifications")]
            [JsonConverter(typeof(FlexibleListConverter<Specification>))]
            public List<Specification> Specifications { get; set; }

            [JsonPropertyName("retailPrice")]
            public string RetailPrice { get; set; }

            [JsonPropertyName("price")]
            public string Price { get; set; }
        }

        public class Specification
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("value")]
            public string Value { get; set; }

            [JsonPropertyName("unit_name")]
            public string UnitName { get; set; }
        }
    
}
