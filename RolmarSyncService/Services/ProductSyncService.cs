using RolmarSyncService.DTOs;
using RolmarSyncService.Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;

namespace RolmarSyncService.Services
{
    public class ProductSyncService
    {
        private readonly IProductRepository _productRepo;
        private readonly decimal _defaulyMargin;
        private readonly List<MarginRange> _marginRanges;

        public ProductSyncService(IProductRepository productRepo)
        {
            _productRepo = productRepo;
            _defaulyMargin = AppSettingsLoader.GetDefaultMargin();
            _marginRanges = AppSettingsLoader.GetMarginRanges();
        }

        public async Task SyncToDatabaseAsync(List<FullProductDto> products)
        {
            int productInserted = 0;
            int productUpdated = 0;

            Log.Information("Attempting to update {Count} products in database.", products.Count);

            foreach (var product in products)
            {
                try
                {
                    decimal applicableMargin = MarginHelper.CalculateMargin(product.Price, _defaulyMargin, _marginRanges);

                    // 1. Upsert Product
                    var dto = new ProductDto
                    {
                        Id = 0,
                        Code = product.ProductIndex,
                        Ean = product.Ean,
                        Name = product.Name,
                        Quantity = product.Stock,
                        NetBuyPrice = product.Price,
                        GrossBuyPrice = product.Price * 1.23m,
                        NetSellPrice = product.Price * ((applicableMargin / 100m) + 1),
                        GrossSellPrice = product.Price * 1.23m * ((applicableMargin / 100m) + 1),
                        Vat = 23,
                        Weight = product.Weight,
                        Brand = product.Brand,
                        Unit = "szt.",
                        IntegrationCompany = "Intercars"
                    };
                    int result = 1;
                    //int result = await _productRepo.UpsertProductAsync(dto);
                    if (result == 1)
                    {
                        productInserted++;
                        Log.Information("Inserted product {Code} - {Name}", product.ProductIndex, product.Name);
                    }
                    else if (result == 2)
                    {
                        productUpdated++;
                        Log.Information("Updated product {Code} - {Name}", product.ProductIndex, product.Name);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to upsert product {Code}", product.ProductIndex);
                }

                // 2. Update Description
                try
                {
                    var opisBuilder = new StringBuilder();

                    if (!string.IsNullOrWhiteSpace(product.Description))
                    {
                        opisBuilder.Append("<h2>Opis produktu</h2>");
                        opisBuilder.Append(product.Description);
                    }

                    string truncatedDesc = DescriptionHelper.TruncateHtml(opisBuilder.ToString(), 1000);
                    //await _productRepo.UpdateProductDescriptionAsync(product.ProductIndex, truncatedDesc);

                    Log.Information("Updated description for product {Code}", product.ProductIndex);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to update product description {Code}", product.ProductIndex);
                }

                // 3. Update Images
                try
                {
                    if (product.Images != null && product.Images.Any())
                    {
                        using (HttpClient _imageClient = new HttpClient())
                        {
                            foreach (var img in product.Images)
                            {
                                try
                                {
                                    // Get the file name from the URL (remove query)
                                    var uri = new Uri(img.Url);
                                    string fileName = Path.GetFileName(uri.AbsolutePath); // DT10-0-75-15-3.jpg

                                    // Optional: replace invalid filename chars in local file
                                    foreach (var c in Path.GetInvalidFileNameChars())
                                        fileName = fileName.Replace(c, '_');

                                    string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
                                    Directory.CreateDirectory(folderPath);
                                    string filePath = Path.Combine(folderPath, fileName);

                                    var imageData = await _imageClient.GetByteArrayAsync(img.Url);
                                    File.WriteAllBytes(filePath, imageData);

                                    Log.Information("Downloaded and saved image for {Code} at {Path}", product.ProductIndex, filePath);
                                }
                                catch (HttpRequestException httpEx)
                                {
                                    Log.Error(httpEx, "Failed to download image for {Code} from Url: {Url}", product.ProductIndex, img.Url);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Failed to save image for {Code} from Url: {Url}", product.ProductIndex, img.Url);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to update images for {Code}", product.ProductIndex);
                }
            }

            Log.Information("Products imported: {Total}/{All}, Inserted: {Inserted}, Updated: {Updated}",
                productInserted + productUpdated, products.Count, productInserted, productUpdated);
        }
    }
}