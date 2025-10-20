using AmapartsSyncService.DTOs;
using AmapartsSyncService.Helpers;
using AmapartsSyncService.Settings;
using CsvHelper;
using CsvHelper.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Helpers;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;

namespace AmapartsSyncService.Services
{
    public class ApiService
    {
        private readonly AmapartsApiSettings _apiSettings;
        private readonly IProductRepository _productRepo;
        private readonly decimal _defaulyMargin;
        private readonly List<MarginRange> _marginRanges;

        public ApiService(IProductRepository productRepo)
        {
            _productRepo = productRepo;
            _apiSettings = AppSettingsLoader.LoadApiSettings();
            _defaulyMargin = AppSettingsLoader.GetDefaultMargin();
            _marginRanges = AppSettingsLoader.GetMarginRanges();
        }

        public async Task SyncProducts()
        {
            using (var client = new HttpClient())
            {
                try
                {
                    client.BaseAddress = new Uri(_apiSettings.BaseUrl);
                    client.Timeout = TimeSpan.FromMinutes(10);

                    // 1. Trigger CSV generation for products
                    Log.Information($"Triggering product export. (This takes few minutes)");
                    await client.GetAsync($"export.php?q={_apiSettings.ApiKey}");

                    // 2. Trigger CSV generation for product parameters
                    Log.Information($"Triggering product parameters export. (This takes few minutes)");
                    await client.GetAsync($"exportfeatures.php?q={_apiSettings.ApiKey}");

                    int productInserted = 0;
                    int productUpdated = 0;

                    // 3. Fetch product CSV
                    List<ApiProductsResponse> productsDetails;
                    using (var stream = await client.GetStreamAsync($"products-{_apiSettings.ApiKey}.csv"))
                    using (var reader = new StreamReader(stream))
                    using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        Delimiter = ";",
                        HasHeaderRecord = true,
                        TrimOptions = TrimOptions.Trim,
                    }))
                    {
                        productsDetails = csv.GetRecords<ApiProductsResponse>().ToList();
                    }

