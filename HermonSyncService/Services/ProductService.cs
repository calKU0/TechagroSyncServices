using HermonSyncService.DTOs;
using HermonSyncService.Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TechagroSyncServices.Shared.Repositories;

namespace HermonSyncService.Services
{
    public class ProductService
    {
        private readonly HermonApiClient _apiClient;

        public ProductService(IProductRepository productRepo)
        {
            var apiSettings = AppSettingsLoader.LoadApiSettings();
            _apiClient = new HermonApiClient(apiSettings);
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

        public List<FullProductDto> BuildFullProducts(IEnumerable<FtpProducts> ftpProducts, IEnumerable<ProductsDetailResponse> apiDetails, IEnumerable<FtpImage> ftpImages)
        {
            return ftpProducts
                .Select(ftpProduct =>
                {
                    var detail = apiDetails
                        .FirstOrDefault(d => d.Id?.Equals(ftpProduct.Code, StringComparison.OrdinalIgnoreCase) == true);

                    if (detail == null)
                        return null;

                    decimal totalQuantity = 0;
                    if (detail.BranchesAvailability != null && detail.BranchesAvailability.Any())
                    {
                        totalQuantity = detail.BranchesAvailability
                            .Select(b => ParseQuantity(b.Quantity))
                            .Sum();
                    }

                    var images = ftpImages
                        .Where(img => img.FileName.StartsWith($"{ftpProduct.Code}_"))
                        .ToList();

                    return new FullProductDto
                    {
                        Code = ftpProduct.Code,
                        Name = ftpProduct.Name,
                        Ean = ftpProduct.Ean,
                        Unit = ftpProduct.Unit,
                        CN = ftpProduct.CN,
                        Weight = ftpProduct.Weight,
                        Description = ftpProduct.Description,
                        Brand = detail?.ProducerName,
                        PriceNet = detail.ClientPrice.NetPrice,
                        PriceGross = detail.ClientPrice.GrossPrice,
                        Tax = detail?.ClientPrice?.TaxRate ?? 0,
                        Quantity = totalQuantity,
                        Images = images
                    };
                })
                .Where(p => p != null)
                .ToList();
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