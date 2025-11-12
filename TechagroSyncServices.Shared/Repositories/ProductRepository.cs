using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
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
                    cmd.Parameters.AddWithValue("@NAZWA", productDto.Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@STAN", productDto.Quantity);
                    cmd.Parameters.AddWithValue("@INDEKS_KATALOGOWY", productDto.Code ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@INDEKS_HANDLOWY", productDto.TradingCode ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@CENA_ZAKUPU_BRUTTO", productDto.GrossBuyPrice);
                    cmd.Parameters.AddWithValue("@CENA_ZAKUPU_NETTO", productDto.NetBuyPrice);
                    cmd.Parameters.AddWithValue("@CENA_SPRZEDAZY_BRUTTO", productDto.GrossSellPrice);
                    cmd.Parameters.AddWithValue("@CENA_SPRZEDAZY_NETTO", productDto.NetSellPrice);
                    cmd.Parameters.AddWithValue("@VAT_ZAKUPU", vatString);
                    cmd.Parameters.AddWithValue("@VAT_SPRZEDAZY", vatString);
                    cmd.Parameters.AddWithValue("@KOD_KRESKOWY", productDto.Ean ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@WAGA", productDto.Weight);
                    cmd.Parameters.AddWithValue("@PRODUCENT", productDto.Brand ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ID_PRODUCENTA", productDto.Id);
                    cmd.Parameters.AddWithValue("@INTEGRATION_COMPANY", productDto.IntegrationCompany ?? "GASKA");
                    cmd.Parameters.AddWithValue("@UNIT", productDto.Unit ?? (object)DBNull.Value);

                    var resultParam = cmd.Parameters.Add("@Result", SqlDbType.Int);
                    resultParam.Direction = ParameterDirection.Output;

                    await cmd.ExecuteNonQueryAsync();
                    return (int)resultParam.Value;
                }
            }
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
    }
}