using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AgroramiSyncService.DTOs
{
    public class AttributeMetadataResponse
    {
        [JsonPropertyName("attribute_code")]
        public string AttributeCode { get; set; } = string.Empty;

        [JsonPropertyName("attribute_type")]
        public string AttributeType { get; set; }

        [JsonPropertyName("entity_type")]
        public string EntityType { get; set; }

        [JsonPropertyName("input_type")]
        public string InputType { get; set; }

        [JsonPropertyName("attribute_options")]
        public List<AttributeOption> AttributeOptions { get; set; } = new List<AttributeOption>();

        public class AttributeOption
        {
            [JsonPropertyName("label")]
            public string Label { get; set; } = string.Empty;

            [JsonPropertyName("value")]
            public string Value { get; set; } = string.Empty;
        }
    }
}