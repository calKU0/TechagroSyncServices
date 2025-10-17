using IntercarsSyncService.DTOs;
using IntercarsSyncService.Helpers;
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

namespace IntercarsSyncService.Services
{
    public class ProductSyncService
    {
        private readonly IProductRepository _productRepo;
        private readonly int _margin;
        private readonly HttpClient _imageClient = HttpClientHelper.CreateImageClient();

        public ProductSyncService(IProductRepository productRepo)
        {
            _productRepo = productRepo;
            _margin = AppSettingsLoader.GetMargin();
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
                    // 1. Upsert Product
                    var dto = new ProductDto
                    {
                        Id = 0,
                        Code = product.TowKod,
                        Ean = product.Barcodes.Split(',').FirstOrDefault(),
                        Name = product.ShortDescription ?? product.Description,
                        Quantity = product.TotalAvailability,
                        NetBuyPrice = product.WholesalePrice,
                        GrossBuyPrice = product.WholesalePrice * 1.23m,
                        NetSellPrice = product.WholesalePrice * ((_margin / 100m) + 1),
                        GrossSellPrice = product.WholesalePrice * 1.23m * ((_margin / 100m) + 1),
                        Vat = 23,
                        Weight = product.PackageWeight ?? 0,
                        Brand = product.Manufacturer,
                        Unit = "szt.",
                        IntegrationCompany = "Intercars"
                    };

                    int result = await _productRepo.UpsertProductAsync(dto);
                    if (result == 1)
                    {
                        productInserted++;
                        Log.Information("Inserted product {Code} - {Name}", product.TowKod, product.ShortDescription);
                    }
                    else if (result == 2)
                    {
                        productUpdated++;
                        Log.Information("Updated product {Code} - {Name}", product.TowKod, product.ShortDescription);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to upsert product {Code}", product.TowKod);
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
                    await _productRepo.UpdateProductDescriptionAsync(product.TowKod, truncatedDesc);

                    Log.Information("Updated description for product {Code}", product.TowKod);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to update product description {Code}", product.TowKod);
                }

                // 3. Update Images
                try
                {
                    if (product.Images != null && product.Images.Any())
                    {
                        foreach (var img in product.Images)
                        {
                            try
                            {
                                var imageData = await _imageClient.GetByteArrayAsync(img.ImageLink);
                                string imageId = img.TowKod + "_" + img.SortNr;
                                await _productRepo.UpsertProductImageAsync(product.TowKod, imageId, imageData);
                                Log.Information("Updated image for {Code}", product.TowKod);
                            }
                            catch (HttpRequestException httpEx)
                            {
                                Log.Error(httpEx, "Failed to download image for {Code}", product.TowKod);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to update images for {Code}", product.TowKod);
                }
            }

            Log.Information("Products imported: {Total}/{All}, Inserted: {Inserted}, Updated: {Updated}",
                productInserted + productUpdated, products.Count, productInserted, productUpdated);
        }
    }
}