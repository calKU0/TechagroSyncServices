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
using System.Threading.Tasks;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;
using TechagroSyncServices.Shared.Services;

namespace IntercarsSyncService.Services
{
    public class FileService
    {
        private readonly IntercarsApiSettings _apiSettings;
        private readonly IEmailService _emailService;
        private readonly IProductRepository _productRepo;

        public FileService(IProductRepository productRepo, IEmailService emailService)
        {
            _productRepo = productRepo;
            _emailService = emailService;
            _apiSettings = AppSettingsLoader.LoadApiSettings();
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
                var fullProducts = BuildFullProductDtos(products, aggregatedStock, images);
                Log.Information("Merged {Count} products", fullProducts.Count);

                // Step 5.1: Detect newly added products
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Export", $"products.json");
                var previousProducts = await FileUtils.ReadFromJsonAsync<FullProductDto>(filePath);
                var newProducts = ProductComparer.FindNewProducts(previousProducts, fullProducts);

                if (newProducts.Count > 0 && previousProducts.Count > 0)
                {
                    try
                    {
                        Log.Information("Detected {Count} NEW products", newProducts.Count);

                        string to = AppSettingsLoader.GetEmailsToNotify();

                        const int batchSize = 100;
                        int totalBatches = (int)Math.Ceiling(newProducts.Count / (double)batchSize);

                        for (int i = 0; i < totalBatches; i++)
                        {
                            var batch = newProducts.Skip(i * batchSize).Take(batchSize).ToList();
                            string subject = $"Nowe produkty dodane do oferty Inter Cars ({newProducts.Count})";
                            string htmlBody = HtmlHelper.BuildNewProductsEmailHtml(batch);

                            await _emailService.SendEmailAsync(to, subject, htmlBody);

                            Log.Information("Sent email for batch {Batch}/{TotalBatches} ({Count} products)", i + 1, totalBatches, batch.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to send new products email notification");
                    }
                }
                else
                {
                    Log.Information("No new products detected.");
                }

                // Step 6: Export to JSON
                await FileUtils.WriteToJsonAsync(fullProducts, filePath);
                Log.Information("JSON file created at {Path}", filePath);

                // Step 7: Filter by import list
                var importFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Import", "numery_katalogowe.txt");
                var allowedTowKods = FileUtils.ReadImportList(importFilePath);

                if (!allowedTowKods.Any())
                {
                    Log.Warning("Import file is empty or missing. Aborting import");
                    return;
                }
                else
                {
                    var allowedSet = new HashSet<string>(allowedTowKods, StringComparer.OrdinalIgnoreCase);

                    fullProducts = fullProducts
                        .Where(p => allowedSet.Contains(p.TowKod))
                        .ToList();

                    Log.Information("Filtered product list to {Count} items based on import file", fullProducts.Count);
                }

                // Step 8: Sync to database
                //var productSync = new ProductSyncService(_productRepo);

                // Step 8.1: Delete products not in the current import list
                //await productSync.DeleteNotSyncedProducts(allowedTowKods);

                // Step 8.2: Sync current products
                //await productSync.SyncToDatabaseAsync(fullProducts);

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

            using (var client = HttpClientHelper.CreateAuthorizedClient(_apiSettings.Username, _apiSettings.Password))
            {
                var zipBytes = await client.GetByteArrayAsync(latestFile.Url);
                return CsvHelperUtility.ParseCsvFromZip<ProductResponse>(zipBytes);
            }
        }

        private async Task<List<StockPriceDto>> GetLatestStockPriceAsync()
        {
            Log.Information("Getting stock and price data...");
            var latestFile = await GetLatestFileAsync("Stock_price");
            if (latestFile == null) return new List<StockPriceDto>();

            using (var client = HttpClientHelper.CreateAuthorizedClient(_apiSettings.Username, _apiSettings.Password))
            {
                var zipBytes = await client.GetByteArrayAsync(latestFile.Url);
                return CsvHelperUtility.ParseCsvFromZip<StockPriceDto>(zipBytes);
            }
        }

        private async Task<List<ImageResponse>> GetLatestProductImagesAsync()
        {
            Log.Information("Getting images...");
            var latestFile = await GetLatestFileAsync("Pictures");
            if (latestFile == null) return new List<ImageResponse>();

            using (var client = HttpClientHelper.CreateAuthorizedClient(_apiSettings.Username, _apiSettings.Password))
            {
                var zipBytes = await client.GetByteArrayAsync(latestFile.Url);
                return CsvHelperUtility.ParseCsvFromZip<ImageResponse>(zipBytes);
            }
        }

        // -------------------------------------
        // Common helper for fetching latest file
        // -------------------------------------
        private async Task<ApiFile> GetLatestFileAsync(string endpoint)
        {
            string url = $"{_apiSettings.BaseUrl.TrimEnd('/')}/customer/{_apiSettings.Username}/{endpoint}";

            using (var client = HttpClientHelper.CreateAuthorizedClient(_apiSettings.Username, _apiSettings.Password))
            {
                var response = await client.GetAsync(url);

                if (response.StatusCode == HttpStatusCode.MovedPermanently || response.StatusCode == HttpStatusCode.Redirect)
                {
                    var redirectUrl = response.Headers.Location?.ToString();
                    if (!string.IsNullOrEmpty(redirectUrl))
                        response = await client.GetAsync(redirectUrl);
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

        private List<FullProductDto> BuildFullProductDtos(List<ProductResponse> products, Dictionary<string, StockAggregationDto> aggregatedStock, List<ImageResponse> images)
        {
            var imageGroups = images
                .GroupBy(i => i.TowKod)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SortNr).ToList());

            return products.Select(p =>
            {
                var dto = new FullProductDto
                {
                    TowKod = p.TowKod,
                    IcIndex = p.IcIndex,
                    TecDoc = p.TecDoc,
                    TecDocProd = p.TecDocProd,
                    ArticleNumber = p.ArticleNumber,
                    Manufacturer = p.Manufacturer,
                    ShortDescription = p.ShortDescription,
                    Description = p.Description,
                    Barcodes = p.Barcodes,
                    PackageWeight = p.PackageWeight,
                    PackageLength = p.PackageLength,
                    PackageWidth = p.PackageWidth,
                    PackageHeight = p.PackageHeight,
                    CustomCode = p.CustomCode,
                    BlockedReturn = p.BlockedReturn,
                    Gtu = p.Gtu,
                    Images = imageGroups.ContainsKey(p.TowKod)
                        ? imageGroups[p.TowKod]
                        : new List<ImageResponse>()
                };

                if (aggregatedStock.TryGetValue(p.TowKod, out var stock))
                {
                    dto.TotalAvailability = stock.TotalAvailability;
                    dto.WholesalePrice = stock.WholesalePrice;
                    dto.SumPrice = stock.SumPrice;
                    dto.RetailPrice = stock.RetailPrice;
                }

                return dto;
            }).ToList();
        }
    }
}