                    // 4. Fetch parameters CSV
                    List<ProductParameterCsv> parameters;
                    using (var stream = await client.GetStreamAsync($"products-features-{_apiSettings.ApiKey}.csv"))
                    using (var reader = new StreamReader(stream))
                    using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        Delimiter = ";",
                        HasHeaderRecord = true,
                        TrimOptions = TrimOptions.Trim,
                    }))
                    {
                        parameters = csv.GetRecords<ProductParameterCsv>().ToList();
                    }

                    // 5. Group parameters
                    var parametersGrouped = parameters
                        .GroupBy(p => p.ProductCode)
                        .ToDictionary(
                            g => g.Key,
                            g => g.ToDictionary(r => r.Parameter, r => r.Value)
                        );

                    // 6. Collect to 1 dto
                    var products = productsDetails.Select(p => new ProductFullDto
                    {
                        ProductCode = p.ProductCode,
                        ProductName = p.ProductName,
                        Description = p.Description,
                        Manufacturer = p.Manufacturer,
                        StockQuantity = p.StockQuantity,
                        NetPurchasePrice = p.NetPurchasePrice,
                        Photos = (p.ProductImageUrl ?? string.Empty)
                            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => x.Trim().Trim('"'))
                            .Where(x => !string.IsNullOrWhiteSpace(x) && Uri.IsWellFormedUriString(x, UriKind.Absolute))
                            .ToList(),
                        Attributes = parametersGrouped.ContainsKey(p.ProductCode) ? parametersGrouped[p.ProductCode] : new Dictionary<string, string>()
                    }).ToList();

                    Log.Information($"Attempting to update {products.Count()} products in database.");
                    foreach (var product in products)
                    {
                        try
                        {
                            decimal applicableMargin = MarginHelper.CalculateMargin(product.NetPurchasePrice, _defaulyMargin, _marginRanges);

                            // 7. Updating details
                            var dto = new ProductDto
                            {
                                Id = 0,
                                Code = product.ProductCode,
                                Ean = null,
                                Name = product.ProductName,
                                Quantity = product.StockQuantity,
                                NetBuyPrice = product.NetPurchasePrice,
                                GrossBuyPrice = product.NetPurchasePrice * 1.23m,
                                NetSellPrice = product.NetPurchasePrice * ((applicableMargin / 100m) + 1),
                                GrossSellPrice = product.NetPurchasePrice * 1.23m * ((applicableMargin / 100m) + 1),
                                Vat = 23,
                                Weight = 0,
                                Brand = product.Manufacturer,
                                Unit = "szt.",
                                IntegrationCompany = "AMA"
                            };

                            int result = 1;
                            await _productRepo.UpsertProductAsync(dto);
                            if (result == 1)
                            {
                                productInserted++;
                                Log.Information($"Inserted product: Product Code = {product.ProductCode}, Name = {product.ProductName}");
                            }
                            else if (result == 2)
                            {
                                productUpdated++;
                                Log.Information($"Updated product: Product Code = {product.ProductCode}, Name = {product.ProductName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Failed to update/insert product: Product Code = {product.ProductCode}, Name = {product.ProductName}");
                        }

                        try
                        {
                            // 8. Updating description
                            var opisBuilder = new StringBuilder();
                            if (!string.IsNullOrWhiteSpace(product.Description) && product.Attributes.Any())
                            {
                                opisBuilder.Append("<h2>Opis produktu</h2>");
                            }

                            if (!string.IsNullOrWhiteSpace(product.Description))
                                opisBuilder.Append(product.Description);

                            if (product.Attributes != null && product.Attributes.Any())
                            {
                                opisBuilder.Append("<p><b>Parametry: </b>");

                                // Join each parameter as "Key = Value"
                                opisBuilder.Append(string.Join(", ", product.Attributes
                                    .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                                    .Select(kv => $"{kv.Key}: {kv.Value}")));

                                opisBuilder.Append("</p>");
                            }

                            string truncatedDesc = DescriptionHelper.TruncateHtml(opisBuilder.ToString(), 1000);

                            await _productRepo.UpdateProductDescriptionAsync(product.ProductCode, truncatedDesc);
                            Log.Information($"Updated product description: Product Code = {product.ProductCode}, Name = {product.ProductName}");
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Failed to update/insert product description: Product Code = {product.ProductCode}, Name = {product.ProductName}");
                        }

                        try
                        {
                            // 9. Updating images
                            if (product.Photos != null)
                            {
                                foreach (var imgUrl in product.Photos.Where(u => !string.IsNullOrEmpty(u)))
                                {
                                    try
                                    {
                                        byte[] imageData = await client.GetByteArrayAsync(imgUrl);
                                        string path = new Uri(imgUrl).AbsolutePath;
                                        string firstSegment = path.TrimStart('/').Split('/')[0];
                                        string imageId = firstSegment.Split('-')[0];

                                        await _productRepo.UpsertProductImageAsync(product.ProductCode, imageId, imageData);
                                        Log.Information($"Updated product image: Product Code = {product.ProductCode}, Name = {product.ProductName}");
                                    }
                                    catch (HttpRequestException httpEx)
                                    {
                                        Log.Error(httpEx, $"Failed to download image for Product Code = {product.ProductCode}, Name = {product.ProductName}");
                                    }
                                }
                            }
                        }
                        catch (HttpRequestException httpEx)
                        {
                            Log.Error(httpEx, $"Failed to download product image: Product Code = {product.ProductCode}, Name = {product.ProductName}");
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Failed to update/insert product image: Product Code = {product.ProductCode}, Name = {product.ProductName}");
                        }
                    }
                    Log.Information("Products imported: {Total} out of {ToUpdate}, Inserted: {Inserted}, Updated: {Updated}, Failed: {Failed}", productInserted + productUpdated, products.Count(), productInserted, productUpdated, products.Count() - productInserted + productUpdated);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error while fetching products");
                }
            }
        }
    }
}