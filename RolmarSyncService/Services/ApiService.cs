using RolmarSyncService.DTOs;
using RolmarSyncService.Helpers;
using RolmarSyncService.Settings;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TechagroSyncServices.Shared.Repositories;

namespace RolmarSyncService.Services
{
    public class ApiService
    {
        private readonly RolmarApiSettings _apiSettings;
        private readonly IProductRepository _productRepo;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiService(IProductRepository productRepo)
        {
            _productRepo = productRepo;
            _apiSettings = AppSettingsLoader.LoadApiSettings();

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_apiSettings.BaseUrl)
            };

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
        }

        public async Task SyncProducts()
        {
            try
            {
                Log.Information("Starting products synchronization...");

                var productsUrl = $"v1/product/products.php?m=getProducts&lang=pl";
                Log.Information($"Getting products from {productsUrl}");

                var products = await GetApiDataAsync<List<ProductsResponse>, Product>(productsUrl);
                if (!products.Any())
                {
                    Log.Warning("No products found. Aborting sync.");
                    return;
                }

                var stockUrl = $"v1/stock/stock.php?m=getStock&lang=pl";
                Log.Information($"Getting stock from {stockUrl}");
                var stock = await GetApiDataAsync<List<StockResponse>, StockItem>(stockUrl);
                if (!stock.Any())
                {
                    Log.Warning("No stock data found. Aborting sync.");
                    return;
                }

                var imagesUrl = $"v1/photo/photo.php?m=getPhotos&lang=pl";
                Log.Information($"Getting images from {imagesUrl}");

                var images = await GetApiDataAsync<List<PhotosResponse>, PhotoItem>(imagesUrl);
                if (!images.Any())
                {
                    Log.Warning("No images found. Aborting sync.");
                    return;
                }

                var fullProducts = BuildFullProductDtos(products, stock, images);

                Log.Information("Merged {Count} products", fullProducts.Count);

                //var productSync = new ProductSyncService(_productRepo);
                //await productSync.SyncToDatabaseAsync(fullProducts);

                Log.Information("Product synchronization completed successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during products synchronization");
            }
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

        // ===========================================================
        // MAPPING
        // ===========================================================

        private List<FullProductDto> BuildFullProductDtos(List<Product> products, List<StockItem> stock, List<PhotoItem> images)
        {
            var stockDict = stock.ToDictionary(s => s.Index, s => s);
            var imageDict = images.GroupBy(i => i.Index)
                                  .ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<FullProductDto>();

            foreach (var p in products)
            {
                // Safe conversions with logging
                int id = SafeConvertToInt(p.Id, "Id", p.ProductIndex);
                decimal weight = SafeConvertToDecimal(p.Weight, "Weight", p.ProductIndex);
                decimal price = SafeConvertToDecimal(p.Price, "Price", p.ProductIndex);

                var dto = new FullProductDto
                {
                    Id = id,
                    ProductIndex = p.ProductIndex,
                    Name = p.Name,
                    Description = p.Description,
                    Brand = p.Brand,
                    Weight = weight,
                    Specifications = p.Specifications,
                    Ean = p.Ean,
                    Cn = p.Cn,
                    Price = price,
                    Stock = stockDict.TryGetValue(p.ProductIndex, out var s) ? s.Stock : 0,
                    Unit = stockDict.TryGetValue(p.ProductIndex, out var s2) ? s2.Unit : p.Unit,
                    Images = imageDict.TryGetValue(p.ProductIndex, out var imgs) ? imgs : new List<PhotoItem>()
                };

                result.Add(dto);
            }

            return result;
        }

        private int SafeConvertToInt(string value, string fieldName, string productIndex)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out int result))
                return result;

            Log.Warning("Invalid int conversion for field '{Field}' in product '{ProductIndex}'. Value: '{Value}'", fieldName, productIndex, value);
            return 0;
        }

        private decimal SafeConvertToDecimal(string value, string fieldName, string productIndex)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0m;

            // Try normal parse
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                return result;

            Log.Warning("Invalid decimal conversion for field '{Field}' in product '{ProductIndex}'. Value: '{Value}'", fieldName, productIndex, value);
            return 0m;
        }
    }
}
