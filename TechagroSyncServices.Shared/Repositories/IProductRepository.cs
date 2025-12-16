using System.Collections.Generic;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.DTOs;

namespace TechagroSyncServices.Shared.Repositories
{
    public interface IProductRepository
    {
        Task<int> UpsertProductAsync(ProductDto product);

        Task UpdateProductDescriptionAsync(string code, string description);

        Task UpsertProductImageAsync(string code, string fileName, byte[] imageData, bool skipWhenExistsImages = false);

        Task<int> DeleteNotSyncedProducts(IntegrationCompany integrationCompany, List<string> codes);

        Task<List<int>> GetProductsWithoutDescription(int topCount);
    }
}