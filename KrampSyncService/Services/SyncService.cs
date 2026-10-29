using KrampSyncService.Clients;
using KrampSyncService.DTOs;
using KrampSyncService.Settings;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TechagroSyncServices.Shared.Helpers;

namespace KrampSyncService.Services
{
    public class SyncService
    {
        private readonly KrampApiClient _apiClient;
        private readonly RequestLimiter _limiter;
        private readonly KrampApiSettings _apiSettings;
        private readonly string _productsStatePath;

        private Dictionary<string, ProductState> _states;

        public SyncService(KrampApiClient apiClient, RequestLimiter limiter, KrampApiSettings apiSettings, string productsStatePath)
        {
            _apiClient = apiClient;
            _limiter = limiter;
            _apiSettings = apiSettings;
            _productsStatePath = productsStatePath;
        }

        // Refreshes prices and stock of the SKUs that waited the longest, using as much of the daily limit as needed.
        // Returns products that were refreshed in this run and have both price and stock known.
        public async Task<List<ProductState>> RefreshProductsAsync(List<string> allowedCodes)
        {
            var states = LoadStates(allowedCodes);
            var refreshed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Prices - first, because products without a price are never synced.
            // Refreshed only when older than PricesRefreshHours, so they use a small part of the limit.
            var pricesDeadline = DateTime.Now.AddHours(-_apiSettings.PricesRefreshHours);
            var priceCandidates = states.Values
                .Where(s => s.PriceUpdatedAt == null || s.PriceUpdatedAt < pricesDeadline)
                .OrderBy(s => s.PriceUpdatedAt ?? DateTime.MinValue)
                .Select(s => s.Sku)
                .ToList();

            await RefreshInBatchesAsync(KrampEndpoint.Prices, priceCandidates, _apiSettings.PricesBatchSize, async batch =>
            {
                var response = await _apiClient.GetPricesAsync(batch);
                if (response == null)
                    return false;

                ApplyPrices(states, batch, response);
                refreshed.UnionWith(batch);
                return true;
            });

            // 2. Stock - refreshed on every run
            var stockCandidates = states.Values
                .OrderBy(s => s.StockUpdatedAt ?? DateTime.MinValue)
                .Select(s => s.Sku)
                .ToList();

            await RefreshInBatchesAsync(KrampEndpoint.Atp, stockCandidates, _apiSettings.AtpBatchSize, async batch =>
            {
                var response = await _apiClient.GetAtpAsync(batch);
                if (response == null)
                    return false;

                ApplyStock(states, batch, response);
                refreshed.UnionWith(batch);
                return true;
            });

            _limiter.LogUsage();

            var incomplete = states.Values.Count(s => !s.IsComplete);
            if (incomplete > 0)
                Log.Information("{Count} products are still waiting for price or stock data.", incomplete);

            return states.Values
                .Where(s => refreshed.Contains(s.Sku) && s.IsComplete)
                .ToList();
        }

        // SKUs for which Kramp answered without a price - Kramp no longer sells them.
        // SKUs not refreshed yet or failed because of network errors are never returned here.
        public List<string> GetMissingSkus()
        {
            return (_states ?? new Dictionary<string, ProductState>()).Values
                .Where(s => s.PriceUpdatedAt != null && s.NetPrice == null)
                .Select(s => s.Sku)
                .ToList();
        }

        private async Task RefreshInBatchesAsync(KrampEndpoint endpoint, List<string> candidates, int batchSize, Func<List<string>, Task<bool>> fetchBatch)
        {
            int remaining = _limiter.GetRemaining();
            var skus = candidates.Take(remaining * batchSize).ToList();

            if (!skus.Any())
            {
                Log.Information("Nothing to refresh for {Endpoint} (candidates: {Candidates}, requests left today: {Remaining}).", endpoint, candidates.Count, remaining);
                return;
            }

            Log.Information("Refreshing {Endpoint} for {Count}/{Candidates} products (requests left today: {Remaining}).", endpoint, skus.Count, candidates.Count, remaining);

            for (int i = 0; i < skus.Count; i += batchSize)
            {
                var batch = skus.Skip(i).Take(batchSize).ToList();

                try
                {
                    if (!await fetchBatch(batch))
                        return;

                    SaveStates();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error when fetching {Endpoint} for products {From}-{To}.", endpoint, i + 1, i + batch.Count);
                }

                await Task.Delay(_apiSettings.RequestInterval);
            }
        }

        private static void ApplyStock(Dictionary<string, ProductState> states, List<string> batch, AtpResponse response)
        {
            var now = DateTime.Now;
            var atpBySku = (response.AtpData ?? new List<AtpItem>())
                .Where(a => a?.Id != null)
                .GroupBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Errors about single SKUs mention the SKU (e.g. "entity with id <123> does not exist", "Please request delivery time for item : 123"),
            // anything else means the whole batch failed
            var batchErrors = (response.Errors ?? new List<AtpError>())
                .Where(e => !batch.Any(sku => MentionsSku(e.ErrorMessage, sku)))
                .Select(e => e.ErrorMessage)
                .ToList();

            if (batchErrors.Any())
                throw new InvalidOperationException("Kramp ATP error: " + string.Join("; ", batchErrors));

            foreach (var sku in batch)
            {
                // SKUs missing in atp_data are not available for us
                states[sku].Stock = atpBySku.TryGetValue(sku, out var atp) ? atp.AvailableQuantity : 0;
                states[sku].StockUpdatedAt = now;
            }
        }

        // SKU as a separate word, so "123" is not found in "1234"
        private static bool MentionsSku(string message, string sku)
        {
            return !string.IsNullOrEmpty(message)
                && Regex.IsMatch(message, @"(?<![\w-])" + Regex.Escape(sku) + @"(?![\w-])", RegexOptions.IgnoreCase);
        }

        private static void ApplyPrices(Dictionary<string, ProductState> states, List<string> batch, List<PriceResponse> response)
        {
            var now = DateTime.Now;
            var pricesBySku = response
                .Where(p => p.Id != null)
                .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var sku in batch)
            {
                var state = states[sku];
                state.PriceUpdatedAt = now;

                if (!pricesBySku.TryGetValue(sku, out var price) || price.NetPrice == null)
                {
                    Log.Warning("Kramp has no price for product {Sku}.", sku);
                    state.NetPrice = null;
                    state.ListPrice = null;
                    continue;
                }

                state.NetPrice = price.NetPrice;
                state.ListPrice = price.ListPrice;
            }
        }

        private Dictionary<string, ProductState> LoadStates(List<string> allowedCodes)
        {
            var saved = FileUtils.ReadJson<List<ProductState>>(_productsStatePath)
                .Where(s => s?.Sku != null)
                .GroupBy(s => s.Sku, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Only SKUs from the import list are kept and requested
            _states = allowedCodes.ToDictionary(
                code => code,
                code => saved.TryGetValue(code, out var state) ? state : new ProductState { Sku = code },
                StringComparer.OrdinalIgnoreCase);

            return _states;
        }

        private void SaveStates() => FileUtils.WriteJson(_productsStatePath, _states.Values.ToList());
    }
}
