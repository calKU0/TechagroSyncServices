using AgrobisSyncService.DTOs;
using AgrobisSyncService.Helpers;
using AgrobisSyncService.Settings;
using Serilog;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgrobisSyncService.Services
{
    public class ProductsSyncService
    {
        private readonly AgrobisApiSettings _apiSettings;
        private readonly HttpClient _client;
        public ProductsSyncService(HttpClient client)
        {
            _apiSettings = AppSettingsLoader.LoadApiSettings();
            _client = client;

            _client.BaseAddress = new Uri(_apiSettings.BaseUrl);
            _client.DefaultRequestHeaders.Add("ApiKey", _apiSettings.ApiKey);
        }

        public async Task<List<ProductsResponse>> FetchProducts()
        {
            try
            {
                int page = 1;
                bool hasMore = true;
                List<ProductsResponse> allProducts = new List<ProductsResponse>();

                Log.Information($"Starting products sync.");

                while (hasMore)
                {
                    try
                    {
                        var url = $"api/export/getOffer?page={page}&size={_apiSettings.ProductsPerPage}";
                        Log.Information("Fetching 1000 products from page {Page}", page);
                        var response = await _client.PostAsync(url, null);

                        response.EnsureSuccessStatusCode();

                        var json = await response.Content.ReadAsStringAsync();

                        if (string.IsNullOrWhiteSpace(json))
                        {
                            Log.Information("API returned empty response for this page. Ending paging.");
                            hasMore = false;
                        }

                        List<ProductsResponse> products = JsonSerializer.Deserialize<List<ProductsResponse>>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });

                        if (products.Count == 0)
                        {
                            Log.Information("API returned empty response for this page. Ending paging.");
                            hasMore = false;
                        }

                        allProducts.AddRange(products);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Error fetching products from page {page}.");
                        continue;
                    }
                    finally
                    {
                        page++;
                    }
                }

                Log.Information($"Finished products sync. Total products fetched: {allProducts.Count}");
                return allProducts;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during product sync.");
                return new List<ProductsResponse>();
            }
        }
    }
}
