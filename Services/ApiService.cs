using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TeachagroApiSync.DTOs;
using TeachagroApiSync.Helpers;

namespace TechagroApiSync.Services
{
    public class ApiService
    {
        private readonly GaskaApiSettings _apiSettings;
        private readonly string _connectionString;

        public ApiService(GaskaApiSettings apiCredentials, string connectionString)
        {
            _apiSettings = apiCredentials;
            _connectionString = connectionString;
        }

        public async Task SyncProducts()
        {
            HashSet<int> fetchedProductIds = new HashSet<int>();
            bool hasErrors = false;

            using (var client = new HttpClient())
            {
                ApiHelper.AddDefaultHeaders(_apiSettings, client);

                int page = 1;
                bool hasMore = true;

                while (hasMore)
                {
                    try
                    {
                        var url = $"{_apiSettings.BaseUrl}/products?page={page}&perPage={_apiSettings.ProductsPerPage}&lng=pl";
                        Log.Information($"Sending request to {url}.");
                        var response = await client.GetAsync(url);

                        if (!response.IsSuccessStatusCode)
                        {
                            Log.Error($"API error while fetching page {page}: {response.StatusCode}");
                            hasErrors = true;
                            break;
                        }

                        var json = await response.Content.ReadAsStringAsync();
                        var apiResponse = JsonConvert.DeserializeObject<ApiProductsResponse>(json);

                        if (apiResponse.Products == null || apiResponse.Products.Count == 0)
                        {
                            hasMore = false;
                            break;
                        }

                        //using (SqlConnection connection = new SqlConnection(_connectionString))
                        //{
                        //    await connection.OpenAsync();

                        //    foreach (var apiProduct in apiResponse.Products)
                        //    {
                        //        fetchedProductIds.Add(apiProduct.Id);

                        //        string sql = @"
                        //        MERGE Products AS target
                        //        USING (SELECT @Id AS Id) AS source
                        //        ON (target.Id = source.Id)
                        //        WHEN MATCHED THEN
                        //            UPDATE SET
                        //                CodeGaska = @CodeGaska,
                        //                CodeCustomer = @CodeCustomer,
                        //                Name = @Name,
                        //                Description = @Description,
                        //                Ean = @Ean,
                        //                TechnicalDetails = @TechnicalDetails,
                        //                WeightGross = @WeightGross,
                        //                WeightNet = @WeightNet,
                        //                SupplierName = @SupplierName,
                        //                SupplierLogo = @SupplierLogo,
                        //                InStock = @InStock,
                        //                CurrencyPrice = @CurrencyPrice,
                        //                PriceNet = @PriceNet,
                        //                PriceGross = @PriceGross,
                        //                Archived = 0
                        //        WHEN NOT MATCHED THEN
                        //            INSERT (Id, CodeGaska, CodeCustomer, Name, Description, Ean, TechnicalDetails, WeightGross, WeightNet, SupplierName, SupplierLogo, InStock, CurrencyPrice, PriceNet, PriceGross, Archived)
                        //            VALUES (@Id, @CodeGaska, @CodeCustomer, @Name, @Description, @Ean, @TechnicalDetails, @WeightGross, @WeightNet, @SupplierName, @SupplierLogo, @InStock, @CurrencyPrice, @PriceNet, @PriceGross, 0);";

                        //        using (SqlCommand cmd = new SqlCommand(sql, connection))
                        //        {
                        //            cmd.Parameters.AddWithValue("@Id", apiProduct.Id);
                        //            cmd.Parameters.AddWithValue("@CodeGaska", (object)apiProduct.CodeGaska ?? DBNull.Value);
                        //            cmd.Parameters.AddWithValue("@CodeCustomer", (object)apiProduct.CodeCustomer ?? DBNull.Value);
                        //            cmd.Parameters.AddWithValue("@Name", (object)apiProduct.Name ?? DBNull.Value);
                        //            cmd.Parameters.AddWithValue("@Description", (object)apiProduct.Description ?? DBNull.Value);
                        //            cmd.Parameters.AddWithValue("@Ean", (object)apiProduct.Ean ?? DBNull.Value);
                        //            cmd.Parameters.AddWithValue("@TechnicalDetails", (object)apiProduct.TechnicalDetails ?? DBNull.Value);
                        //            cmd.Parameters.AddWithValue("@WeightGross", (object)apiProduct.GrossWeight ?? DBNull.Value);
                        //            cmd.Parameters.AddWithValue("@WeightNet", (object)apiProduct.NetWeight ?? DBNull.Value);
                        //            cmd.Parameters.AddWithValue("@SupplierName", (object)apiProduct.SupplierName ?? DBNull.Value);
                        //            cmd.Parameters.AddWithValue("@SupplierLogo", (object)apiProduct.SupplierLogo ?? DBNull.Value);
                        //            cmd.Parameters.AddWithValue("@InStock", (object)apiProduct.InStock ?? DBNull.Value);
                        //            cmd.Parameters.AddWithValue("@CurrencyPrice", (object)apiProduct.CurrencyPrice ?? DBNull.Value);
                        //            cmd.Parameters.AddWithValue("@PriceNet", (object)apiProduct.NetPrice ?? DBNull.Value);
                        //            cmd.Parameters.AddWithValue("@PriceGross", (object)apiProduct.GrossPrice ?? DBNull.Value);

                        //            await cmd.ExecuteNonQueryAsync();
                        //        }
                        //    }
                        //}
                        Log.Information($"Successfully fetched {apiResponse.Products.Count} products.");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Error while getting products from page {page}.");
                        hasErrors = true;
                        break;
                    }
                    finally
                    {
                        page++;
                        await Task.Delay(TimeSpan.FromSeconds(_apiSettings.ProductsInterval));
                    }
                }
            }

            if (hasErrors)
            {
                Log.Warning("Errors occurred during product sync. Archiving skipped to avoid data inconsistency.");
                return;
            }

            //try
            //{
            //    Log.Information("Searching for products to archive.");
            //    int archivedCount = 0;

            //    using (SqlConnection connection = new SqlConnection(_connectionString))
            //    {
            //        await connection.OpenAsync();

            //        // Build parameterized IN clause
            //        var idParams = new List<string>();
            //        var cmd = new SqlCommand();
            //        cmd.Connection = connection;

            //        int i = 0;
            //        foreach (var id in fetchedProductIds)
            //        {
            //            string paramName = $"@id{i}";
            //            idParams.Add(paramName);
            //            cmd.Parameters.AddWithValue(paramName, id);
            //            i++;
            //        }

            //        if (idParams.Count <= 0) return;

            //        string sql = $@"
            //            UPDATE Products
            //            SET Archived = 1
            //            WHERE Id NOT IN ({string.Join(",", idParams)}) AND Archived = 0;

            //            SELECT @@ROWCOUNT;";

            //        cmd.CommandText = sql;

            //        var result = await cmd.ExecuteScalarAsync();
            //        archivedCount = Convert.ToInt32(result);
            //    }

            //    Log.Information($"Archived {archivedCount} products.");
            //}
            //catch (Exception ex)
            //{
            //    Log.Error(ex, "An error occurred while checking for products to archive.");
            //}
        }

