using AgroramiSyncService.DTOs;
using AgroramiSyncService.Helpers;
using AgroramiSyncService.Settings;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Repositories;

namespace AgroramiSyncService.Services
{
    public class ApiService
    {
        private readonly AgroramiApiSettings _apiSettings;
        private readonly IProductRepository _productRepo;
        private readonly GraphQLClient _client;

        public ApiService(IProductRepository productRepo)
        {
            _productRepo = productRepo;
            _apiSettings = AppSettingsLoader.LoadApiSettings();
            _client = new GraphQLClient(_apiSettings.BaseUrl);
        }

        public async Task SyncProducts()
        {
            try
            {
                Log.Information("Starting product synchronization...");

                var token = await GetCustomerTokenAsync();
                var allProducts = new List<ProductsResponse>();
                int currentPage = 1;

                while (currentPage == 1)
                {
                    var pageProducts = await GetProductsAsync(token, currentPage);

                    if (pageProducts == null || pageProducts.Count == 0)
                    {
                        Log.Information("No more products found (page {Page}).", currentPage);
                        break;
                    }

                    Log.Information("Fetched {Count} products from page {Page}.", pageProducts.Count, currentPage);
                    allProducts.AddRange(pageProducts);

                    currentPage++;
                }

                if (allProducts.Count == 0)
                {
                    Log.Warning("No products found.");
                    return;
                }

                var attributes = await GetAttributeMetadataAsync(token);

                Log.Information("Mapping attribute labels...");
                MapAttributeLabels(allProducts, attributes);

                Log.Information("Mapping completed. Syncing {Count} products to database...", allProducts.Count);
                var productSync = new ProductSyncService(_productRepo);
                await productSync.SyncToDatabaseAsync(allProducts);

                Log.Information("Product synchronization completed successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during product synchronization");
            }
        }

        private async Task<string> GetCustomerTokenAsync()
        {
            var query = @"
                mutation {
                    generateCustomerToken(email: ""techagro@poczta.internetdsl.pl"", password: ""19750723SmSi@"") {
                        token
                    }
                }";

            var result = await _client.ExecuteAsync<Dictionary<string, CustomerTokenResponse>>(query);
            return result["generateCustomerToken"].Token;
        }

        private async Task<List<ProductsResponse>> GetProductsAsync(string token, int currentPage)
        {
            var query = $@"
                query Products {{
                    products(filter: {{ category_id: {{ eq: ""4"" }} }}, pageSize: 1000, currentPage: {currentPage}) {{
                        items {{
                            id
                            sku
                            name
                            description {{ html }}
                            short_description {{ html }}
                            manufacturer
                            unit
                            ... on SimpleProduct {{
                                weight
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
    }
}