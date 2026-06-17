using GranitSyncService.Clients;
using GranitSyncService.DTOs;
using GranitSyncService.Settings;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GranitSyncService.Services
{
    public class SyncService
    {
        private readonly GranitApiClient _granitApiClient;
        private readonly GranitApiSettings _apiSettings;

        public SyncService(GranitApiClient apiClient, GranitApiSettings apiSettings)
        {
            _granitApiClient = apiClient;
            _apiSettings = apiSettings;
        }

        public async Task<List<GranitItem>> FetchProductStockAndPrices(List<string> codes)
        {
            var granitItems = new List<GranitItem>();

            try
            {
                Log.Information($"Starting batch fetch for {codes.Count} products from Granit API.");

                for (int i = 0; i < codes.Count; i += _apiSettings.MaxProductsBatch)
                {
                    var batch = codes.Skip(i).Take(_apiSettings.MaxProductsBatch).ToList();
                    Log.Information($"Fetching batch: items {i} to {Math.Min(i + _apiSettings.MaxProductsBatch, codes.Count)}...");
                    var response = await _granitApiClient.GetStockAndPricesAsync(batch, 1000);

                    if (response?.Items != null)
                    {
                        granitItems.AddRange(response.Items);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(_apiSettings.RequestInterval));
                }

                Log.Information($"Successfully fetched a total of {granitItems.Count} items.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error when fetching products stock and prices.");
            }

            return granitItems;
        }
    }
}
