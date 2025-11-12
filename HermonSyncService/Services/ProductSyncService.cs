using HermonSyncService.DTOs;
using HermonSyncService.Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;

namespace HermonSyncService.Services
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

        public async Task SyncToDatabaseAsync(List<FullProductDto> products)
        {
            int productInserted = 0;
            int productUpdated = 0;

            Log.Information("Attempting to update {Count} products in database.", products.Count);

            foreach (var product in products)
            {
                try
                {
                    decimal applicableMargin = MarginHelper.CalculateMargin(product.PriceNet, _defaultMargin, _marginRanges);

                    // 1. Upsert Product
                    var dto = new ProductDto
                    {
                        Id = 0,
                        Code = product.Code,
                        Ean = product.Ean,
                        Name = product.Name,
                        Quantity = product.Quantity,
                        NetBuyPrice = product.PriceNet,
                        GrossBuyPrice = product.PriceGross,
                        NetSellPrice = product.PriceNet * ((applicableMargin / 100m) + 1),
                        GrossSellPrice = product.PriceGross * ((applicableMargin / 100m) + 1),
                        Vat = product.Tax,
                        Weight = product.Weight,
                        Brand = product.Brand,
                        Unit = product.Unit,
                        IntegrationCompany = "HERMON"
                    };

                    int result = await _productRepo.UpsertProductAsync(dto);
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
                        opisBuilder.Append("<h2>Opis Produktu</h2>");
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
                        foreach (var image in product.Images)
                        {
                            try
                            {
                                await _productRepo.UpsertProductImageAsync(product.Code, image.Item1, image.Item2, true);
                                Log.Information("Updated image for {Code}", product.Code);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Failed to save image for {Code}", product.Code);
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