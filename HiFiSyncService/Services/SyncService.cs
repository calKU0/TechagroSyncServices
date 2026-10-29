using HiFiSyncService.Clients;
using HiFiSyncService.Constants;
using HiFiSyncService.DTOs;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechagroSyncServices.Shared.DTOs;

namespace HiFiSyncService.Services
{
    public class SyncService
    {
        private readonly HiFiApiClient _apiClient;

        public SyncService(HiFiApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        // Live prices and stock of the given references, fetched in batches of 20 (API limit).
        // References without a price are not returned, so they are never synced.
        // Result is incomplete when any batch failed.
        public async Task<FetchResult<HiFiProduct>> FetchProductsAsync(List<string> references)
        {
            var products = new List<HiFiProduct>();
            var missingCodes = new List<string>();
            bool isComplete = true;

            Log.Information("Starting live fetch of prices and stock for {Count} products from HIFI API.", references.Count);

            for (int i = 0; i < references.Count; i += ServiceConstants.MaxLiveBatchSize)
            {
                var batch = references.Skip(i).Take(ServiceConstants.MaxLiveBatchSize).ToList();

                try
                {
                    var prices = await _apiClient.GetPricesAsync(batch);
                    var availability = await _apiClient.GetAvailabilityAsync(batch);

                    var batchProducts = MergeBatch(prices, availability).ToList();
                    products.AddRange(batchProducts);

                    // Only successful batches are checked, failed ones are logged as errors
                    var fetched = new HashSet<string>(batchProducts.Select(p => p.Reference), StringComparer.OrdinalIgnoreCase);
                    missingCodes.AddRange(batch.Where(r => !fetched.Contains(r)));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error when fetching prices and stock for products {From}-{To}.", i + 1, i + batch.Count);
                    isComplete = false;
                }
            }

            if (missingCodes.Any())
            {
                Log.Warning("Missing {Count} product codes", missingCodes.Count);
                foreach (var code in missingCodes)
                    Log.Warning("Missing: {Code}", code);
            }

            Log.Information("Successfully fetched a total of {Count} products.", products.Count);

            return isComplete ? FetchResult<HiFiProduct>.Complete(products) : FetchResult<HiFiProduct>.Incomplete(products);
        }

        private static IEnumerable<HiFiProduct> MergeBatch(List<PriceResponse> prices, List<AvailabilityResponse> availability)
        {
            var availabilityByReference = (availability ?? new List<AvailabilityResponse>())
                .Where(a => a?.Reference != null)
                .GroupBy(a => a.Reference, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var price in (prices ?? new List<PriceResponse>()).Where(p => p?.Reference != null))
            {
                // Only stock of the warehouse serving our legal entity, supply hub is not counted
                availabilityByReference.TryGetValue(price.Reference, out var stock);

                yield return new HiFiProduct
                {
                    Reference = price.Reference,
                    NetPrice = price.UnitPrice,
                    Currency = price.Currency,
                    Stock = stock?.StockQuantity ?? 0
                };
            }
        }
    }
}
