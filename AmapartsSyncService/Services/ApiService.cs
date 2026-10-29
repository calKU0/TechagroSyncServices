using AmapartsSyncService.DTOs;
using AmapartsSyncService.Helpers;
using AmapartsSyncService.Settings;
using CsvHelper;
using CsvHelper.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TechagroSyncServices.Shared.DTOs;

namespace AmapartsSyncService.Services
{
    public class ApiService
    {
        private readonly AmapartsApiSettings _apiSettings;
        private readonly HttpClient _client;

        public ApiService(HttpClient client)
        {
            _apiSettings = AppSettingsLoader.LoadApiSettings();
            _client = client;

            _client.BaseAddress = new Uri(_apiSettings.BaseUrl);
            _client.Timeout = TimeSpan.FromMinutes(10);
        }

        // Whole offer is a single CSV file, so any error means incomplete result
        public async Task<FetchResult<ApiProductsResponse>> FetchProductsDetails()
        {
            try
            {
                // 1. Trigger CSV generation for products
                Log.Information($"Triggering product export. (This takes few minutes)");
                using (var exportResponse = await _client.GetAsync($"export.php?q={_apiSettings.ApiKey}"))
                {
                    // Failed export leaves the CSV from a previous export, which must not be treated as the current offer
                    exportResponse.EnsureSuccessStatusCode();
                }

                // 3. Fetch product CSV
                List<ApiProductsResponse> productsDetails;
                using (var stream = await _client.GetStreamAsync($"products-{_apiSettings.ApiKey}.csv"))
                using (var reader = new StreamReader(stream))
                using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = ";",
                    HasHeaderRecord = true,
                    TrimOptions = TrimOptions.Trim,
                }))
                {
                    productsDetails = csv.GetRecords<ApiProductsResponse>().ToList();
                }

                return FetchResult<ApiProductsResponse>.Complete(productsDetails);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during product sync.");
                return FetchResult<ApiProductsResponse>.Incomplete(new List<ApiProductsResponse>());
            }
        }

        public async Task<List<ProductParameterCsv>> FetchParameters()
        {
            Log.Information($"Triggering product parameters export. (This takes few minutes)");
            await _client.GetAsync($"exportfeatures.php?q={_apiSettings.ApiKey}");

            List<ProductParameterCsv> parameters;
            using (var stream = await _client.GetStreamAsync($"products-features-{_apiSettings.ApiKey}.csv"))
            using (var reader = new StreamReader(stream))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
            }))
            {
                parameters = csv.GetRecords<ProductParameterCsv>().ToList();
            }

            return parameters;
        }
    }
}