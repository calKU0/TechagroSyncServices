using System.Collections.Generic;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.DTOs;

namespace TechagroSyncServices.Shared.Repositories
{
    public interface IProductRepository
    {
        Task<int> UpsertProductAsync(ProductDto product);

        // Saves products with descriptions (null keeps the current one) in one transaction.
        // Returns result for each product, in the same order: 0 = nothing changed, 1 = inserted, 2 = updated.
        Task<int[]> UpsertProductsAsync(IReadOnlyList<(ProductDto Product, string Description)> products);

        Task UpdateProductDescriptionAsync(string code, string description);

        Task UpsertProductImageAsync(string code, string fileName, byte[] imageData, bool skipWhenExistsImages = false);

        Task<int> DeleteNotSyncedProducts(IntegrationCompany integrationCompany, List<string> codes);

        Task<List<string>> GetProductCodesAsync(IntegrationCompany integrationCompany);

        Task<List<int>> GetProductsWithoutDescription(int topCount);
    }
}