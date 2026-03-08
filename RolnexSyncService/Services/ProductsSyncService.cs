using CsvHelper;
using CsvHelper.Configuration;
using RolnexSyncService.DTOs;
using RolnexSyncService.Helpers;
using RolnexSyncService.Settings;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace RolnexSyncService.Services
{
    public class ProductsSyncService
    {
        private readonly RolnexApiSettings _apiSettings;
        private readonly HttpClient _client;

        public ProductsSyncService(HttpClient client)
        {
            _apiSettings = AppSettingsLoader.LoadApiSettings();
            _client = client;

            _client.BaseAddress = new Uri(_apiSettings.BaseUrl);
            _client.Timeout = TimeSpan.FromMinutes(10);
        }

        public async Task<List<ProductsResponse>> FetchProducts()
        {
            try
            {
                Log.Information($"Triggering product export. (This takes few minutes)");

                List<ProductsResponse> productsDetails;
                using (var stream = await _client.GetStreamAsync($"pl/xmlapi/2/2/UTF8/{_apiSettings.ApiKey}"))
                using (var reader = new StreamReader(stream))
                using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = ";",
                    HasHeaderRecord = true,
                    TrimOptions = TrimOptions.Trim,
                    BadDataFound = null,
                    MissingFieldFound = null
                }))
                {
                    productsDetails = csv.GetRecords<ProductsResponse>().ToList();
                }

                return productsDetails;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during product sync.");
                return new List<ProductsResponse>();
            }
        }
    }
}
