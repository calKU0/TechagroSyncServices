using AgroramiSyncService.DTOs;
using AgroramiSyncService.Helpers;
using AgroramiSyncService.Settings;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechagroApiSync.Shared.DTOs;
using TechagroApiSync.Shared.Enums;
using TechagroApiSync.Shared.Helpers;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;

namespace AgroramiSyncService.Services
{
    public class ApiService
    {
        private readonly AgroramiApiSettings _apiSettings;
        private readonly GraphQLClient _client;
        private readonly decimal _defaultMargin;
        private readonly List<MarginRange> _marginRanges;

        public ApiService()
        {
            _apiSettings = AppSettingsLoader.LoadApiSettings();
            _defaultMargin = AppSettingsLoader.GetDefaultMargin();
            _marginRanges = AppSettingsLoader.GetMarginRanges();
            _client = new GraphQLClient(_apiSettings.BaseUrl);
        }

        public async Task<List<ProductDto>> SyncProducts()
        {
            try
            {
                Log.Information("Starting product synchronization...");

                // Step 1: Get Customer Token
                var token = await GetCustomerTokenAsync();
                var allProducts = new List<ProductsResponse>();
                int currentPage = 1;
                int maxRetries = 3;
                int currentTry = 0;
                int pageSize = 500;

                // Step 2: Download products with pagination
                while (true && currentTry <= maxRetries
                    )
                {
                    try
                    {
                        var pageProducts = await GetProductsAsync(token, currentPage, pageSize);

                        if (pageProducts == null || pageProducts.Count == 0)
                        {
                            Log.Information("No more products found (page {Page}).", currentPage);
                            break;
                        }

                        Log.Information("Fetched {Count} products from page {Page}.", pageProducts.Count, currentPage);
                        allProducts.AddRange(pageProducts);
                        currentTry = 0;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error fetching products on page {Page}", currentPage);
                        currentTry++;
                    }
                    finally
                    {
                        currentPage++;
                    }
                }

                if (allProducts.Count == 0)
                {
                    Log.Warning("No products found.");
                    return null;
                }

                // Step 3: Download attributes
                var attributes = await GetAttributeMetadataAsync(token);

                // Step 4: Aggregate and merge data
                Log.Information("Mapping attribute labels...");
                MapAttributeLabels(allProducts, attributes);

                var products = await BuildProductDtos(allProducts);
                Log.Information("Mapping completed. Fetched {Count} products.", allProducts.Count);

                return products;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during product synchronization");
                return null;
            }
        }

        private async Task<string> GetCustomerTokenAsync()
        {
            var query = $@"
                mutation {{
                    generateCustomerToken(
                        email: ""{_apiSettings.Login}"",
                        password: ""{_apiSettings.Password}""
                    ) {{
                        token
                    }}
                }}";

            var result = await _client.ExecuteAsync<Dictionary<string, CustomerTokenResponse>>(query);
            return result["generateCustomerToken"].Token;
        }

        private async Task<List<ProductsResponse>> GetProductsAsync(string token, int currentPage, int pageSize)
        {
            var query = $@"
                query Products {{
                    products(filter: {{ category_id: {{ eq: ""4"" }} }}, pageSize: {pageSize}, currentPage: {currentPage}) {{
                        items {{
                            id
                            sku
                            name
                            standard_external_shipping_price
                            express_external_shipping_price
                            catalog_number
                            description {{ html }}
                            short_description {{ html }}
                            manufacturer
                            unit
                            ... on SimpleProduct {{
                                weight
                            }}
                            categories {{
                                id
                                name
                                url_key
                                url_path
                                level
                                breadcrumbs {{
                                    category_id
                                    category_name
                                    category_url_path
                                }}
                            }}
                            stock_status
                            stock_availability {{
                                in_stock
                                in_stock_real
                            }}
                            media_gallery {{
                                url
                                label
                                position
                            }}
                            price_range {{
                                minimum_price {{
                                    individual_price {{
                                        net
                                        gross
                                        currency
                                    }}
                                }}
                            }}
                        }}
                    }}
                }}
            ";

            var result = await _client.ExecuteAsync<Dictionary<string, Dictionary<string, List<ProductsResponse>>>>(query, token);

            if (result.TryGetValue("products", out var productSection) && productSection.TryGetValue("items", out var products))
            {
                return products;
            }

            Log.Warning("Unexpected GraphQL structure or empty data on page {Page}.", currentPage);
            return new List<ProductsResponse>();
        }

        private async Task<List<AttributeMetadataResponse>> GetAttributeMetadataAsync(string token)
        {
            var query = @"
                query {
                    customAttributeMetadata(
                        attributes: [
                            { attribute_code: ""manufacturer"", entity_type: ""catalog_product"" }
                            { attribute_code: ""unit"", entity_type: ""catalog_product"" }
                        ]
                    ) {
                        items {
                            attribute_code
                            attribute_type
                            entity_type
                            input_type
                            attribute_options {
                                label
                                value
                            }
                        }
                    }
                }";

            var result = await _client.ExecuteAsync<Dictionary<string, Dictionary<string, List<AttributeMetadataResponse>>>>(query, token);

            if (result.TryGetValue("customAttributeMetadata", out var metadataSection) && metadataSection.TryGetValue("items", out var attributes))
            {
                return attributes;
            }

            return new List<AttributeMetadataResponse>();
        }

