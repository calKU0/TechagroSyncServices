using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TechagroSyncServices.Agroland.DTOs;
using TechagroSyncServices.Agroland.Helpers;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;

namespace TechagroSyncServices.Agroland.Services
{
    public class ApiService
    {
        private readonly AgrolandApiSettings _apiSettings;
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
                try
                {
                    int productInserted = 0;
                    int productUpdated = 0;

                    var url = $"{_apiSettings.BaseUrl}1/3/utf8/{_apiSettings.ApiKey}?stream=true";
                    Log.Information($"Sending request to {url}.");
                    var response = await client.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        Log.Error($"API error while fetching products: {response.StatusCode}");
                        return;
                    }
                    var xml = await response.Content.ReadAsStringAsync();

                    Products apiResponse;
                    var serializer = new XmlSerializer(typeof(Products));
                    using (var reader = new StringReader(xml))
                    {
                        apiResponse = (Products)serializer.Deserialize(reader);
                    }

                    Log.Information($"Got response with {apiResponse.ProductList.Count()} products.");
                    Log.Information($"Attempting to update {apiResponse.ProductList.Count()} products in database.");
                    foreach (var apiProduct in apiResponse.ProductList)
                    {
                        try
                        {
                            var dto = new ProductDto
                            {
                                Id = apiProduct.Id,
                                Code = apiProduct.Ean,
                                Ean = apiProduct.Ean,
                                Name = apiProduct.Name,
                                Quantity = apiProduct.Qty,
                                NetBuyPrice = apiProduct.PriceAfterDiscountNet,
                                GrossBuyPrice = (apiProduct.PriceAfterDiscountNet * 1.23m),
                                NetSellPrice = apiProduct.PriceAfterDiscountNet * ((_margin / 100m) + 1),
                                GrossSellPrice = (apiProduct.PriceAfterDiscountNet * 1.23m) * ((_margin / 100m) + 1),
                                Vat = apiProduct.Vat,
                                Weight = apiProduct.Weight ?? 0,
                                Brand = apiProduct.Brand?.Name,
                                Unit = apiProduct.Unit,
                                IntegrationCompany = "AGROLAND"
                            };

                            int result = await _productRepo.UpsertProductAsync(dto);
                            if (result == 1)
                            {
                                productInserted++;
                                Log.Information($"Inserted product: Product EAN = {apiProduct.Ean}, Name = {apiProduct.Name}");
                            }
                            else if (result == 2)
                            {
                                productUpdated++;
                                Log.Information($"Updated product: Product EAN = {apiProduct.Ean}, Name = {apiProduct.Name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Failed to update/insert product: Product EAN = {apiProduct.Ean}, Name = {apiProduct.Name}");
                        }

                        try
                        {
                            // 2. Upsert product description
                            var opisBuilder = new StringBuilder();

                            opisBuilder.Append("<h2>Opis produktu</h2>");

                            if (!string.IsNullOrWhiteSpace(apiProduct.Desc))
                                opisBuilder.Append($"<p>{apiProduct.Desc}</p>");

                            if (apiProduct.Attributes?.Any(a => !string.IsNullOrWhiteSpace(a)) == true)
                                opisBuilder.Append("<p><b>Parametry: </b>")
                                           .Append(string.Join(", ", apiProduct.Attributes.Where(a => !string.IsNullOrWhiteSpace(a))))
                                           .Append("</p>");

                            string nowyOpis = DescriptionHelper.TruncateHtml(opisBuilder.ToString(), 1000);

                            await _productRepo.UpdateProductDescriptionAsync(apiProduct.Ean, nowyOpis);
                            Log.Information($"Updated product description: Product EAN = {apiProduct.Ean}, Name = {apiProduct.Name}");
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Failed to update/insert product description: Product EAN = {apiProduct.Ean}, Name = {apiProduct.Name}");
                        }

                        try
                        {
                            // 3. Upsert images
                            if (apiProduct.Photos != null)
                            {
                                foreach (var img in apiProduct.Photos)
                                {
                                    if (string.IsNullOrEmpty(img.Url)) continue;

                                    byte[] imageData = await client.GetByteArrayAsync(img.Url);
                                    await _productRepo.UpsertProductImageAsync(apiProduct.Ean, img.Id.ToString() ?? "image", imageData);
                                    Log.Information($"Updated product image: Product EAN = {apiProduct.Ean}, Name = {apiProduct.Name}");
                                }
                            }
                        }
                        catch (HttpRequestException httpEx)
                        {
                            Log.Error(httpEx, $"Failed to download product image: Product EAN = {apiProduct.Ean}, Name = {apiProduct.Name}");
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Failed to update/insert product image: Product EAN = {apiProduct.Ean}, Name = {apiProduct.Name}");
                        }
                    }
                    Log.Information("Products imported: {Total} out of {ToUpdate}, Inserted: {Inserted}, Updated: {Updated}", productInserted + productUpdated, apiResponse.ProductList.Count(), productInserted, productUpdated);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error while fetching products");
                }
            }
        }
    }
}