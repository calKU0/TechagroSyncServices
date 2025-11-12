using System.Text.Json.Serialization;

namespace AgroramiSyncService.DTOs
{
    public class CustomerTokenResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }
}