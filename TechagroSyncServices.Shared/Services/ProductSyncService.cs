using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;

namespace TechagroApiSync.Shared.Services
{
    public class ProductSyncService : IProductSyncService
    {
        private readonly IProductRepository _productRepo;
        private readonly HttpClient _imageClient;

        public ProductSyncService(IProductRepository productRepo, HttpClient imageClient = null)
        {
            _productRepo = productRepo;
            _imageClient = imageClient ?? new HttpClient();
        }

        public async Task SyncToDatabaseAsync(List<ProductDto> products)
        {
            int productInserted = 0;
            int productUpdated = 0;

            Log.Information("Attempting to update {Count} products in database.", products.Count);

            foreach (var product in products)
            {
                try
                {
                    int result = await _productRepo.UpsertProductAsync(product);
                    if (result == 1)
                    {
                        productInserted++;
                        Log.Information("Inserted product {Code} - {Name}", product.Code, product.Name);
                    }
                    else if (result == 2)
                    {
                        productUpdated++;
                        Log.Information("Updated product {Code} - {Name}", product.Code, product.Name);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to upsert product {Code}", product.Code);
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
                    await _productRepo.UpdateProductDescriptionAsync(product.Code, truncatedDesc);

                    Log.Information("Updated description for product {Code}", product.Code);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to update product description {Code}", product.Code);
                }

                // 3. Update Images
                try
                {
                    if (product.Images != null && product.Images.Any())
                    {
                        foreach (var img in product.Images)
                        {
                            // Ensure we have image bytes
                            if (img.Data == null || img.Data.Length == 0)
                            {
                                if (!string.IsNullOrEmpty(img.Url))
                                {
                                    try
                                    {
                                        img.Data = await _imageClient.GetByteArrayAsync(img.Url);
                                        Log.Information("Downloaded image for {Code} from {Url}", product.Code, img.Url);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Warning(ex, "Failed to download image for {Code} from {Url}", product.Code, img.Url);
                                    }
                                }
                            }

                            // Upsert image if we have data
                            if (img.Data != null && img.Data.Length > 0)
                            {
                                try
                                {
                                    await _productRepo.UpsertProductImageAsync(product.Code, img.Name, img.Data);
                                    Log.Information("Updated image for {Code}", product.Code);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Failed to update image for {Code} from Url: {Url}", product.Code, img.Url);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to update images for {Code}", product.Code);
                }
            }

            Log.Information("Products imported: {Total}/{All}, Inserted: {Inserted}, Updated: {Updated}", productInserted + productUpdated, products.Count, productInserted, productUpdated);
        }

        public async Task DeleteNotSyncedProducts(List<string> productCodes, IntegrationCompany integrationCompany)
        {
            try
            {
                Log.Information("Attempting to delete products that are not in the import list. This takes a while...");
                int result = await _productRepo.DeleteNotSyncedProducts(integrationCompany, productCodes);
                Log.Information("Deleted {Count} not synced products from database.", result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete not synced products from database.");
            }
        }
    }
}