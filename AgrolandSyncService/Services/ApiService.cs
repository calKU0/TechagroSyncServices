using AgrolandSyncService.DTOs;
using AgrolandSyncService.Helpers;
using AgrolandSyncService.Settings;
using AgrolandSyncService.Constants;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TechagroApiSync.Shared.DTOs;
using TechagroApiSync.Shared.Enums;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;

namespace AgrolandSyncService.Services
{
    public class ApiService
    {
        private readonly AgrolandApiSettings _apiSettings;
        private readonly IProductSyncService _productSyncService;
        private readonly decimal _defaultMargin;
        private readonly List<MarginRange> _marginRanges;

        public ApiService(IProductSyncService productSyncService)
        {
            _productSyncService = productSyncService;
            _apiSettings = AppSettingsLoader.LoadApiSettings();
            _defaultMargin = CommonSettingsLoader.GetDefaultMargin();
            _marginRanges = CommonSettingsLoader.GetMarginRanges();
        }

        // Returns codes of all products returned by API, incomplete when the request failed
        public async Task<FetchResult<string>> SyncProducts()
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var url = $"{_apiSettings.BaseUrl}1/3/utf8/{_apiSettings.ApiKey}?stream=true";
                    Log.Information($"Sending request to {url}.");
                    var response = await client.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        Log.Error($"API error while fetching products: {response.StatusCode}");
                        return FetchResult<string>.Incomplete(new List<string>());
                    }
                    var xml = await response.Content.ReadAsStringAsync();

                    Products apiResponse;
                    var serializer = new XmlSerializer(typeof(Products));
                    using (var reader = new StringReader(xml))
                    {
                        apiResponse = (Products)serializer.Deserialize(reader);
                    }

                    Log.Information($"Got response with {apiResponse.ProductList.Count()} products.");
                    var products = new List<ProductDto>();
                    foreach (var apiProduct in apiResponse.ProductList)
                    {
                        try
                        {
                            decimal applicableMargin = MarginHelper.CalculateMargin(apiProduct.PriceAfterDiscountNet, _defaultMargin, _marginRanges);
                            var dto = new ProductDto
                            {
                                Id = apiProduct.Id,
                                Code = apiProduct.Ean,
                                Ean = apiProduct.Ean,
                                Name = apiProduct.Name,
                                Quantity = apiProduct.Qty,
                                NetBuyPrice = apiProduct.PriceAfterDiscountNet,
                                GrossBuyPrice = (apiProduct.PriceAfterDiscountNet * 1.23m),
                                NetSellPriceB = MarginHelper.ApplyMargin(apiProduct.PriceAfterDiscountNet, applicableMargin),
                                GrossSellPriceB = MarginHelper.ApplyMargin(apiProduct.PriceAfterDiscountNet * 1.23m, applicableMargin),
                                Vat = apiProduct.Vat,
                                Weight = apiProduct.Weight ?? 0,
                                Brand = apiProduct.Brand?.Name,
                                Unit = apiProduct.Unit,
                                Description = BuildDescription(apiProduct),
                                Images = apiProduct.Photos?
                                    .Where(img => !string.IsNullOrEmpty(img.Url))
                                    .Select(img => new ImageDto { Name = img.Id.ToString(), Url = img.Url })
                                    .ToList() ?? new List<ImageDto>(),
                                IntegrationCompany = ServiceConstants.Company
                            };

                            products.Add(dto);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Failed to update/insert product: Product EAN = {apiProduct.Ean}, Name = {apiProduct.Name}");
                        }
                    }

                    await _productSyncService.SyncToDatabaseAsync(products);

                    var codes = apiResponse.ProductList.Select(p => p.Ean).Where(code => !string.IsNullOrWhiteSpace(code)).ToList();
                    return FetchResult<string>.Complete(codes);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error while fetching products");
                    return FetchResult<string>.Incomplete(new List<string>());
                }
            }
        }

        // Description without header - ProductSyncService adds it. Null when supplier has no description.
        private static string BuildDescription(Product apiProduct)
        {
            var opisBuilder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(apiProduct.Desc))
                opisBuilder.Append($"<p>{apiProduct.Desc}</p>");

            if (apiProduct.Attributes?.Any(a => !string.IsNullOrWhiteSpace(a)) == true)
                opisBuilder.Append("<p><b>Parametry: </b>")
                           .Append(string.Join(", ", apiProduct.Attributes.Where(a => !string.IsNullOrWhiteSpace(a))))
                           .Append("</p>");

            return opisBuilder.Length > 0 ? opisBuilder.ToString() : null;
        }
    }
}