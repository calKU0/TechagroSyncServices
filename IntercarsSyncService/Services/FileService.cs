using HtmlAgilityPack;
using IntercarsSyncService.DTOs;
using IntercarsSyncService.Helpers;
using IntercarsSyncService.Settings;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using TechagroApiSync.Shared.DTOs;
using TechagroApiSync.Shared.Enums;
using TechagroApiSync.Shared.Helpers;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Services;

namespace IntercarsSyncService.Services
{
    public class FileService
    {
        private readonly IntercarsApiSettings _apiSettings;
        private readonly IEmailService _emailService;
        private readonly IProductSyncService _productSyncService;
        private readonly HttpClient _httpClient;
        private readonly decimal _defaultMargin;
        private readonly List<MarginRange> _marginRanges;

        public FileService(IProductSyncService productSyncService, IEmailService emailService, HttpClient httpClient)
        {
            _productSyncService = productSyncService;
            _emailService = emailService;
            _httpClient = httpClient;
            _apiSettings = AppSettingsLoader.LoadApiSettings();
            _defaultMargin = AppSettingsLoader.GetDefaultMargin();
            _marginRanges = AppSettingsLoader.GetMarginRanges();
        }

        public async Task SyncProducts()
        {
            try
            {
                Log.Information("Starting products synchronization...");

                // Step 1: Download latest data files
                var products = await GetLatestProductInformationAsync();
                if (!products.Any())
                {
                    Log.Warning("No products found. Aborting sync.");
                    return;
                }

                // Step 2: Download stock and price data
                var stockData = await GetLatestStockPriceAsync();
                if (!stockData.Any())
                {
                    Log.Warning("No stock data found. Aborting sync.");
                    return;
                }

                // Step 3: Download product images
                var images = await GetLatestProductImagesAsync();
                if (!images.Any())
                {
                    Log.Warning("No images found. Aborting sync.");
                    return;
                }

                // Step 4: Aggregate and merge data
                var aggregatedStock = AggregateStockData(stockData);
                var fullProducts = await BuildProductDtosAsync(products, aggregatedStock, images);
                Log.Information("Merged {Count} products", fullProducts.Count);

                // Step 5.1: Detect newly added products
                var snapshotPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Export", $"products.json");
                var newProducts = await SnapshotChangeDetector.DetectNewAsync(snapshotPath, fullProducts, p => p.Code);

                if (newProducts.Any())
                {
                    var to = AppSettingsLoader.GetEmailsToNotify();

                    await BatchEmailNotifier.SendAsync(
                        newProducts,
                        100,
                        batch => $"Nowe produkty ({newProducts.Count})",
                        batch => HtmlHelper.BuildNewProductsEmailHtml(batch, "Inter Cars"),
                        recipients: to,
                        from: "Intercars Sync Service",
                        emailService: _emailService);
                }
                else
                {
                    Log.Information("No new products detected.");
                }

                // Step 6: Export to JSON
                await SnapshotChangeDetector.SaveSnapshotAsync(snapshotPath, fullProducts);
                Log.Information("JSON file created at {Path}", snapshotPath);

                // Step 7: Filter by import list
                var importFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Import", "numery_katalogowe.txt");

                var allowedCodes = FileUtils.ReadImportList(importFilePath);

                if (!allowedCodes.Any())
                {
                    Log.Warning("Import file is empty or missing. Aborting import");
                    return;
                }

                fullProducts = ImportFilterHelper.FilterByAllowedCodes(fullProducts, allowedCodes, p => p.Code, out var missingCodes).ToList();

                if (missingCodes.Any())
                {
                    Log.Warning("Missing {Count} product codes", missingCodes.Count);
                    foreach (var code in missingCodes)
                        Log.Warning("Missing: {Code}", code);
                }

                // Step 8.1: Delete products not in the current import list
                await _productSyncService.DeleteNotSyncedProducts(allowedCodes, IntegrationCompany.INTERCARS);

                // Step 8.2: Sync current products
                await _productSyncService.SyncToDatabaseAsync(fullProducts);

                Log.Information("Product synchronization completed successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during products synchronization");
            }
        }

        // ------------------------
        // Core data download logic
        // ------------------------

        private async Task<List<ProductResponse>> GetLatestProductInformationAsync()
        {
            Log.Information("Getting product info...");
            var latestFile = await GetLatestFileAsync("ProductInformation");
            if (latestFile == null) return new List<ProductResponse>();

            var zipBytes = await _httpClient.GetByteArrayAsync(latestFile.Url);
            return CsvHelperUtility.ParseCsvFromZip<ProductResponse>(zipBytes);
        }

        private async Task<List<StockPriceDto>> GetLatestStockPriceAsync()
        {
            Log.Information("Getting stock and price data...");
            var latestFile = await GetLatestFileAsync("Stock_price");
            if (latestFile == null) return new List<StockPriceDto>();

            var zipBytes = await _httpClient.GetByteArrayAsync(latestFile.Url);
            return CsvHelperUtility.ParseCsvFromZip<StockPriceDto>(zipBytes);
        }

