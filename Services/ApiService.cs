using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TeachagroApiSync.DTOs;
using TeachagroApiSync.Helpers;

namespace TechagroApiSync.Services
{
    public class ApiService
    {
        private readonly GaskaApiSettings _apiSettings;
        private readonly int _margin;
        private readonly string _connectionString;

        public ApiService(GaskaApiSettings apiCredentials, int margin, string connectionString)
        {
            _apiSettings = apiCredentials;
            _margin = margin;
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

                        using (SqlConnection connection = new SqlConnection(_connectionString))
                        {
                            await connection.OpenAsync();

                            foreach (var apiProduct in apiResponse.Products)
                            {
                                fetchedProductIds.Add(apiProduct.Id);

                                string procedure = @"dbo.UpsertProduct";
                                try
                                {
                                    using (SqlCommand cmd = new SqlCommand(procedure, connection))
                                    {
                                        cmd.CommandType = CommandType.StoredProcedure;

                                        string name = apiProduct.Name;

                                        if (!string.IsNullOrWhiteSpace(name))
                                        {
                                            // Match leading product code (numbers, dots, slashes, letters)
                                            var match = Regex.Match(name, @"^(?<code>[0-9A-Za-z./x-]+)\s+(?<rest>.+)$");

                                            if (match.Success)
                                            {
                                                name = $"{match.Groups["rest"].Value} {match.Groups["code"].Value}";
                                            }
                                        }

                                        cmd.Parameters.AddWithValue("@NAZWA", (object)name ?? DBNull.Value);
                                        cmd.Parameters.AddWithValue("@STAN", (object)apiProduct.InStock ?? 0);
                                        cmd.Parameters.AddWithValue("@INDEKS_KATALOGOWY", (object)apiProduct.CodeGaska ?? DBNull.Value);
                                        cmd.Parameters.AddWithValue("@CENA_ZAKUPU_BRUTTO", (object)apiProduct.GrossPrice ?? 0);
                                        cmd.Parameters.AddWithValue("@CENA_ZAKUPU_NETTO", (object)apiProduct.NetPrice ?? 0);
                                        cmd.Parameters.AddWithValue("@CENA_SPRZEDAZY_BRUTTO", apiProduct.GrossPrice * ((_margin / 100m) + 1));
                                        cmd.Parameters.AddWithValue("@CENA_SPRZEDAZY_NETTO", apiProduct.NetPrice * ((_margin / 100m) + 1));
                                        cmd.Parameters.AddWithValue("@KOD_KRESKOWY", (object)apiProduct.Ean ?? DBNull.Value);
                                        cmd.Parameters.AddWithValue("@WAGA", (object)apiProduct.GrossWeight ?? DBNull.Value);
                                        cmd.Parameters.AddWithValue("@PRODUCENT", (object)apiProduct.SupplierName ?? DBNull.Value);
                                        cmd.Parameters.AddWithValue("@ID_PRODUCENTA", (object)apiProduct.Id ?? DBNull.Value);
                                        cmd.Parameters.AddWithValue("@UNIT", (object)apiProduct.Unit ?? DBNull.Value);

                                        await cmd.ExecuteNonQueryAsync();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, $"Failed to upsert product {apiProduct.Id}");
                                    hasErrors = true;
                                }
                            }
                            Log.Information($"Successfully fetched and updated {apiResponse.Products.Count} products.");
                        }
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
                Log.Warning("Errors occurred during product sync.");
                return;
            }
        }

