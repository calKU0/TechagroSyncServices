using Serilog;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Repositories;
using TechagroSyncServices.Shared.Services;
using TechagroSyncServices.Shared.Settings;

namespace TechagroApiSync.Shared.Services
{
    public class ProductSyncService : IProductSyncService
    {
        // Removing a few products at once is normal, so the percent limit applies only above this count
        private const int AlwaysAllowedMissingCount = 5;

        // Products saved in one database call - big enough to save round trips, small enough to keep transactions short
        private const int UpsertBatchSize = 500;

        // SQL Server errors meaning the database is unavailable - saving products one by one would only wait for each timeout
        private static readonly HashSet<int> DatabaseUnavailableErrors = new HashSet<int> { -1, 2, 53, 233, 701, 4060, 10053, 10054, 10060, 10061, 11001, 18456, 40613 };
        private const int DeadlockError = 1205;

        private readonly IProductRepository _productRepo;
        private readonly HttpClient _imageClient;
        private readonly DeleteSafetySettings _deleteSettings;
        private readonly MissingProductsTracker _missingTracker;
        private readonly decimal _sellPriceRoundingStep;

        // sellPriceRoundingStep - sale prices are rounded like in WAPRO (e.g. 0.01 or 0.10), so the next run sees no false changes
        public ProductSyncService(IProductRepository productRepo, HttpClient imageClient = null, DeleteSafetySettings deleteSettings = null, decimal? sellPriceRoundingStep = null)
        {
            _productRepo = productRepo;
            _imageClient = imageClient ?? new HttpClient();
            _deleteSettings = deleteSettings ?? CommonSettingsLoader.LoadDeleteSafetySettings();
            _missingTracker = new MissingProductsTracker(_deleteSettings.MissingProductsFilePath);
            _sellPriceRoundingStep = sellPriceRoundingStep ?? CommonSettingsLoader.GetSellPriceRoundingStep();
        }

        public async Task SyncToDatabaseAsync(List<ProductDto> products)
        {
            int productInserted = 0;
            int productUpdated = 0;
            int productUnchanged = 0;

            Log.Information("Attempting to update {Count} products in database.", products.Count);

            foreach (var product in products)
                RoundPrices(product);

            for (int start = 0; start < products.Count; start += UpsertBatchSize)
            {
                var batch = products.GetRange(start, Math.Min(UpsertBatchSize, products.Count - start));
                var results = await UpsertBatchAsync(batch);

                for (int i = 0; i < batch.Count; i++)
                {
                    var product = batch[i];
                    if (results[i] == 1)
                    {
                        productInserted++;
                        Log.Information("Inserted product {Code} - {Name}", product.Code, product.Name);
                    }
                    else if (results[i] == 2)
                    {
                        productUpdated++;
                        Log.Information("Updated product {Code} - {Name}", product.Code, product.Name);
                    }
                    else if (results[i] == 0)
                    {
                        productUnchanged++;
                    }

                    await SyncImagesAsync(product);
                }
            }

            Log.Information("Products imported: {Total}/{All}, Inserted: {Inserted}, Updated: {Updated}, Unchanged: {Unchanged}",
                productInserted + productUpdated + productUnchanged, products.Count, productInserted, productUpdated, productUnchanged);
        }

        // Sale prices are saved rounded like WAPRO keeps them, otherwise every run would see a change and save them again.
        // Purchase prices are not rounded in WAPRO, so they are saved as they are.
        private void RoundPrices(ProductDto product)
        {
            product.NetSellPriceB = RoundToStep(product.NetSellPriceB, _sellPriceRoundingStep);
            product.GrossSellPriceB = RoundToStep(product.GrossSellPriceB, _sellPriceRoundingStep);

            if (product.NetSellPriceC.HasValue)
                product.NetSellPriceC = RoundToStep(product.NetSellPriceC.Value, _sellPriceRoundingStep);
            if (product.GrossSellPriceC.HasValue)
                product.GrossSellPriceC = RoundToStep(product.GrossSellPriceC.Value, _sellPriceRoundingStep);
        }

