using HiFiSyncService.DTOs;
using HiFiSyncService.Services;
using HiFiSyncService.Settings;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HiFiSyncService.Clients
{
    public class HiFiApiClient
    {
        // HIFI recommends to cache customer accounts for about a day
        private static readonly TimeSpan CustomerCacheTime = TimeSpan.FromDays(1);
        private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(60);

        private readonly HiFiApiSettings _settings;
        private readonly RequestThrottler _throttler;
        private readonly HttpClient _httpClient;

        private CustomerResponse _customer;
        private DateTime _customerResolvedAt = DateTime.MinValue;

        public HiFiApiClient(HiFiApiSettings settings, RequestThrottler throttler)
        {
            _settings = settings;
            _throttler = throttler;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromMinutes(5)
            };

            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", settings.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("X-Api-Secret", settings.ApiSecret);
            _httpClient.DefaultRequestHeaders.Add("X-Api-Locale", settings.Locale);
        }

        // Up to 20 references per request, unknown references are skipped in the response
        public async Task<List<PriceResponse>> GetPricesAsync(IReadOnlyList<string> references)
        {
            var body = references.Select(r => new PriceRequest { Reference = r, Quantity = 1 }).ToList();
            using (var response = await SendAsync(HttpMethod.Post, "price", body, true))
            {
                return await ReadAsync<List<PriceResponse>>(response, "price");
            }
        }

        // Up to 20 references per request, unknown references are skipped in the response
        public async Task<List<AvailabilityResponse>> GetAvailabilityAsync(IReadOnlyList<string> references)
        {
            using (var response = await SendAsync(HttpMethod.Post, "availability", references, true))
            {
                return await ReadAsync<List<AvailabilityResponse>>(response, "availability");
            }
        }

        private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object body, bool customerScoped)
        {
            // Customer-specific endpoints need legal entity and customer id headers
            var customer = customerScoped ? await GetCustomerAsync() : null;

            for (int attempt = 1; ; attempt++)
            {
                var response = await _throttler.RunAsync(async () =>
                {
                    using (var request = new HttpRequestMessage(method, url))
                    {
                        if (customer != null)
                        {
                            request.Headers.Add("X-Api-Legal-Entity", customer.LegalEntity);
                            request.Headers.Add("X-Api-Customer", customer.Id);
                        }

                        if (body != null)
                            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

                        return await _httpClient.SendAsync(request);
                    }
                });

                PostponeWhenLimitUsedUp(response);

                if ((int)response.StatusCode != 429)
                    return response;

                // Rate limit exceeded - no requests allowed until Retry-After passes
                var retryAfter = response.Headers.RetryAfter?.Delta ?? DefaultRetryAfter;
                response.Dispose();
                _throttler.Postpone(retryAfter);

                if (attempt > _settings.MaxRetries)
                    throw new HttpRequestException($"HIFI {url} request is still rate limited after {attempt} attempts.");

                Log.Warning("HIFI rate limit exceeded on {Url}. Retrying in {Seconds}s.", url, retryAfter.TotalSeconds);
            }
        }

        // When the per-minute budget is used up, wait until it resets instead of hitting 429
        private void PostponeWhenLimitUsedUp(HttpResponseMessage response)
        {
            if (TryGetHeaderInt(response, "X-Api-RateLimit-RPM-Remaining", out int remaining) && remaining <= 0
                && TryGetHeaderInt(response, "X-Api-RateLimit-RPM-Reset", out int resetSeconds))
            {
                _throttler.Postpone(TimeSpan.FromSeconds(resetSeconds));
            }
        }

        private async Task<CustomerResponse> GetCustomerAsync()
        {
            if (_customer != null && DateTime.Now - _customerResolvedAt < CustomerCacheTime)
                return _customer;

            List<CustomerResponse> customers;
            using (var response = await SendAsync(HttpMethod.Get, "customer", null, false))
            {
                customers = await ReadAsync<List<CustomerResponse>>(response, "customer");
            }

            _customer = customers?.FirstOrDefault(c => string.Equals(c.LegalEntity, _settings.LegalEntity, StringComparison.OrdinalIgnoreCase));
            if (_customer == null)
            {
                var available = string.Join(", ", (customers ?? new List<CustomerResponse>()).Select(c => $"{c.Id} ({c.LegalEntity})"));
                throw new InvalidOperationException($"HIFI customer for legal entity {_settings.LegalEntity} not found. Available: {available}");
            }

            _customerResolvedAt = DateTime.Now;
            Log.Information("Using HIFI customer {CustomerId} ({LegalEntity}, {Currency}).", _customer.Id, _customer.LegalEntity, _customer.Currency);

            return _customer;
        }

        private static async Task<T> ReadAsync<T>(HttpResponseMessage response, string endpoint)
        {
            await EnsureSuccessAsync(response, endpoint);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json);
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage response, string endpoint)
        {
            if (response.IsSuccessStatusCode)
                return;

            var json = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"HIFI {endpoint} request failed with status {(int)response.StatusCode}: {json}");
        }

        private static bool TryGetHeaderInt(HttpResponseMessage response, string name, out int value)
        {
            value = 0;
            return response.Headers.TryGetValues(name, out var values) && int.TryParse(values.FirstOrDefault(), out value);
        }
    }
}
