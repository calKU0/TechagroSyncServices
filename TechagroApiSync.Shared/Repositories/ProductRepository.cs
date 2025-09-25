using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using TechagroApiSync.Shared.DTOs;

namespace TechagroApiSync.Shared.Repositories
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
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@NAZWA", productDto.Name ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@STAN", (object)productDto.Quantity ?? 0);
                    cmd.Parameters.AddWithValue("@INDEKS_KATALOGOWY", productDto.Code ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@CENA_ZAKUPU_BRUTTO", (object)productDto.GrossBuyPrice ?? 0);
                    cmd.Parameters.AddWithValue("@CENA_ZAKUPU_NETTO", (object)productDto.NetBuyPrice ?? 0);
                    cmd.Parameters.AddWithValue("@CENA_SPRZEDAZY_BRUTTO", (object)productDto.GrossSellPrice ?? 0);
                    cmd.Parameters.AddWithValue("@CENA_SPRZEDAZY_NETTO", (object)productDto.NetSellPrice ?? 0);
                    cmd.Parameters.AddWithValue("@VAT_ZAKUPU", productDto.Vat.ToString() ?? "23");
                    cmd.Parameters.AddWithValue("@VAT_SPRZEDAZY", productDto.Vat.ToString() ?? "23");
                    cmd.Parameters.AddWithValue("@KOD_KRESKOWY", productDto.Ean ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@WAGA", (object)productDto.Weight ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@PRODUCENT", productDto.Brand ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ID_PRODUCENTA", (object)productDto.Id ?? (object)DBNull.Value);
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

        public async Task UpsertProductImageAsync(string code, string fileName, byte[] imageData)
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