        private static decimal RoundToStep(decimal value, decimal step)
        {
            if (step <= 0)
                return value;

            return Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
        }

        // Saves the whole batch in one database call. When it fails (e.g. one product with wrong data),
        // products are saved one by one, so only the wrong ones are skipped.
        // Returns result for each product: -1 = failed, 0 = nothing changed, 1 = inserted, 2 = updated.
        private async Task<int[]> UpsertBatchAsync(List<ProductDto> batch)
        {
            var items = batch.Select(product => (Product: product, Description: BuildDescription(product))).ToList();

            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return await _productRepo.UpsertProductsAsync(items);
                }
                catch (SqlException ex) when (ex.Number == DeadlockError && attempt == 1)
                {
                    // Another service was saving at the same time - the batch is retried once as a whole
                    Log.Warning("Batch of {Count} products was chosen as a deadlock victim. Retrying.", batch.Count);
                }
                catch (SqlException ex) when (DatabaseUnavailableErrors.Contains(ex.Number))
                {
                    Log.Error(ex, "Database is unavailable. Batch of {Count} products is skipped and will be saved in next run.", batch.Count);
                    return Enumerable.Repeat(-1, batch.Count).ToArray();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to save batch of {Count} products at once. Saving them one by one.", batch.Count);
                    break;
                }
            }

            var singleResults = new int[items.Count];
            for (int i = 0; i < items.Count; i++)
                singleResults[i] = await UpsertSingleAsync(items[i].Product, items[i].Description);

