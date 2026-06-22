using AgroramiSyncService.Constants;
using AgroramiSyncService.DTOs;
using AgroramiSyncService.Helpers;
using AgroramiSyncService.Settings;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechagroApiSync.Shared.DTOs;
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

                var token = await GetCustomerTokenAsync();
                var allProducts = new List<ProductsResponse>();

                DateTime start = new DateTime(2024, 1, 1);
                DateTime end = DateTime.UtcNow;

                TimeSpan windowSize = TimeSpan.FromDays(15);

                int pageSize = 750;
                int maxRetries = 3;

                while (start < end)
                {
                    DateTime windowEnd = start.Add(windowSize);
                    if (windowEnd > end)
                        windowEnd = end;

                    Log.Information("Processing window {Start} → {End}", start, windowEnd);

                    int currentPage = 1;

                    while (true)
                    {
                        try
                        {
                            var pageProducts = await GetProductsByDateRangeAsync(
                                token,
                                start,
                                windowEnd,
                                currentPage,
                                pageSize
                            );

                            if (pageProducts == null || pageProducts.Count == 0)
                                break;

                            Log.Information(
                                "Fetched {Count} products (window {Start}-{End}, page {Page})",
                                pageProducts.Count,
                                start,
                                windowEnd,
                                currentPage
                            );

                            allProducts.AddRange(pageProducts);

                            currentPage++;

                            await Task.Delay(1200);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex,
                                "Error in window {Start}-{End}, page {Page}",
                                start,
                                windowEnd,
                                currentPage);

                            maxRetries--;

                            if (maxRetries <= 0)
                            {
                                Log.Error("Skipping window {Start}-{End}", start, windowEnd);
                                break;
                            }

                            await Task.Delay(5000);
                        }
                    }

                    // move to next window
                    start = windowEnd.AddSeconds(1);
                    maxRetries = 3;
                }

                if (allProducts.Count == 0)
                {
                    Log.Warning("No products found.");
                    return null;
                }

                var attributes = await GetAttributeMetadataAsync(token);

                Log.Information("Mapping attribute labels...");
                MapAttributeLabels(allProducts, attributes);

                var products = await BuildProductDtos(allProducts);

                Log.Information("Mapping completed. Prepared {Count} products.", products.Count);

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

            var result = await _client.ExecuteWithRetryAsync<Dictionary<string, CustomerTokenResponse>>(query);
            return result["generateCustomerToken"].Token;
        }

        private async Task<List<ProductsResponse>> GetProductsByDateRangeAsync(string token, DateTime from, DateTime to, int currentPage, int pageSize)
        {
            var fromStr = from.ToString("yyyy-MM-dd HH:mm:ss");
            var toStr = to.ToString("yyyy-MM-dd HH:mm:ss");

            var query = $@"
            query Products {{
                products(
                    filter: {{
                        created_at: {{
                            from: ""{fromStr}"",
                            to: ""{toStr}""
                        }}
                        category_id: {{ eq: ""4"" }}
                    }},
                    pageSize: {pageSize},
                    currentPage: {currentPage}
                ) {{
                    items {{
                        id
                        sku
                        name
                        created_at
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
            }}";

            var result =
                await _client.ExecuteWithRetryAsync<Dictionary<string, Dictionary<string, List<ProductsResponse>>>>(query, token);

            if (result.TryGetValue("products", out var section) &&
                section.TryGetValue("items", out var products))
            {
                return products;
            }

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

            var result = await _client.ExecuteWithRetryAsync<Dictionary<string, Dictionary<string, List<AttributeMetadataResponse>>>>(query, token);

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
            var result = new List<ProductDto>();

            foreach (var product in products)
            {
                decimal? standardPrice = (product.StandardPrice.HasValue && product.StandardPrice.Value != 0m && product.StockAvailability.InStock == 0)
                    ? product.StandardPrice
                    : (decimal?)null;
                decimal? expressPrice = (product.ExpressPrice.HasValue && product.ExpressPrice.Value != 0m && product.StockAvailability.InStock == 0)
                    ? product.ExpressPrice
                    : (decimal?)null;

                decimal baseStandardPrice = standardPrice ?? product.PriceRange.MinimumPrice.IndividualPrice.Net;
                bool hasExpress = expressPrice.HasValue;
                hasExpress = hasExpress && expressPrice.Value > 0m;

                // Kod bazowy + przycięcie do 20 znaków
                var baseCodeRaw = product.Sku + "AR";
                var baseCode = baseCodeRaw.Length > 20 ? baseCodeRaw.Substring(0, 20) : baseCodeRaw;
                var expressCodeRaw = baseCodeRaw + "-EXP";
                var expressCode = expressCodeRaw.Length > 20 ? expressCodeRaw.Substring(0, 20) : expressCodeRaw;

                product.Sku = baseCode; // zachowujemy dotychczasowe użycie dla obrazów
                product.Name = product.Name + " " + product.CatalogNumber;

                var descriptionText = string.Empty;

                if (!string.IsNullOrWhiteSpace(product.CatalogNumber))
                {
                    descriptionText += ("<p><b>Numer katalogowy: " + product.CatalogNumber + "</b></p>");
                }

                if (!string.IsNullOrWhiteSpace(product.Description.Html))
                {
                    descriptionText += (product.Description.Html);
                }

                async Task AddDtoAsync(string codeValue, decimal buyPrice)
                {
                    decimal applicableMargin = MarginHelper.CalculateMargin(buyPrice, _defaultMargin, _marginRanges);
                    decimal marginFactor = (applicableMargin / 100m) + 1m;

                    var netBuyPrice = buyPrice;
                    var grossBuyPrice = buyPrice * 1.23m;

                    var netSellPriceB = buyPrice * marginFactor;
                    var grossSellPriceB = netSellPriceB * 1.23m;

                    var dto = new ProductDto
                    {
                        Id = product.Id,
                        Code = codeValue,
                        Ean = product.Ean,
                        Name = product.Name,
                        Quantity = product.StockAvailability.InStock == 0 ? 0 : Convert.ToDecimal(product.StockAvailability.InStockReal.Replace("+", "")),
                        NetBuyPrice = netBuyPrice,
                        GrossBuyPrice = grossBuyPrice,
                        NetSellPriceB = netSellPriceB,
                        GrossSellPriceB = grossSellPriceB,
                        NetSellPriceC = null,
                        GrossSellPriceC = null,
                        Vat = 23,
                        Weight = product.Weight ?? 0,
                        Brand = product.ManufacturerLabel,
                        Unit = UnitHelpers.MapUnitLabel(product.UnitLabel),
                        IntegrationCompany = ServiceConstants.Company,
                        Description = descriptionText,
                        Images = await BuildProductImagesAsync(codeValue, product.MediaGallery),
                        CategoriesString = BuildLeafCategoriesString(product.Categories)
                    };

                    result.Add(dto);
                }

                // Standard DTO
                await AddDtoAsync(baseCode, baseStandardPrice);

                // Express DTO (tylko gdy obie ceny są dostępne)
                if (hasExpress && standardPrice.HasValue)
                {
                    await AddDtoAsync(expressCode, expressPrice.Value);
                }
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