        private async Task<List<ImageResponse>> GetLatestProductImagesAsync()
        {
            Log.Information("Getting images...");
            var latestFile = await GetLatestFileAsync("Pictures");
            if (latestFile == null) return new List<ImageResponse>();

            var zipBytes = await _httpClient.GetByteArrayAsync(latestFile.Url);
            return CsvHelperUtility.ParseCsvFromZip<ImageResponse>(zipBytes);
        }

        // -------------------------------------
        // Common helper for fetching latest file
        // -------------------------------------
        private async Task<ApiFile> GetLatestFileAsync(string endpoint)
        {
            string url = $"{_apiSettings.BaseUrl.TrimEnd('/')}/customer/{_apiSettings.Username}/{endpoint}";

            var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == HttpStatusCode.MovedPermanently || response.StatusCode == HttpStatusCode.Redirect)
            {
                var redirectUrl = response.Headers.Location?.ToString();
                if (!string.IsNullOrEmpty(redirectUrl))
                    response = await _httpClient.GetAsync(redirectUrl);
            }

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Log.Information($"Status: {response.StatusCode} | Content: {content}");
                return null;
            }

            var html = await response.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Select <a> elements inside the <table>
            var links = doc.DocumentNode.SelectNodes("//table//a")
                ?.Select(a => new
                {
                    FileName = a.InnerText.Trim(),
                    Href = a.GetAttributeValue("href", "")
                })
                .Where(f => f.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (links == null || links.Count == 0)
                return null;

            // Extract date from filename
            var files = links
                .Select(f =>
                {
                    DateTime? date = null;
                    var match = System.Text.RegularExpressions.Regex.Match(f.FileName, @"(\d{4}-\d{2}-\d{2})");
                    if (match.Success && DateTime.TryParse(match.Value, out var parsed))
                        date = parsed;

                    return new ApiFile
                    {
                        FileName = f.FileName,
                        Url = new Uri(new Uri(url + "/"), f.Href).ToString(),
                        DateCreated = date
                    };
                })
                .OrderByDescending(f => f.DateCreated ?? DateTime.MinValue)
                .ToList();

            return files.FirstOrDefault();
        }

        // -------------------------------------
        // Aggregation + merge logic
        // -------------------------------------
        private Dictionary<string, StockAggregationDto> AggregateStockData(List<StockPriceDto> stockData)
        {
            Log.Information("Aggregating stock data...");

            return stockData
                .GroupBy(s => s.TowKod)
                .Select(g => new StockAggregationDto
                {
                    TowKod = g.Key,
                    TotalAvailability = g.Sum(x => x.Availability),
                    WholesalePrice = g.Average(x => x.WholesalePrice),
                    SumPrice = g.Average(x => x.SumPrice),
                    RetailPrice = g.Average(x => x.RetailPrice)
                })
                .ToDictionary(x => x.TowKod, x => x);
        }

        private async Task<List<ProductDto>> BuildProductDtosAsync(List<ProductResponse> products, Dictionary<string, StockAggregationDto> aggregatedStock, List<ImageResponse> images)
        {
            var imageGroups = images
                .GroupBy(i => i.TowKod)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SortNr).ToList());

            var result = new List<ProductDto>(products.Count);

            foreach (var p in products)
            {
                var dto = new ProductDto
                {
                    Code = p.TowKod.Length > 20 ? p.TowKod.Substring(0, 20) : p.TowKod,
                    TradingCode = p.IcIndex,
                    Name = $"{p.Description} {p.IcIndex}",
                    Ean = p.Barcodes?.Split(',').FirstOrDefault(),
                    Brand = p.Manufacturer,
                    Description = p.Description,
                    Weight = p.PackageWeight ?? 0,
                    Unit = "szt.",
                    Vat = 23,
                    IntegrationCompany = IntegrationCompany.INTERCARS,
                    Images = await BuildProductImagesAsync(imageGroups, p.TowKod)
                };

                if (aggregatedStock.TryGetValue(p.TowKod, out var stock))
                {
                    decimal margin = MarginHelper.CalculateMargin(
                        stock.WholesalePrice,
                        _defaultMargin,
                        _marginRanges);

                    dto.Quantity = stock.TotalAvailability;
                    dto.NetBuyPrice = stock.WholesalePrice;
                    dto.GrossBuyPrice = stock.WholesalePrice * 1.23m;
                    dto.NetSellPrice = stock.WholesalePrice * ((margin / 100m) + 1);
                    dto.GrossSellPrice = stock.WholesalePrice * 1.23m * ((margin / 100m) + 1);
                }

                result.Add(dto);
            }

            return result;
        }

        private async Task<List<ImageDto>> BuildProductImagesAsync(Dictionary<string, List<ImageResponse>> imageGroups, string towKod)
        {
            if (!imageGroups.TryGetValue(towKod, out var images))
                return new List<ImageDto>();

            var result = new List<ImageDto>(images.Count);

            foreach (var img in images)
            {
                result.Add(new ImageDto
                {
                    Name = $"{img.TowKod}_{img.SortNr}",
                    Url = img.ImageLink
                });
            }

            return result;
        }
    }
}