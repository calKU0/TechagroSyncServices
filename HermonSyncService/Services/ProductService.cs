using HermonSyncService.DTOs;
using HermonSyncService.Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TechagroApiSync.Shared.DTOs;
using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;

namespace HermonSyncService.Services
{
    public class ProductService
    {
        private readonly HermonApiClient _apiClient;
        private readonly decimal _defaultMargin;
        private readonly List<MarginRange> _marginRanges;

        public ProductService()
        {
            var apiSettings = AppSettingsLoader.LoadApiSettings();
            _apiClient = new HermonApiClient(apiSettings);
            _defaultMargin = AppSettingsLoader.GetDefaultMargin();
            _marginRanges = AppSettingsLoader.GetMarginRanges();
        }

        public async Task<List<FtpProducts>> SyncProductsFromFtp()
        {
            var products = new List<FtpProducts>();

            Log.Information("Starting basic product synchronization from FTP...");
            var ftpSettings = AppSettingsLoader.LoadFtpSettings();
            using (HermonFtpClient ftpClient = new HermonFtpClient(ftpSettings))
            {
                products = ftpClient.DownloadCsv<FtpProducts>("HERMON_OFERTA.csv");
                if (!products.Any())
                {
                    Log.Warning("No products found in HERMON_OFERTA.csv");
                }
            }
            Log.Information("Basic product sync from FTP completed.");

            return products;
        }

        public List<FtpImage> SyncImagesFromFtp()
        {
            var images = new List<FtpImage>();

            Log.Information("Starting image synchronization...");
            var ftpSettings = AppSettingsLoader.LoadFtpSettings();
            using (HermonFtpClient ftpClient = new HermonFtpClient(ftpSettings))
            {
                images = ftpClient.DownloadImages("/ZDJECIA");
            }

            if (!images.Any())
            {
                Log.Warning("No images found in /ZDJECIA");
            }

            Log.Information("Image synchronization completed.");

            return images;
        }

        public async Task<List<ProductsDetailResponse>> FetchProductDetailsFromApi(IEnumerable<FtpProducts> products)
        {
            var allDetails = new List<ProductsDetailResponse>();

            Log.Information("Starting to fetch product details from API...");

            var token = await _apiClient.AuthenticateAsync();
            if (string.IsNullOrEmpty(token))
            {
                Log.Warning("Authentication failed. Aborting sync.");
                return null;
            }

            var productCodes = products
                .Select(p => p.Code)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            foreach (var chunk in Chunk(productCodes, 1000))
            {
                var response = await _apiClient.PostAsync("/client-service/articles", chunk);

                if (!response.IsSuccessStatusCode)
                {
                    Log.Error("Failed to fetch product details. Status: {Status}", response.StatusCode);
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(json))
                {
                    Log.Warning("API returned empty response for this chunk.");
                    continue;
                }

                List<ProductsDetailResponse> details;
                try
                {
                    details = JsonSerializer.Deserialize<List<ProductsDetailResponse>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                }
                catch (JsonException ex)
                {
                    Log.Error(ex, "Failed to deserialize API response: {Json}", json);
                    continue;
                }

                if (details != null)
                {
                    Log.Information("Fetched {Count} product details from API.", details.Count);
                    allDetails.AddRange(details.Where(x => x.Error == null));
                }
            }
            Log.Information("Product details fetch from API completed.");

            return allDetails;
        }

        private static IEnumerable<List<T>> Chunk<T>(IEnumerable<T> source, int size)
        {
            return source
                .Select((x, i) => new { x, i })
                .GroupBy(x => x.i / size)
                .Select(g => g.Select(x => x.x).ToList());
        }

        // ------------------------------
        // DTO Builder
        // ------------------------------

        public Task<List<ProductDto>> BuildProductDtos(IEnumerable<FtpProducts> ftpProducts, IEnumerable<ProductsDetailResponse> apiDetails, IEnumerable<FtpImage> ftpImages)
        {
            Log.Information("Building full product data...");

            // Index API details by product code
            var detailsByCode = apiDetails
                .Where(d => d.Id != null)
                .ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);

            // Group images by product code prefix
            var imagesByCode = ftpImages
                .GroupBy(img => GetProductCodeFromFileName(img.FileName))
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var result = new List<ProductDto>();

            foreach (var ftpProduct in ftpProducts)
            {
                ProductsDetailResponse detail;
                if (!detailsByCode.TryGetValue(ftpProduct.Code, out detail))
                    continue;

                decimal totalQuantity = 0;
                if (detail.BranchesAvailability != null)
                {
                    foreach (var branch in detail.BranchesAvailability)
                    {
                        totalQuantity += ParseQuantity(branch.Quantity);
                    }
                }

                List<FtpImage> imagesTemp;
                if (!imagesByCode.TryGetValue(ftpProduct.Code, out imagesTemp))
                {
                    imagesTemp = new List<FtpImage>();
                }

                decimal applicableMargin = MarginHelper.CalculateMargin(
                    detail.ClientPrice.NetPrice,
                    _defaultMargin,
                    _marginRanges);

                string code = ftpProduct.Code + "HR";
                string productCode = code.Length > 20 ? code.Substring(0, 20) : code;

                result.Add(new ProductDto
                {
                    Id = 0,
                    Code = productCode,
                    Ean = ftpProduct.Ean,
                    Name = ftpProduct.Name,
                    Quantity = totalQuantity,
                    NetBuyPrice = detail.ClientPrice.NetPrice,
                    GrossBuyPrice = detail.ClientPrice.GrossPrice,
                    NetSellPriceB = detail.ClientPrice.NetPrice * ((applicableMargin / 100m) + 1),
                    GrossSellPriceB = detail.ClientPrice.GrossPrice * ((applicableMargin / 100m) + 1),
                    Vat = detail.ClientPrice != null ? detail.ClientPrice.TaxRate : 0,
                    Weight = ftpProduct.Weight ?? 0,
                    Brand = detail.ProducerName,
                    Unit = ftpProduct.Unit,
                    IntegrationCompany = IntegrationCompany.HERMON,
                    Description = ftpProduct.Description,
                    Images = BuildProductImages(productCode, imagesTemp)
                });
            }

            return Task.FromResult(result);
        }

        private List<ImageDto> BuildProductImages(string productCode, List<FtpImage> images)
        {
            var result = new List<ImageDto>(images.Count);

            foreach (var img in images)
            {
                result.Add(new ImageDto
                {
                    Name = img.FileName,
                    Path = img.FilePath
                });
            }

            return result;
        }

        private static string GetProductCodeFromFileName(string fileName)
        {
            int underscoreIndex = fileName.IndexOf('_');
            if (underscoreIndex <= 0)
                return fileName;

            return fileName.Substring(0, underscoreIndex);
        }

        private static decimal ParseQuantity(string quantityStr)
        {
            if (string.IsNullOrWhiteSpace(quantityStr))
                return 0;

            var cleaned = new string(quantityStr.Where(c => char.IsDigit(c) || c == ',' || c == '.').ToArray());

            cleaned = cleaned.Replace(',', '.');

            return decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var qty)
                ? qty
                : 0;
        }
    }
}