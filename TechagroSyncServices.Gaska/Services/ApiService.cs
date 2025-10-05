using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TechagroSyncServices.Gaska.DTOs;
using TechagroSyncServices.Gaska.Helpers;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;

namespace TechagroSyncServices.Gaska.Services
{
    public class ApiService
    {
        private readonly GaskaApiSettings _apiSettings;
        private readonly IProductRepository _productRepo;
        private readonly int _margin;

        public ApiService(IProductRepository productRepo)
        {
            _productRepo = productRepo;
            _apiSettings = AppSettingsLoader.LoadApiSettings();
            _margin = AppSettingsLoader.GetMargin();
        }

        public async Task SyncProducts()
        {
            using (var client = new HttpClient())
            {
                ApiHelper.AddDefaultHeaders(_apiSettings, client);

                int page = 1;
                bool hasMore = true;
                int productInserted = 0;
                int productUpdated = 0;

                while (hasMore)
                {
                    try
                    {
                        var url = $"{_apiSettings.BaseUrl}/products?page={page}&perPage={_apiSettings.ProductsPerPage}&lng=pl";
                        Log.Information($"Sending request to {url}.");
                        var response = await client.GetAsync(url);

                        if (!response.IsSuccessStatusCode)
                        {
                            Log.Error($"API error while fetching page {page}: {response.StatusCode}");
                            continue;
                        }

                        var json = await response.Content.ReadAsStringAsync();
                        var apiResponse = JsonConvert.DeserializeObject<ApiProductsResponse>(json);

                        if (apiResponse.Products == null || apiResponse.Products.Count == 0)
                        {
                            hasMore = false;
                            break;
                        }

                        Log.Information($"Got response with {apiResponse.Products.Count()} products.");
                        Log.Information($"Attempting to update {apiResponse.Products.Count()} products in database.");

                        foreach (var apiProduct in apiResponse.Products)
                        {
                            try
                            {
                                string name = apiProduct.Name;

                                if (!string.IsNullOrWhiteSpace(name))
                                {
                                    // Match leading product code (numbers, dots, slashes, letters)
                                    var match = Regex.Match(name, @"^(?<code>[0-9A-Za-z./x-]+)\s+(?<rest>.+)$");

                                    if (match.Success)
                                    {
                                        name = $"{match.Groups["rest"].Value} {match.Groups["code"].Value}";
                                    }
                                }

                                var dto = new ProductDto
                                {
                                    Id = apiProduct.Id,
                                    Code = apiProduct.CodeGaska,
                                    Ean = apiProduct.Ean,
                                    Name = name,
                                    Quantity = (decimal)apiProduct.InStock,
                                    NetBuyPrice = apiProduct.NetPrice,
                                    GrossBuyPrice = apiProduct.GrossPrice,
                                    NetSellPrice = apiProduct.NetPrice * ((_margin / 100m) + 1),
                                    GrossSellPrice = apiProduct.GrossPrice * ((_margin / 100m) + 1),
                                    Vat = 23,
                                    Weight = (decimal)apiProduct.GrossWeight,
                                    Brand = apiProduct.SupplierName,
                                    Unit = apiProduct.Unit,
                                    IntegrationCompany = "GĄSKA"
                                };

                                int result = await _productRepo.UpsertProductAsync(dto);
                                if (result == 1)
                                {
                                    productInserted++;
                                    Log.Information($"Inserted product: Product Code = {apiProduct.CodeGaska}, Name = {apiProduct.Name}");
                                }
                                else if (result == 2)
                                {
                                    productUpdated++;
                                    Log.Information($"Updated product: Product Code = {apiProduct.CodeGaska}, Name = {apiProduct.Name}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, $"Failed to insert/update product {apiProduct.CodeGaska}");
                            }
                        }
                        Log.Information($"Successfully fetched and updated {apiResponse.Products.Count} products.");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Error while getting products from page {page}.");
                    }
                    finally
                    {
                        page++;
                        await Task.Delay(TimeSpan.FromSeconds(_apiSettings.ProductsInterval));
                    }
                }
                Log.Information("Products imported: {Total}, Inserted: {Inserted}, Updated: {Updated}", productInserted + productUpdated, productInserted, productUpdated);
            }
        }

        public async Task SyncProductDetails()
        {
            List<int> productsToUpdate = new List<int>();

            try
            {
                productsToUpdate = await _productRepo.GetProductsWithoutDescription(_apiSettings.ProductPerDay);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while fetching products without description.");
                return;
            }

            if (!productsToUpdate.Any())
            {
                return;
            }

            using (var client = new HttpClient())
            {
                ApiHelper.AddDefaultHeaders(_apiSettings, client);

                foreach (var plu in productsToUpdate)
                {
                    try
                    {
                        var response = await client.GetAsync($"{_apiSettings.BaseUrl}/product?id={plu}&lng=pl");
                        if (!response.IsSuccessStatusCode)
                        {
                            Log.Error($"API error while fetching product details. PLU: {plu}, Status: {response.StatusCode}");
                            continue;
                        }

                        var json = await response.Content.ReadAsStringAsync();
                        var apiResponse = JsonConvert.DeserializeObject<ApiProductResponse>(json);

                        if (apiResponse?.Product == null)
                            continue;

                        var p = apiResponse.Product;

                        // Build the HTML description for Opis
                        var opisBuilder = new StringBuilder();

                        if (p.Parameters?.Any() == true)
                        {
                            opisBuilder.Append("<p><b>Parametry: </b>")
                                       .Append(string.Join(", ", p.Parameters.Select(ps => $"{ps.AttributeName} : {ps.AttributeValue}")))
                                       .Append("</p>");
                        }

                        if (p.CrossNumbers?.Any() == true)
                        {
                            opisBuilder.Append("<p><b>Numery referencyjne: </b>")
                                       .Append(string.Join(", ", p.CrossNumbers.Select(c => c.CrossNumber)))
                                       .Append("</p>");
                        }

                        // Applications section
                        if (p.Applications?.Any() == true)
                        {
                            var appsByParent = p.Applications
                                                      .GroupBy(a => a.ParentID)
                                                      .ToDictionary(g => g.Key, g => g.ToList());

                            string BuildApplicationsHtml(List<ApiApplication> apps, int depth = 1)
                            {
                                if (!apps.Any()) return string.Empty;

                                var sb = new StringBuilder();
                                sb.Append("<ul>");

                                foreach (var app in apps)
                                {
                                    sb.Append("<li>").Append(app.Name);

                                    if (appsByParent.ContainsKey(app.Id))
                                        sb.Append(BuildApplicationsHtml(appsByParent[app.Id], depth + 1));

                                    sb.Append("</li>");
                                }

                                sb.Append("</ul>");
                                return sb.ToString();
                            }

                            if (appsByParent.ContainsKey(0))
                            {
                                opisBuilder.Append("<div class=\"product-applications\">")
                                           .Append("<p><b>Zastosowanie: </b></p>")
                                           .Append(BuildApplicationsHtml(appsByParent[0]))
                                           .Append("</div>");
                            }
                        }

                        string nowyOpis = opisBuilder?.ToString() ?? string.Empty;
                        nowyOpis = DescriptionHelper.TruncateHtml(nowyOpis, 1000);

                        // Update description
                        await _productRepo.UpdateProductDescriptionAsync(p.CodeGaska, nowyOpis);
                        Log.Information($"Updated product description: Product Code = {p.CodeGaska}, Name = {p.Name}");

                        // Upsert images
                        if (p.Images != null)
                        {
                            foreach (var img in p.Images)
                            {
                                if (string.IsNullOrEmpty(img.Url))
                                    continue;

                                byte[] imageData = await client.GetByteArrayAsync(img.Url);

                                await _productRepo.UpsertProductImageAsync(p.CodeGaska, img.Title ?? "image", imageData);
                                Log.Information($"Updated product image: Product Code = {p.CodeGaska}, Name = {p.Name}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Error processing product {plu}");
                    }
                    finally
                    {
                        await Task.Delay(TimeSpan.FromSeconds(_apiSettings.ProductInterval));
                    }
                }
            }
        }
    }
}