        //public async Task SyncProductDetails()
        //{
        //    List<Product> productsToUpdate = new List<Product>();

        //    using (var db = new MyDbContext())
        //    {
        //        try
        //        {
        //            // Get products that are missing the details collections
        //            productsToUpdate = await db.Products
        //                .Where(p => !p.Categories.Any() && p.PriceNet >= _minProductPrice && !p.Archived)
        //                .Take(_apiSettings.ProductPerDay)
        //                .ToListAsync();

        //            // If nothing was found, fallback to products ordered by UpdatedDate
        //            if (!productsToUpdate.Any())
        //            {
        //                productsToUpdate = await db.Products
        //                    .Where(p => p.PriceNet >= _minProductPrice && !p.Archived)
        //                    .OrderBy(p => p.UpdatedDate)
        //                    .Take(_apiSettings.ProductPerDay)
        //                    .ToListAsync();
        //            }

        //            if (!productsToUpdate.Any())
        //            {
        //                return;
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Log.Error(ex, $"Error while getting products to update details from database");
        //            return;
        //        }

        //        using (var client = new HttpClient())
        //        {
        //            ApiHelper.AddDefaultHeaders(_apiSettings, client);

        //            foreach (var product in productsToUpdate)
        //            {
        //                try
        //                {
        //                    var response = await client.GetAsync($"{_apiSettings.BaseUrl}/product?id={product.Id}&lng=pl");

        //                    if (!response.IsSuccessStatusCode)
        //                    {
        //                        Log.Error($"API error while fetching product details. Product ID: {product.Id}. Product Code: {product.CodeGaska}. Response Status: {response.StatusCode}");
        //                        continue;
        //                    }

        //                    var json = await response.Content.ReadAsStringAsync();
        //                    var apiResponse = JsonConvert.DeserializeObject<ApiProductResponse>(json);

        //                    if (apiResponse?.Product == null)
        //                    {
        //                        continue;
        //                    }

        //                    var updatedProduct = apiResponse.Product;

