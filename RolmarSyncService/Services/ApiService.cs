using RolmarSyncService.DTOs;
using RolmarSyncService.Helpers;
using RolmarSyncService.Settings;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RolmarSyncService.Services
{
    public class ApiService
    {
        private readonly RolmarApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiService(HttpClient client)
        {
            _apiSettings = AppSettingsLoader.LoadApiSettings();
            _httpClient = client;
            _httpClient.BaseAddress = new Uri(_apiSettings.BaseUrl);
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
        }

        public async Task<List<Product>> FetchProducts()
        {
            var productsUrl = $"v1/product/products.php?m=getProducts&lang=pl";
            var products = await GetApiDataAsync<List<ProductsResponse>, Product>(productsUrl);
            return products;
        }

        public async Task<List<StockItem>> FetchProductStock()
        {
            var stockUrl = $"v1/stock/stock.php?m=getStock&lang=pl";
            var stock = await GetApiDataAsync<List<StockResponse>, StockItem>(stockUrl);
            return stock;
        }

        public async Task<List<PhotoItem>> FetchProductImages()
        {
            var imagesUrl = $"v1/photo/photo.php?m=getPhotos&lang=pl";
            var images = await GetApiDataAsync<List<PhotosResponse>, PhotoItem>(imagesUrl);
            return images;
        }

        // ===========================================================
        // API CALLS
        // ===========================================================

        private async Task<List<TItem>> GetApiDataAsync<TRoot, TItem>(string url) where TRoot : class
        {
            try
            {
                var body = new
                {
                    data = new[]
                    {
                        new { wsKey = _apiSettings.ApiKey }
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();

                var rootObjects = JsonSerializer.Deserialize<TRoot>(json, _jsonOptions);

                var allItems = new List<TItem>();

                if (rootObjects is List<ProductsResponse> products)
                {
                    foreach (var p in products)
                        allItems.AddRange(p.Result.Cast<TItem>());
                }
                else if (rootObjects is List<PhotosResponse> photos)
                {
                    foreach (var p in photos)
                        allItems.AddRange(p.Result.Cast<TItem>());
                }
                else if (rootObjects is List<StockResponse> stock)
                {
                    foreach (var s in stock)
                        allItems.AddRange(s.Result.Cast<TItem>());
                }

                return allItems;
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "Network error calling {Url}", url);
                return new List<TItem>();
            }
            catch (JsonException ex)
            {
                Log.Error(ex, "JSON parsing error calling {Url}", url);
                return new List<TItem>();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unknown error calling {Url}", url);
                return new List<TItem>();
            }
        }
    }
}