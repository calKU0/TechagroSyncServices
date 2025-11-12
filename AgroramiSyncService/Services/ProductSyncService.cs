using AgroramiSyncService.DTOs;
using AgroramiSyncService.Helpers;
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

namespace AgroramiSyncService.Services
{
    public class ProductSyncService
    {
        private readonly IProductRepository _productRepo;
        private readonly decimal _defaultMargin;
        private readonly List<MarginRange> _marginRanges;

        public ProductSyncService(IProductRepository productRepo)
        {
            _productRepo = productRepo;
            _defaultMargin = AppSettingsLoader.GetDefaultMargin();
            _marginRanges = AppSettingsLoader.GetMarginRanges();
        }

        public async Task SyncToDatabaseAsync(List<ProductsResponse> products)
        {
            int productInserted = 0;
            int productUpdated = 0;

            Log.Information("Attempting to update {Count} products in database.", products.Count);

            foreach (var product in products)
            {
                try
                {
                    decimal applicableMargin = MarginHelper.CalculateMargin(product.PriceRange.MinimumPrice.IndividualPrice.Net, _defaultMargin, _marginRanges);
                    product.Sku = product.Sku + "AR";
                    product.Name = product.Name + " " + product.CatalogNumber;

                    // 1. Upsert Product
                    var dto = new ProductDto
                    {
                        Id = product.Id,
                        Code = product.Sku,
                        Ean = product.Ean,
                        Name = product.Name,
                        Quantity = product.StockAvailability.InStock == 0 ? 0 : Convert.ToDecimal(product.StockAvailability.InStockReal.Replace("+", "")),
                        NetBuyPrice = product.PriceRange.MinimumPrice.IndividualPrice.Net,
                        GrossBuyPrice = product.PriceRange.MinimumPrice.IndividualPrice.Gross,
                        NetSellPrice = product.PriceRange.MinimumPrice.IndividualPrice.Net * ((applicableMargin / 100m) + 1),
                        GrossSellPrice = product.PriceRange.MinimumPrice.IndividualPrice.Gross * ((applicableMargin / 100m) + 1),
                        Vat = 23,
                        Weight = product.Weight ?? 0,
                        Brand = product.ManufacturerLabel,
                        Unit = MapUnitLabel(product.UnitLabel),
                        IntegrationCompany = "AGRORAMI"
                    };

                    int result = await _productRepo.UpsertProductAsync(dto);
                    if (result == 1)
                    {
                        productInserted++;
                        Log.Information("Inserted product {Code} - {Name}", product.Sku, product.Name);
                    }
                    else if (result == 2)
                    {
                        productUpdated++;
                        Log.Information("Updated product {Code} - {Name}", product.Sku, product.Name);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to upsert product {Code}", product.Sku);
                }

                // 2. Update Description
                try
                {
                    var opisBuilder = new StringBuilder();

                    if (!string.IsNullOrWhiteSpace(product.CatalogNumber) || !string.IsNullOrWhiteSpace(product.Description.Html))
                    {
                        opisBuilder.Append("<h2>Opis Produktu</h2>");
                    }

                    if (!string.IsNullOrWhiteSpace(product.CatalogNumber))
                    {
                        opisBuilder.Append("<p><strong>Numer katalogowy: " + product.CatalogNumber + "</strong></p>");
                    }

                    if (!string.IsNullOrWhiteSpace(product.Description.Html))
                    {
                        opisBuilder.Append(product.Description.Html);
                    }

                    string truncatedDesc = DescriptionHelper.TruncateHtml(opisBuilder.ToString(), 1000);
                    await _productRepo.UpdateProductDescriptionAsync(product.Sku, truncatedDesc);

                    Log.Information("Updated description for product {Code}", product.Sku);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to update product description {Code}", product.Sku);
                }

                // 3. Update Images
                try
                {
                    if (product.MediaGallery != null && product.MediaGallery.Any())
                    {
                        using (HttpClient imageClient = new HttpClient())
                        {
                            foreach (var img in product.MediaGallery)
                            {
                                try
                                {
                                    var uri = new Uri(img.Url);
                                    var imageData = await imageClient.GetByteArrayAsync(img.Url);
                                    await _productRepo.UpsertProductImageAsync(product.Sku, product.Sku + "_" + img.Position, imageData);
                                    Log.Information("Updated image for {Code} from Url: {Url}", product.Sku, img.Url);
                                }
                                catch (HttpRequestException httpEx)
                                {
                                    Log.Error(httpEx, "Failed to download image for {Code} from Url: {Url}", product.Sku, img.Url);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Failed to save image for {Code} from Url: {Url}", product.Sku, img.Url);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to update images for {Code}", product.Sku);
                }
            }

            Log.Information("Products imported: {Total}/{All}, Inserted: {Inserted}, Updated: {Updated}", productInserted + productUpdated, products.Count, productInserted, productUpdated);
        }

        private string MapUnitLabel(string unitLabel)
        {
            if (string.IsNullOrWhiteSpace(unitLabel))
                return "szt.";
            unitLabel = unitLabel.ToLower();

            switch (unitLabel)
            {
                case "piece":
                case "sztuka":
                    return "szt.";

                case "kilogram":
                case "kg":
                    return "kg";

                case "gram":
                case "g":
                    return "g";

                case "litr":
                case "litre":
                case "l":
                    return "litr";

                case "meter":
                case "mb":
                case "m":
                    return "mb";

                case "pack":
                case "package":
                case "opakowanie":
                case "paczka":
                    return "opak.";

                case "komplet":
                    return "kpl.";

                default:
                    return "szt.";
            }
        }
    }
}