        //                    // Clear existing collections
        //                    db.Packages.RemoveRange(product.Packages);
        //                    db.CrossNumbers.RemoveRange(product.CrossNumbers);
        //                    db.Components.RemoveRange(product.Components);
        //                    db.RecommendedParts.RemoveRange(product.RecommendedParts);
        //                    db.Applications.RemoveRange(product.Applications);
        //                    db.ProductParameters.RemoveRange(product.Parameters);
        //                    db.ProductImages.RemoveRange(product.Images);
        //                    db.ProductFiles.RemoveRange(product.Files);
        //                    db.ProductCategories.RemoveRange(product.Categories);

        //                    // Update fields
        //                    product.CodeGaska = updatedProduct.CodeGaska;
        //                    product.CodeCustomer = updatedProduct.CodeCustomer;
        //                    product.Name = updatedProduct.Name;
        //                    product.SupplierName = updatedProduct.SupplierName;
        //                    product.SupplierLogo = updatedProduct.SupplierLogo;
        //                    product.InStock = updatedProduct.InStock;
        //                    product.CurrencyPrice = updatedProduct.CurrencyPrice;
        //                    product.PriceNet = updatedProduct.PriceNet;
        //                    product.PriceGross = updatedProduct.PriceGross;
        //                    product.UpdatedDate = DateTime.Now;

        //                    // Map updated collections
        //                    product.Packages = updatedProduct.Packages.Select(p => new Package
        //                    {
        //                        PackUnit = p.PackUnit,
        //                        PackQty = p.PackQty,
        //                        PackNettWeight = p.PackNettWeight,
        //                        PackGrossWeight = p.PackGrossWeight,
        //                        PackEan = p.PackEan,
        //                        PackRequired = p.PackRequired
        //                    }).ToList();

        //                    product.CrossNumbers = (updatedProduct.CrossNumbers ?? Enumerable.Empty<ApiCrossNumber>())
        //                        .Where(c => c != null && !string.IsNullOrEmpty(c.CrossNumber))
        //                        .SelectMany(c => c.CrossNumber
        //                            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
        //                            .Select(cn => new CrossNumber
        //                            {
        //                                CrossNumberValue = cn.Trim(),
        //                                CrossManufacturer = c.CrossManufacturer
        //                            }))
        //                        .ToList();

        //                    product.Components = updatedProduct.Components.Select(c => new Component
        //                    {
        //                        TwrID = c.TwrID,
        //                        CodeGaska = c.CodeGaska,
        //                        Qty = c.Qty
        //                    }).ToList();

        //                    product.RecommendedParts = updatedProduct.RecommendedParts.Select(r => new RecommendedPart
        //                    {
        //                        TwrID = r.TwrID,
        //                        CodeGaska = r.CodeGaska,
        //                        Qty = r.Qty
        //                    }).ToList();

        //                    product.Applications = updatedProduct.Applications.Select(a => new Application
        //                    {
        //                        ApplicationId = a.Id,
        //                        ParentID = a.ParentID,
        //                        Name = a.Name
        //                    }).ToList();

        //                    product.Parameters = updatedProduct.Parameters.Select(p => new ProductParameter
        //                    {
        //                        AttributeId = p.AttributeId,
        //                        AttributeName = p.AttributeName,
        //                        AttributeValue = p.AttributeValue
        //                    }).ToList();

        //                    product.Images = updatedProduct.Images.Select(i => new ProductImage
        //                    {
        //                        Title = i.Title,
        //                        Url = i.Url
        //                    }).ToList();

        //                    product.Files = updatedProduct.Files.Select(f => new ProductFile
        //                    {
        //                        Title = f.Title,
        //                        Url = f.Url
        //                    }).ToList();

        //                    product.Categories = updatedProduct.Categories.Select(c => new ProductCategory
        //                    {
        //                        CategoryId = c.Id,
        //                        ParentID = c.ParentID,
        //                        Name = c.Name
        //                    }).ToList();

        //                    await db.SaveChangesAsync();
        //                    Log.Information($"Successfully fetched and updated details of product with ID: {product.Id} and Code: {product.CodeGaska}");
        //                }
        //                catch (Exception ex)
        //                {
        //                    Log.Error(ex, $"Error while trying to fetch and update product data. Product with ID: {product.Id} and code {product.CodeGaska}");
        //                    continue;
        //                }
        //                finally
        //                {
        //                    Task.Delay(TimeSpan.FromSeconds(_apiSettings.ProductInterval)).Wait(); // Respect API rate limits
        //                }
        //            }
        //        }
        //    }
        //}
    }
}