            return singleResults;
        }

        private async Task<int> UpsertSingleAsync(ProductDto product, string description)
        {
            int result = -1;

            try
            {
                result = await _productRepo.UpsertProductAsync(product);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to upsert product {Code}", product.Code);
            }

            try
            {
                if (description != null)
                {
                    await _productRepo.UpdateProductDescriptionAsync(product.Code, description);
                    Log.Information("Updated description for product {Code}", product.Code);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update product description {Code}", product.Code);
            }

            return result;
        }

        // Products without description keep the one filled in manually
        private static string BuildDescription(ProductDto product)
        {
            if (string.IsNullOrWhiteSpace(product.Description))
                return null;

            var opisBuilder = new StringBuilder();
            opisBuilder.Append("<h2>Opis produktu</h2>");
            opisBuilder.Append(product.Description);

            return DescriptionHelper.TruncateHtml(opisBuilder.ToString(), 1000);
        }

        private async Task SyncImagesAsync(ProductDto product)
        {
            try
            {
                if (product.Images != null && product.Images.Any())
                {
                    foreach (var img in product.Images)
                    {
                        // Ensure we have image bytes
                        if (img.Data == null || img.Data.Length == 0)
                        {
                            if (!string.IsNullOrEmpty(img.Url))
                            {
                                try
                                {
                                    img.Data = await _imageClient.GetByteArrayAsync(img.Url);
                                    Log.Information("Downloaded image for {Code} from {Url}", product.Code, img.Url);
                                }
                                catch (Exception ex)
                                {
                                    Log.Warning(ex, "Failed to download image for {Code} from {Url}", product.Code, img.Url);
                                }
                            }
                            else if (!string.IsNullOrEmpty(img.Path))
                            {
                                try
                                {
                                    img.Data = File.ReadAllBytes(img.Path);
                                    Log.Information("Downloaded image for {Code} from {Path}", product.Code, img.Path);
                                }
                                catch (Exception ex)
                                {
                                    Log.Warning(ex, "Failed to download image for {Code} from {Url}", product.Code, img.Url);
                                }
                            }
                        }

                        // Upsert image if we have data
                        if (img.Data != null && img.Data.Length > 0)
                        {
                            try
                            {
                                await _productRepo.UpsertProductImageAsync(product.Code, img.Name, img.Data);
                                Log.Information("Updated image for {Code}", product.Code);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Failed to update image for {Code} from Url: {Url}", product.Code, img.Url);
                            }
                            finally
                            {
                                // Clear image data from memory
                                img.Data = null;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update images for {Code}", product.Code);
            }
        }

        public async Task DeleteNotSyncedProducts(IntegrationCompany integrationCompany, IReadOnlyCollection<string> importCodes, IEnumerable<string> fetchedCodes, bool isFetchComplete)
        {
            try
            {
                var codesToKeep = await GetCodesToKeepAsync(integrationCompany, importCodes, fetchedCodes, isFetchComplete);
                if (codesToKeep == null)
                    return;

                Log.Information("Attempting to delete products that are not in the import list or not returned by API. This takes a while...");
                int result = await _productRepo.DeleteNotSyncedProducts(integrationCompany, codesToKeep);
                Log.Information("Deleted {Count} not synced products from database.", result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete not synced products from database.");
            }
        }

        // Returns codes of products that have to stay in database, or null when nothing can be deleted safely.
        // Products missing in API are deleted only when the fetch was complete, the number of missing products looks sane
        // and they were missing in several complete fetches in a row, so network errors, broken API responses
        // or products shifted between pages never remove products.
        private async Task<List<string>> GetCodesToKeepAsync(IntegrationCompany integrationCompany, IReadOnlyCollection<string> importCodes, IEnumerable<string> fetchedCodes, bool isFetchComplete)
        {
            bool hasImportList = importCodes != null;

            // All codes are compared the way they are saved in database - trimmed and cut to column length,
            // otherwise a long code from import file would never match its product
            if (hasImportList)
                importCodes = importCodes.Select(ProductCodeAffix.Normalize).ToList();

            if (!isFetchComplete)
            {
                if (!hasImportList)
                {
                    Log.Warning("Fetch from API was not complete. Skipping deletion of products.");
                    return null;
                }

                Log.Warning("Fetch from API was not complete. Only products not in the import list will be deleted.");
                return importCodes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            // Without import list every product returned by API is synced, so products expected in API are the ones in database
            var expectedCodes = (hasImportList ? importCodes : await _productRepo.GetProductCodesAsync(integrationCompany))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var fetchedSet = new HashSet<string>(fetchedCodes.Select(ProductCodeAffix.Normalize), StringComparer.OrdinalIgnoreCase);
            var missingSet = new HashSet<string>(expectedCodes.Where(code => !fetchedSet.Contains(code)), StringComparer.OrdinalIgnoreCase);

            if (missingSet.Any() && IsSuspiciousMissingCount(missingSet.Count, expectedCodes.Count, fetchedSet.Count))
            {
                Log.Warning("API did not return {Missing} of {Expected} products (limit {Limit}%). It looks like an API error, so these products are not deleted.",
                    missingSet.Count, expectedCodes.Count, _deleteSettings.MaxMissingProductsPercent);

                return hasImportList ? expectedCodes : null;
            }

            // Counted only for complete and sane fetches, so failed runs neither delete nor reset anything
            var missingRuns = _missingTracker.Track(missingSet);
            var codesToDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var missing in missingRuns)
            {
                if (missing.Value >= _deleteSettings.MissingRunsBeforeDelete)
                {
                    codesToDelete.Add(missing.Key);
                    Log.Warning("Product {Code} is no longer returned by API ({Runs} runs in a row) and will be deleted.", missing.Key, missing.Value);
                }
                else
                {
                    Log.Warning("Product {Code} was not returned by API ({Runs}/{Required} runs in a row). It will be deleted if it is still missing.",
                        missing.Key, missing.Value, _deleteSettings.MissingRunsBeforeDelete);
                }
            }

            return expectedCodes.Where(code => !codesToDelete.Contains(code)).ToList();
        }

        private bool IsSuspiciousMissingCount(int missingCount, int expectedCount, int fetchedCount)
        {
            // API that returned nothing is never trusted
            if (fetchedCount == 0)
                return true;

            if (missingCount <= AlwaysAllowedMissingCount)
                return false;

            return missingCount * 100m / expectedCount > _deleteSettings.MaxMissingProductsPercent;
        }
    }
}