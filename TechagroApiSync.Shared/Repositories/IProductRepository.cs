using System.Collections.Generic;
using System.Threading.Tasks;
using TechagroApiSync.Shared.DTOs;

namespace TechagroApiSync.Shared.Repositories
{
    public interface IProductRepository
    {
        Task<int> UpsertProductAsync(ProductDto product);

        Task UpdateProductDescriptionAsync(string code, string description);

        Task UpsertProductImageAsync(string code, string fileName, byte[] imageData);

        Task<List<int>> GetProductsWithoutDescription(int topCount);
    }
}