using AmapartsSyncService.DTOs;
using AmapartsSyncService.Helpers;
using AmapartsSyncService.Settings;
using CsvHelper;
using CsvHelper.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TechagroApiSync.Shared.DTOs;
using TechagroApiSync.Shared.Enums;
using TechagroApiSync.Shared.Helpers;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;
using TechagroSyncServices.Shared.Services;

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

        public async Task<List<ApiProductsResponse>> FetchProductsDetails()
        {
            try
            {
                // 1. Trigger CSV generation for products
                Log.Information($"Triggering product export. (This takes few minutes)");
                await _client.GetAsync($"export.php?q={_apiSettings.ApiKey}");

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

                return productsDetails;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during product sync.");
                return new List<ApiProductsResponse>();
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