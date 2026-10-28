using KrampSyncService.DTOs;
using KrampSyncService.Services;
using KrampSyncService.Settings;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace KrampSyncService.Clients
{
    public class KrampApiClient
    {
        private static readonly TimeSpan TokenExpirationMargin = TimeSpan.FromMinutes(2);

        private readonly KrampApiSettings _settings;
        private readonly RequestLimiter _limiter;
        private readonly HttpClient _httpClient;

        private string _accessToken;
        private string _refreshToken;
        private DateTime _tokenExpiresAt = DateTime.MinValue;

        public KrampApiClient(KrampApiSettings settings, RequestLimiter limiter)
        {
            _settings = settings;
            _limiter = limiter;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/")
            };
        }

        // Returns null when the daily limit is reached
        public async Task<List<PriceResponse>> GetPricesAsync(IReadOnlyList<string> skus)
        {
            var request = BuildRequest(skus, 1);
            var response = await PostAsync<List<PriceResponse>>("products/prices", request, KrampEndpoint.Prices);

            // Unknown SKUs are returned as null items
            return response?.Where(p => p != null).ToList();
        }

        // Returns null when the daily limit is reached
        public Task<AtpResponse> GetAtpAsync(IReadOnlyList<string> skus)
        {
            var request = BuildRequest(skus, _settings.AtpRequestedQuantity);
            return PostAsync<AtpResponse>("products/atp", request, KrampEndpoint.Atp);
        }

        private ProductsRequest BuildRequest(IReadOnlyList<string> skus, int quantity)
        {
            return new ProductsRequest
            {
                CustomerNumber = _settings.CustomerNumber,
                Products = skus.Select(sku => new ProductRequestItem { Id = sku, Quantity = quantity.ToString() }).ToList()
            };
        }

        private async Task<T> PostAsync<T>(string url, object body, KrampEndpoint endpoint) where T : class
        {
            if (!await EnsureAuthenticatedAsync() || !_limiter.TryConsume(endpoint))
                return null;

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

                using (var response = await _httpClient.SendAsync(request))
                {
                    var json = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                        throw new HttpRequestException($"Kramp {endpoint} request failed with status {(int)response.StatusCode}: {json}");

                    return JsonSerializer.Deserialize<T>(json);
                }
            }
        }

        private async Task<bool> EnsureAuthenticatedAsync()
        {
            if (_accessToken != null && DateTime.UtcNow < _tokenExpiresAt - TokenExpirationMargin)
                return true;

            if (_refreshToken != null && await TryAuthenticateAsync("authenticate/refresh", new Dictionary<string, string>
            {
                ["refresh_token"] = _refreshToken
            }))
            {
                return true;
            }

            return await TryAuthenticateAsync("authenticate", new Dictionary<string, string>
            {
                ["username"] = _settings.Username,
                ["password"] = _settings.Password,
                ["customer_number"] = _settings.CustomerNumber
            });
        }

        private async Task<bool> TryAuthenticateAsync(string url, Dictionary<string, string> form)
        {
            _accessToken = null;
            _refreshToken = null;

            using (var response = await _httpClient.PostAsync(url, new FormUrlEncodedContent(form)))
            {
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Log.Error("Kramp authentication ({Url}) failed with status {StatusCode}: {Response}", url, (int)response.StatusCode, json);
                    return false;
                }

                var auth = JsonSerializer.Deserialize<AuthResponse>(json);
                _accessToken = auth.AccessToken;
                _refreshToken = auth.RefreshToken;
                _tokenExpiresAt = DateTime.UtcNow.AddSeconds(auth.ExpiresIn);

                Log.Information("Authenticated in Kramp API. Token valid for {Seconds}s.", auth.ExpiresIn);
                return true;
            }
        }
    }
}
