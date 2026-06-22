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
            _client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromMinutes(3)
            };

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<T> ExecuteWithRetryAsync<T>(string query, string token = null)
        {
            const int maxRetries = 3;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, "graphql")
                    {
                        Content = new StringContent(
                            JsonSerializer.Serialize(new { query }),
                            Encoding.UTF8,
                            "application/json")
                    };

                    if (!string.IsNullOrEmpty(token))
                        request.Headers.Add("Authorization", $"Bearer {token}");

                    var response = await _client.SendAsync(request);
                    var content = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        Log.Error(
                            "GraphQL error: {Code} {Body}",
                            response.StatusCode,
                            content);

                        throw new HttpRequestException(
                            $"GraphQL query failed: {response.StatusCode}");
                    }

                    using (var root = JsonDocument.Parse(content))
                    {
                        var dataElement = root.RootElement.GetProperty("data");

                        return JsonSerializer.Deserialize<T>(
                            dataElement.GetRawText(),
                            _jsonOptions);
                    }
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    var delay = TimeSpan.FromMinutes(Math.Pow(2, attempt));

                    Log.Warning(
                        ex,
                        "GraphQL request failed. Retrying in {Delay}. Attempt {Attempt}/{MaxRetries}",
                        delay,
                        attempt + 1,
                        maxRetries);

                    await Task.Delay(delay);
                }
            }

            throw new InvalidOperationException("Unexpected retry termination.");
        }
    }
}