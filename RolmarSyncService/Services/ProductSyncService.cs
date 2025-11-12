using RolmarSyncService.DTOs;
using RolmarSyncService.Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
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
                        IntegrationCompany = "ROLMAR"
                    };

                    int result = await _productRepo.UpsertProductAsync(dto);
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
                    await _productRepo.UpdateProductDescriptionAsync(product.ProductIndex, truncatedDesc);

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
                        using (HttpClient imageClient = new HttpClient())
                        {
                            int imageIndex = 1;

                            foreach (var img in product.Images)
                            {
                                try
                                {
                                    if (string.IsNullOrEmpty(img.Url))
                                        continue;

                                    byte[] imageData = await imageClient.GetByteArrayAsync(img.Url);
                                    string imageName = $"{product.ProductIndex}_{imageIndex}";

                                    // Save image (upload to DB, storage, etc.)
                                    await _productRepo.UpsertProductImageAsync(product.ProductIndex, imageName, imageData, true);

                                    Log.Information("Updated product image for {Index} from URL: {Url} ", product.ProductIndex, img.Url);

                                    imageIndex++;
                                }
                                catch (HttpRequestException httpEx)
                                {
                                    Log.Error(httpEx, "Failed to download image for {Index} from URL: {Url}", product.ProductIndex, img.Url);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Failed to save image for {Index} from URL: {Url}", product.ProductIndex, img.Url);
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