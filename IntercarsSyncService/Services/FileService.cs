using CsvHelper;
using IntercarsSyncService.DTOs;
using IntercarsSyncService.Helpers;
using IntercarsSyncService.Settings;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Repositories;

namespace IntercarsSyncService.Services
{
    public class FileService
    {
        private readonly IntercarsApiSettings _apiSettings;
        private readonly IProductRepository _productRepo;

        public FileService(IProductRepository productRepo)
        {
            _productRepo = productRepo;
            _apiSettings = AppSettingsLoader.LoadApiSettings();
        }

        public async Task SyncProducts()
        {
            try
            {
                Log.Information("Starting products synchronization...");

                var products = await GetLatestProductInformationAsync();
                if (!products.Any())
                {
                    Log.Warning("No products found. Aborting sync.");
                    return;
                }

                var stockData = await GetLatestStockPriceAsync();
                if (!stockData.Any())
                {
                    Log.Warning("No stock data found. Aborting sync.");
                    return;
                }

                var images = await GetLatestProductImagesAsync();
                if (!images.Any())
                {
                    Log.Warning("No images found. Aborting sync.");
                    return;
                }

                var aggregatedStock = AggregateStockData(stockData);
                var fullProducts = BuildFullProductDtos(products, aggregatedStock, images);

                Log.Information("Merged {Count} products", fullProducts.Count);

                //var productSync = new ProductSyncService(_productRepo);
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
            var latestFile = await GetLatestFileAsync("stockPrice");
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
            var latestFile = await GetLatestFileAsync("pictures");
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
            string url = $"{_apiSettings.BaseUrl}/customer/{_apiSettings.Username}/{endpoint}";

            using (var client = HttpClientHelper.CreateAuthorizedClient(_apiSettings.Username, _apiSettings.Password))
            {
                var response = await client.GetStringAsync(url);
                var files = JsonConvert.DeserializeObject<List<ApiFile>>(response);
                return files?
                    .OrderByDescending(f => f.DateCreated ?? DateTime.MinValue)
                    .FirstOrDefault();
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
                    CorePrice = g.Average(x => x.CorePrice),
                    SumPrice = g.Average(x => x.SumPrice),
                    RetailPrice = g.Average(x => x.RetailPrice)
                })
                .ToDictionary(x => x.TowKod, x => x);
        }

        private List<FullProductDto> BuildFullProductDtos(
            List<ProductResponse> products,
            Dictionary<string, StockAggregationDto> aggregatedStock,
            List<ImageResponse> images)
        {
            Log.Information("Building FullProductDto list...");

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
                    dto.CorePrice = stock.CorePrice;
                    dto.SumPrice = stock.SumPrice;
                    dto.RetailPrice = stock.RetailPrice;
                }

                return dto;
            }).ToList();
        }
    }
}
