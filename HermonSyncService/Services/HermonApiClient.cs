using HermonSyncService.DTOs;
using HermonSyncService.Settings;
using Serilog;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HermonSyncService.Helpers
{
    public class HermonApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly HermonApiSettings _settings;
        private string _token;

        public HermonApiClient(HermonApiSettings settings)
        {
            _settings = settings;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/'))
            };
        }

        /// <summary>
        /// Authenticates against the HERMON API and stores the bearer token internally.
        /// </summary>
        public async Task<string> AuthenticateAsync()
        {
            var url = $"{_settings.BaseUrl.TrimEnd('/')}/client-service/authenticate";
            var payload = new
            {
                Login = _settings.Username,
                Password = _settings.Password
            };

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning("Authentication failed with status: {StatusCode}", response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var auth = JsonSerializer.Deserialize<AuthResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (auth == null || !string.IsNullOrEmpty(auth.Error))
                {
                    Log.Warning("Authentication error: {Error}", auth?.Error);
                    return null;
                }

                _token = auth.Token;
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
                return _token;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error authenticating with HERMON API");
                return null;
            }
        }

        /// <summary>
        /// Performs a POST request to the given relative URL with optional JSON body.
        /// </summary>
        public async Task<HttpResponseMessage> PostAsync(string relativeUrl, object body)
        {
            var url = $"{_settings.BaseUrl.TrimEnd('/')}/{relativeUrl.TrimStart('/')}";
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                return await _httpClient.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error performing POST to {Url}", url);
                throw;
            }
        }
    }
}