        public async Task SyncProductDetails()
        {
            List<int> productsToUpdate = new List<int>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                try
                {
                    string procedure = "dbo.GetProductsWithoutDescription";

                    using (var cmd = new SqlCommand(procedure, connection))
                    {
                        cmd.Parameters.AddWithValue("@TopCount", _apiSettings.ProductPerDay);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                productsToUpdate.Add(reader.GetInt32(0));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error while fetching products without description.");
                    return;
                }

                if (!productsToUpdate.Any())
                {
                    return; // Nothing to process
                }

                using (var client = new HttpClient())
                {
                    ApiHelper.AddDefaultHeaders(_apiSettings, client);

                    foreach (var plu in productsToUpdate)
                    {
                        try
                        {
                            var response = await client.GetAsync($"{_apiSettings.BaseUrl}/product?id={plu}&lng=pl");
                            if (!response.IsSuccessStatusCode)
                            {
                                Log.Error($"API error while fetching product details. PLU: {plu}, Status: {response.StatusCode}");
                                continue;
                            }

                            var json = await response.Content.ReadAsStringAsync();
                            var apiResponse = JsonConvert.DeserializeObject<ApiProductResponse>(json);

                            if (apiResponse?.Product == null)
                                continue;

                            var p = apiResponse.Product;

                            // Build the HTML description for Opis
                            var opisBuilder = new StringBuilder();

                            if (p.Parameters?.Any() == true)
                            {
                                opisBuilder.Append("<p><b>Parametry: </b>")
                                           .Append(string.Join(", ", p.Parameters.Select(ps => $"{ps.AttributeName} : {ps.AttributeValue}")))
                                           .Append("</p>");
                            }

                            if (p.CrossNumbers?.Any() == true)
                            {
                                opisBuilder.Append("<p><b>Numery referencyjne: </b>")
                                           .Append(string.Join(", ", p.CrossNumbers.Select(c => c.CrossNumber)))
                                           .Append("</p>");
                            }

                            // Applications section
                            if (p.Applications?.Any() == true)
                            {
                                var appsByParent = p.Applications
                                                          .GroupBy(a => a.ParentID)
                                                          .ToDictionary(g => g.Key, g => g.ToList());

                                string BuildApplicationsHtml(List<ApiApplication> apps, int depth = 1)
                                {
                                    if (!apps.Any()) return string.Empty;

                                    var sb = new StringBuilder();
                                    sb.Append("<ul>");

                                    foreach (var app in apps)
                                    {
                                        sb.Append("<li>").Append(app.Name);

                                        if (appsByParent.ContainsKey(app.Id))
                                            sb.Append(BuildApplicationsHtml(appsByParent[app.Id], depth + 1));

                                        sb.Append("</li>");
                                    }

                                    sb.Append("</ul>");
                                    return sb.ToString();
                                }

                                if (appsByParent.ContainsKey(0))
                                {
                                    opisBuilder.Append("<div class=\"product-applications\">")
                                               .Append("<p><b>Zastosowanie: </b></p>")
                                               .Append(BuildApplicationsHtml(appsByParent[0]))
                                               .Append("</div>");
                                }
                            }

                            string TruncateHtml(string html, int maxLength)
                            {
                                if (string.IsNullOrEmpty(html) || html.Length <= maxLength)
                                    return html;

                                // Find the last safe closing tag before the limit
                                int lastLiClose = html.LastIndexOf("</li>", maxLength, StringComparison.OrdinalIgnoreCase);
                                int lastPClose = html.LastIndexOf("</p>", maxLength, StringComparison.OrdinalIgnoreCase);
                                int lastDivClose = html.LastIndexOf("</div>", maxLength, StringComparison.OrdinalIgnoreCase);

                                // Choose the last valid cutoff point
                                int cutoff = Math.Max(lastLiClose, Math.Max(lastPClose, lastDivClose));
                                if (cutoff == -1) cutoff = maxLength;

                                // Find actual closing tag length
                                string tag = null;
                                if (cutoff == lastLiClose) tag = "</li>";
                                else if (cutoff == lastPClose) tag = "</p>";
                                else if (cutoff == lastDivClose) tag = "</div>";

                                int cutoffLength = tag?.Length ?? 0;
                                if (cutoff + cutoffLength > html.Length)
                                    cutoffLength = 0;

                                string truncated = html.Substring(0, Math.Min(cutoff + cutoffLength, html.Length));

                                // Ensure all opened tags are closed properly
                                var stack = new Stack<string>();
                                var regex = new Regex(@"</?([a-zA-Z0-9]+)[^>]*>");
                                foreach (Match match in regex.Matches(truncated))
                                {
                                    if (!match.Value.StartsWith("</"))
                                        stack.Push(match.Groups[1].Value);
                                    else if (stack.Count > 0 && stack.Peek().Equals(match.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
                                        stack.Pop();
                                }

                                // Close any still-open tags, but keep length <= maxLength
                                while (stack.Count > 0)
                                {
                                    string closeTag = $"</{stack.Pop()}>";
                                    if (truncated.Length + closeTag.Length > maxLength)
                                        break; // stop if adding would exceed limit
                                    truncated += closeTag;
                                }

                                // Final safety: hard cut if still too long
                                if (truncated.Length > maxLength)
                                    truncated = truncated.Substring(0, maxLength);

                                return truncated;
                            }

                            string nowyOpis = opisBuilder?.ToString() ?? string.Empty;
                            nowyOpis = TruncateHtml(nowyOpis, 1000);

                            // Upsert product
                            using (var cmd = new SqlCommand("dbo.UpdateProductDescription", connection))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;

                                cmd.Parameters.AddWithValue("@INDEKS_KATALOGOWY", p.CodeGaska ?? (object)DBNull.Value);
                                cmd.Parameters.AddWithValue("@NowyOpis", nowyOpis ?? (object)DBNull.Value);

                                await cmd.ExecuteNonQueryAsync();
                            }

                            // Upsert images
                            if (p.Images != null)
                            {
                                foreach (var img in p.Images)
                                {
                                    if (string.IsNullOrEmpty(img.Url))
                                        continue;

                                    byte[] imageData = await client.GetByteArrayAsync(img.Url);

                                    using (var cmdImg = new SqlCommand("dbo.UpsertProductImage", connection))
                                    {
                                        cmdImg.CommandType = CommandType.StoredProcedure;

                                        cmdImg.Parameters.Add("@INDEKS", SqlDbType.VarChar, 20).Value = p.CodeGaska ?? (object)DBNull.Value;
                                        cmdImg.Parameters.Add("@NAZWA_PLIKU", SqlDbType.VarChar, 100).Value = img.Title ?? "image";
                                        cmdImg.Parameters.Add("@DANE", SqlDbType.VarBinary, -1).Value = imageData;

                                        await cmdImg.ExecuteNonQueryAsync();
                                    }
                                }
                            }

                            Log.Information($"Inserted/updated product {p.CodeGaska}");
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Error processing product {plu}");
                        }
                        finally
                        {
                            await Task.Delay(TimeSpan.FromSeconds(_apiSettings.ProductInterval));
                        }
                    }
                }
            }
        }
    }
}