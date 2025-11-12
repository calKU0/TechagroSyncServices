using Serilog;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgroramiSyncService.Services
{
    public class GraphQLClient
    {
        private readonly HttpClient _client;
        private readonly JsonSerializerOptions _jsonOptions;

        public GraphQLClient(string baseUrl)
        {
            _client = new HttpClient { BaseAddress = new Uri(baseUrl) };
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<T> ExecuteAsync<T>(string query, string token = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "graphql")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { query }), Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrEmpty(token))
                request.Headers.Add("Authorization", $"Bearer {token}");

            var response = await _client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Log.Error("GraphQL error: {Code} {Body}", response.StatusCode, content);
                throw new HttpRequestException($"GraphQL query failed: {response.StatusCode}");
            }

            var root = JsonDocument.Parse(content);
            var dataElement = root.RootElement.GetProperty("data");
            return JsonSerializer.Deserialize<T>(dataElement.ToString(), _jsonOptions);
        }
    }
}