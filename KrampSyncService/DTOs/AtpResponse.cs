using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KrampSyncService.DTOs
{
    public class AtpResponse
    {
        [JsonPropertyName("errors")]
        public List<AtpError> Errors { get; set; } = new List<AtpError>();

        [JsonPropertyName("atp_data")]
        public List<AtpItem> AtpData { get; set; } = new List<AtpItem>();
    }

    public class AtpItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("requested_quantity")]
        public decimal RequestedQuantity { get; set; }

        [JsonPropertyName("available_quantity")]
        public decimal AvailableQuantity { get; set; }
    }

    public class AtpError
    {
        [JsonPropertyName("property")]
        public string Property { get; set; }

        [JsonPropertyName("error_message")]
        public string ErrorMessage { get; set; }
    }
}
