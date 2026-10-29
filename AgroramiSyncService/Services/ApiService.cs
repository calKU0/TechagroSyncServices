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
            _defaultMargin = CommonSettingsLoader.GetDefaultMargin();
            _marginRanges = CommonSettingsLoader.GetMarginRanges();
            _client = new GraphQLClient(_apiSettings.BaseUrl);
        }

        // Products from import list, fetched by SKU - a run takes minutes instead of scanning the whole offer (over 600k products).
        // Result is incomplete when any batch failed. SKUs not returned in a successful batch are no longer offered.
        public async Task<FetchResult<ProductDto>> FetchProductsBySkusAsync(IReadOnlyList<string> skus)
        {
            try
            {
                Log.Information("Fetching {Count} products from import list...", skus.Count);

                var token = await GetCustomerTokenAsync();
                var allProducts = new List<ProductsResponse>();
                bool isComplete = true;

                for (int i = 0; i < skus.Count; i += ServiceConstants.SkuBatchSize)
                {
                    var batch = skus.Skip(i).Take(ServiceConstants.SkuBatchSize).ToList();
                    var filter = $"sku: {{ in: {System.Text.Json.JsonSerializer.Serialize(batch)} }}";

                    try
                    {
                        var batchProducts = (await GetProductsAsync(token, filter, 1, batch.Count)).Items;
                        allProducts.AddRange(batchProducts);

                        Log.Information("Fetched {Count}/{Requested} products (batch {Batch})", batchProducts.Count, batch.Count, i / ServiceConstants.SkuBatchSize + 1);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error while fetching products batch {Batch}. Products from this batch are skipped.", i / ServiceConstants.SkuBatchSize + 1);
                        isComplete = false;
                    }

                    await Task.Delay(ServiceConstants.RequestDelayMs);
                }

                var products = await MapProductsAsync(token, allProducts);
                return isComplete ? FetchResult<ProductDto>.Complete(products) : FetchResult<ProductDto>.Incomplete(products);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during product synchronization");
                return FetchResult<ProductDto>.Incomplete(new List<ProductDto>());
            }
        }

        // Products added to the offer since given time - used to report new products.
        // Result is incomplete when any page failed, so no new product is missed.
        public async Task<FetchResult<ProductDto>> FetchProductsCreatedSinceAsync(DateTime fromUtc, DateTime toUtc)
        {
            try
            {
                Log.Information("Fetching products created between {From} and {To} (UTC)...", fromUtc, toUtc);

                var token = await GetCustomerTokenAsync();
                var allProducts = new List<ProductsResponse>();
                var filter = $@"created_at: {{ from: ""{fromUtc:yyyy-MM-dd HH:mm:ss}"", to: ""{toUtc:yyyy-MM-dd HH:mm:ss}"" }}";

                // Pages are counted from total_count. API returns at most 300 000 products of a query - pages after that
                // are empty without any error, so the result is checked against total_count.
                int totalCount = 0;
                for (int page = 1; ; page++)
                {
                    var productsPage = await GetProductsAsync(token, filter, page, ServiceConstants.PageSize);
                    totalCount = productsPage.TotalCount;
                    if (productsPage.Items.Count == 0)
                        break;

                    allProducts.AddRange(productsPage.Items);
                    Log.Information("Fetched {Count} created products (page {Page}, {Total} in total)", productsPage.Items.Count, page, productsPage.TotalCount);

                    if (page * ServiceConstants.PageSize >= productsPage.TotalCount)
                        break;

                    await Task.Delay(ServiceConstants.RequestDelayMs);
                }

                if (allProducts.Count < totalCount)
                {
                    Log.Warning("API returned {Count} of {Total} created products. New products will be checked again in next run.", allProducts.Count, totalCount);
                    return FetchResult<ProductDto>.Incomplete(new List<ProductDto>());
                }

                return FetchResult<ProductDto>.Complete(await MapProductsAsync(token, allProducts));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while fetching created products");
                return FetchResult<ProductDto>.Incomplete(new List<ProductDto>());
            }
        }

        private async Task<List<ProductDto>> MapProductsAsync(string token, List<ProductsResponse> apiProducts)
        {
            if (!apiProducts.Any())
                return new List<ProductDto>();

            var attributes = await GetAttributeMetadataAsync(token);

            Log.Information("Mapping attribute labels...");
            MapAttributeLabels(apiProducts, attributes);

            var products = await BuildProductDtos(apiProducts);

            Log.Information("Mapping completed. Prepared {Count} products.", products.Count);
            return products;
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

        // filter - GraphQL product filter, always limited to the spare parts category
        private async Task<ProductsPage> GetProductsAsync(string token, string filter, int currentPage, int pageSize)
        {

            var query = $@"
            query Products {{
                products(
                    filter: {{
                        {filter}
                        category_id: {{ eq: ""4"" }}
                    }},
                    pageSize: {pageSize},
                    currentPage: {currentPage}
                ) {{
                    total_count
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

            var result = await _client.ExecuteWithRetryAsync<Dictionary<string, ProductsPage>>(query, token);

            // Response without products (e.g. GraphQL error) is an error, not an empty page - otherwise products would look removed
            if (result == null || !result.TryGetValue("products", out var productsPage) || productsPage == null)
                throw new InvalidOperationException("API response does not contain products.");

            return productsPage;
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

                // Kod bazowy z zarostkiem + przycięcie do 20 znaków
                var baseCodeRaw = ServiceConstants.CodeSuffix.Add(product.Sku);
                var baseCode = ProductCodeAffix.Truncate(baseCodeRaw);
                var expressCode = ProductCodeAffix.Truncate(baseCodeRaw + ServiceConstants.ExpressSuffix);

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