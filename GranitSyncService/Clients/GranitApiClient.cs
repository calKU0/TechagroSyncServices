using GranitSyncService.DTOs;
using GranitSyncService.Settings;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace GranitSyncService.Clients
{
    public class GranitApiClient
    {
        private readonly GranitApiSettings _settings;
        private readonly HttpClient _httpClient;
        public GranitApiClient(GranitApiSettings settings)
        {
            _settings = settings;
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new System.Uri(_settings.BaseUrl);
        }

        public async Task<GranitAvailabilityResponse> GetStockAndPricesAsync(List<string> codes, int quantity = 1000)
        {
            if (!codes.Any()) return null;

            var queryParams = new List<string>
            {
                "Ver=1.0",
                $"User={_settings.Username}",
                $"Password={_settings.Password}"
            };

            for (int i = 0; i < codes.Count; i++)
            {
                int index = i + 1;
                queryParams.Add($"Itemcode_{index}={codes[i]}");
                queryParams.Add($"Quantity_{index}={quantity}");
            }

            string fullUrl = $"productremotedetails?{string.Join("&", queryParams)}";

            var response = await _httpClient.GetAsync(fullUrl);
            response.EnsureSuccessStatusCode();

            var xmlStream = await response.Content.ReadAsStreamAsync();
            var serializer = new XmlSerializer(typeof(GranitAvailabilityResponse));

            return (GranitAvailabilityResponse)serializer.Deserialize(xmlStream);
        }
    }
}
