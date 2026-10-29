using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.DTOs;

namespace TechagroSyncServices.Shared.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly string _connectionString;

        public ProductRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<int> UpsertProductAsync(ProductDto productDto)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var cmd = new SqlCommand("dbo.UpsertProduct", connection))
                {
                    string vatString = ((int)Math.Round(productDto.Vat)).ToString();
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Data missing in supplier offer is passed as NULL - the procedure keeps values filled in manually
                    cmd.Parameters.AddWithValue("@AKTUALIZUJ_NAZWE", productDto.HasOwnName());
                    cmd.Parameters.AddWithValue("@NAZWA", productDto.Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@STAN", productDto.Quantity);
                    cmd.Parameters.AddWithValue("@INDEKS_KATALOGOWY", productDto.Code ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@INDEKS_HANDLOWY", ValueOrNull(productDto.TradingCode));
                    cmd.Parameters.AddWithValue("@CENA_ZAKUPU_BRUTTO", productDto.GrossBuyPrice);
                    cmd.Parameters.AddWithValue("@CENA_ZAKUPU_NETTO", productDto.NetBuyPrice);
                    cmd.Parameters.AddWithValue("@CENA_SPRZEDAZY_BRUTTO", productDto.GrossSellPriceB);
                    cmd.Parameters.AddWithValue("@CENA_SPRZEDAZY_NETTO", productDto.NetSellPriceB);
                    cmd.Parameters.AddWithValue("@CENA_SPRZEDAZY_BRUTTO_C", (object)productDto.GrossSellPriceC ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@CENA_SPRZEDAZY_NETTO_C", (object)productDto.NetSellPriceC ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@VAT_ZAKUPU", vatString);
                    cmd.Parameters.AddWithValue("@VAT_SPRZEDAZY", vatString);
                    cmd.Parameters.AddWithValue("@KOD_KRESKOWY", ValueOrNull(productDto.Ean));
                    cmd.Parameters.AddWithValue("@WAGA", productDto.Weight > 0 ? productDto.Weight : (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PRODUCENT", ValueOrNull(productDto.Brand));
                    cmd.Parameters.AddWithValue("@ID_PRODUCENTA", productDto.Id);
                    cmd.Parameters.AddWithValue("@CATEGORIES", ValueOrNull(productDto.CategoriesString));
                    cmd.Parameters.AddWithValue("@INTEGRATION_COMPANY", productDto.IntegrationCompany.ToString());
                    cmd.Parameters.AddWithValue("@UNIT", productDto.Unit ?? (object)DBNull.Value);

                    var resultParam = cmd.Parameters.Add("@Result", SqlDbType.Int);
                    resultParam.Direction = ParameterDirection.Output;

                    await cmd.ExecuteNonQueryAsync();
                    return (int)resultParam.Value;
                }
            }
        }

        public async Task<int[]> UpsertProductsAsync(IReadOnlyList<(ProductDto Product, string Description)> products)
        {
            // Columns in order of dbo.ProductUpsertList. Texts are cut like parameters of UpsertProduct.
            var table = new DataTable();
            table.Columns.Add("RowNo", typeof(int));
            table.Columns.Add("INDEKS_KATALOGOWY", typeof(string));
            table.Columns.Add("INDEKS_HANDLOWY", typeof(string));
            table.Columns.Add("NAZWA", typeof(string));
            table.Columns.Add("AKTUALIZUJ_NAZWE", typeof(bool));
            table.Columns.Add("STAN", typeof(decimal));
            table.Columns.Add("KOD_KRESKOWY", typeof(string));
            table.Columns.Add("CENA_ZAKUPU_BRUTTO", typeof(decimal));
            table.Columns.Add("CENA_ZAKUPU_NETTO", typeof(decimal));
            table.Columns.Add("CENA_SPRZEDAZY_BRUTTO", typeof(decimal));
            table.Columns.Add("CENA_SPRZEDAZY_NETTO", typeof(decimal));
            table.Columns.Add("CENA_SPRZEDAZY_BRUTTO_C", typeof(decimal));
            table.Columns.Add("CENA_SPRZEDAZY_NETTO_C", typeof(decimal));
            table.Columns.Add("VAT", typeof(string));
            table.Columns.Add("WAGA", typeof(decimal));
            table.Columns.Add("PRODUCENT", typeof(string));
            table.Columns.Add("ID_PRODUCENTA", typeof(int));
            table.Columns.Add("UNIT", typeof(string));
            table.Columns.Add("CATEGORIES", typeof(string));
            table.Columns.Add("INTEGRATION_COMPANY", typeof(string));
            table.Columns.Add("OPIS", typeof(string));

            for (int i = 0; i < products.Count; i++)
            {
                var (productDto, description) = products[i];
                table.Rows.Add(
                    i,
                    Cut(productDto.Code, 20),
                    Cut(ValueOrNull(productDto.TradingCode), 20),
                    Cut(productDto.Name, 255),
                    productDto.HasOwnName(),
                    productDto.Quantity,
                    Cut(ValueOrNull(productDto.Ean), 1000),
                    productDto.GrossBuyPrice,
                    productDto.NetBuyPrice,
                    productDto.GrossSellPriceB,
                    productDto.NetSellPriceB,
                    (object)productDto.GrossSellPriceC ?? DBNull.Value,
                    (object)productDto.NetSellPriceC ?? DBNull.Value,
                    ((int)Math.Round(productDto.Vat)).ToString(),
                    productDto.Weight > 0 ? productDto.Weight : (object)DBNull.Value,
                    Cut(ValueOrNull(productDto.Brand), 50),
                    productDto.Id,
                    Cut(productDto.Unit, 10),
                    Cut(ValueOrNull(productDto.CategoriesString), 1000),
                    Cut(productDto.IntegrationCompany.ToString(), 50),
                    (object)description ?? DBNull.Value);
            }

            var results = new int[products.Count];
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var cmd = new SqlCommand("dbo.UpsertProducts", connection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 600;

                    var tvpParam = cmd.Parameters.AddWithValue("@Products", table);
                    tvpParam.SqlDbType = SqlDbType.Structured;
                    tvpParam.TypeName = "dbo.ProductUpsertList";

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        // Matched by row number - codes come back converted to database code page
                        while (await reader.ReadAsync())
                            results[reader.GetInt32(0)] = reader.GetInt32(1);
                    }
                }
            }
            return results;
        }

        private static object ValueOrNull(string value) => string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value;

        private static object Cut(object value, int length)
        {
            if (!(value is string text))
                return DBNull.Value;

            return text.Length > length ? text.Substring(0, length) : text;
        }

        public async Task UpdateProductDescriptionAsync(string code, string description)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var cmd = new SqlCommand("dbo.UpdateProductDescription", connection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@INDEKS_KATALOGOWY", code ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@NowyOpis", description ?? (object)DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task UpsertProductImageAsync(string code, string fileName, byte[] imageData, bool skipWhenExistsImages = false)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var cmd = new SqlCommand("dbo.UpsertProductImage", connection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@INDEKS", SqlDbType.VarChar, 20).Value = code ?? (object)DBNull.Value;
                    cmd.Parameters.Add("@NAZWA_PLIKU", SqlDbType.VarChar, 100).Value = fileName ?? "image";
                    cmd.Parameters.Add("@DANE", SqlDbType.VarBinary, -1).Value = imageData;
                    cmd.Parameters.Add("@SKIP_WHEN_EXISTS_IMAGES", SqlDbType.Bit).Value = skipWhenExistsImages ? 1 : 0;

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<List<int>> GetProductsWithoutDescription(int topCount)
        {
            List<int> products = new List<int>();
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                using (var cmd = new SqlCommand("dbo.GetProductsWithoutDescription", connection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@TopCount", topCount);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            products.Add(reader.GetInt32(0));
                        }
                    }
                }
            }
            return products;
        }

        public async Task<List<string>> GetProductCodesAsync(IntegrationCompany integrationCompany)
        {
            var codes = new List<string>();
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var cmd = new SqlCommand("dbo.GetProductCodes", connection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IntegrationCompany", SqlDbType.VarChar).Value = integrationCompany.ToString();

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            codes.Add(reader.GetString(0));
                        }
                    }
                }
            }
            return codes;
        }

        public async Task<int> DeleteNotSyncedProducts(IntegrationCompany integrationCompany, List<string> codes)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var cmd = new SqlCommand("dbo.DeleteNotSyncedProducts", connection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = 7200; // 120 minutes
                    cmd.Parameters.Add("@IntegrationCompany", SqlDbType.VarChar).Value = integrationCompany.ToString();

                    var dt = new DataTable();
                    dt.Columns.Add("Code", typeof(string));

                    foreach (var code in codes)
                        dt.Rows.Add(code);

                    var tvpParam = cmd.Parameters.AddWithValue("@AllowedCodes", dt);
                    tvpParam.SqlDbType = SqlDbType.Structured;
                    tvpParam.TypeName = "dbo.StringList";

                    var result = await cmd.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
        }
    }
}