        private void MapAttributeLabels(List<ProductsResponse> products, List<AttributeMetadataResponse> metadata)
        {
            var map = metadata.ToDictionary(
                m => m.AttributeCode,
                m => m.AttributeOptions.ToDictionary(
                    o => int.TryParse(o.Value, out var intVal) ? intVal : -1,
                    o => o.Label
                )
            );

            foreach (var p in products)
            {
                // manufacturer
                if (p.Manufacturer != null && map.TryGetValue("manufacturer", out var manufDict))
                {
                    if (manufDict.TryGetValue(p.Manufacturer.Value, out var manufLabel))
                        p.ManufacturerLabel = manufLabel;
                }

                // unit
                if (p.Unit != null && map.TryGetValue("unit", out var unitDict))
                {
                    if (unitDict.TryGetValue(p.Unit.Value, out var unitLabel))
                        p.UnitLabel = unitLabel;
                }
            }
        }

        private async Task<List<ProductDto>> BuildProductDtos(List<ProductsResponse> products)
        {
            var result = new List<ProductDto>(products.Count);

            foreach (var product in products)
            {
                decimal applicableMargin = MarginHelper.CalculateMargin(product.PriceRange.MinimumPrice.IndividualPrice.Net, _defaultMargin, _marginRanges);
                string code = product.Sku + "AR";
                decimal marginFactor = (applicableMargin / 100m) + 1m;

                decimal? standardPrice = (product.StandardPrice.HasValue && product.StandardPrice.Value != 0m)
                    ? product.StandardPrice
                    : (decimal?)null;
                decimal? expressPrice = (product.ExpressPrice.HasValue && product.ExpressPrice.Value != 0m)
                    ? product.ExpressPrice
                    : (decimal?)null;

                decimal baseStandardPrice = standardPrice ?? product.PriceRange.MinimumPrice.IndividualPrice.Net;
                bool hasExpress = expressPrice.HasValue;

                decimal baseBuyPrice = hasExpress ? expressPrice.Value : baseStandardPrice;
                decimal netBuyPrice = baseBuyPrice;
                decimal grossBuyPrice = baseBuyPrice * 1.23m;

                decimal priceBBase = hasExpress ? expressPrice.Value : baseStandardPrice;
                decimal netSellPriceB = priceBBase * marginFactor;
                decimal grossSellPriceB = netSellPriceB * 1.23m;

                decimal? netSellPriceC = hasExpress ? (decimal?)(baseStandardPrice * marginFactor) : null;
                decimal? grossSellPriceC = netSellPriceC.HasValue ? netSellPriceC.Value * 1.23m : (decimal?)null;

                product.Sku = code.Length > 20 ? code.Substring(0, 20) : code;
                product.Name = product.Name + " " + product.CatalogNumber;

                var descriptionText = string.Empty;

                if (!string.IsNullOrWhiteSpace(product.CatalogNumber))
                {
                    descriptionText += ("<p><strong>Numer katalogowy: " + product.CatalogNumber + "</strong></p>");
                }

                if (!string.IsNullOrWhiteSpace(product.Description.Html))
                {
                    descriptionText += (product.Description.Html);
                }

                // 1. Upsert Product
                var dto = new ProductDto
                {
                    Id = product.Id,
                    Code = product.Sku,
                    Ean = product.Ean,
                    Name = product.Name,
                    Quantity = product.StockAvailability.InStock == 0 ? 0 : Convert.ToDecimal(product.StockAvailability.InStockReal.Replace("+", "")),
                    NetBuyPrice = netBuyPrice,
                    GrossBuyPrice = grossBuyPrice,
                    NetSellPriceB = netSellPriceB,
                    GrossSellPriceB = grossSellPriceB,
                    NetSellPriceC = netSellPriceC,
                    GrossSellPriceC = grossSellPriceC,
                    Vat = 23,
                    Weight = product.Weight ?? 0,
                    Brand = product.ManufacturerLabel,
                    Unit = UnitHelpers.MapUnitLabel(product.UnitLabel),
                    IntegrationCompany = IntegrationCompany.AGRORAMI,
                    Description = descriptionText,
                    Images = await BuildProductImagesAsync(product.Sku, product.MediaGallery),
                    CategoriesString = BuildLeafCategoriesString(product.Categories)
                };

                result.Add(dto);
            }

            return result;
        }

        private async Task<List<ImageDto>> BuildProductImagesAsync(string productCode, List<MediaGalleryItem> images)
        {
            var result = new List<ImageDto>(images.Count);

            foreach (var img in images)
            {
                result.Add(new ImageDto
                {
                    Name = $"{productCode}_{img.Position}",
                    Url = img.Url
                });
            }

            return result;
        }

        private static List<string> BuildFullCategoryPaths(List<Categories> categories)
        {
            var paths = new List<string>();

            foreach (var category in categories)
            {
                if (category.Level <= 1)
                    continue;

                var parts = new List<string>();

                if (category.BreadCrumbs != null)
                {
                    parts.AddRange(category.BreadCrumbs.Select(b => b.CategoryName));
                }

                parts.Add(category.Name);

                paths.Add(string.Join(" > ", parts));
            }

            return paths;
        }

        private static string BuildLeafCategoriesString(List<Categories> categories)
        {
            if (categories == null || categories.Count == 0)
                return string.Empty;

            var allPaths = BuildFullCategoryPaths(categories);

            var leafPaths = allPaths
                .Where(path =>
                    !allPaths.Any(other =>
                        other != path &&
                        other.StartsWith(path + " >")))
                .Distinct()
                .ToList();

            return string.Join("|", leafPaths);
        }
    }
}