using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Repositories;

namespace TechagroSyncServices.Tests.Unit
{
    // Records delete calls instead of touching the database
    public class FakeProductRepository : IProductRepository
    {
        public List<string> ProductCodesInDatabase { get; set; } = new List<string>();
        public List<string> CodesToKeep { get; private set; }
        public int DeleteCalls { get; private set; }
        public bool ThrowOnDelete { get; set; }

        public Task<int> DeleteNotSyncedProducts(IntegrationCompany integrationCompany, List<string> codes)
        {
            DeleteCalls++;
            CodesToKeep = codes;

            if (ThrowOnDelete)
                throw new InvalidOperationException("Database is down");

            return Task.FromResult(0);
        }

        public Task<List<string>> GetProductCodesAsync(IntegrationCompany integrationCompany) => Task.FromResult(ProductCodesInDatabase);

        public Task<int> UpsertProductAsync(ProductDto product) => Task.FromResult(1);

        public Task<int[]> UpsertProductsAsync(IReadOnlyList<(ProductDto Product, string Description)> products) =>
            Task.FromResult(new int[products.Count]);

        public Task UpdateProductDescriptionAsync(string code, string description) => Task.FromResult(0);

        public Task UpsertProductImageAsync(string code, string fileName, byte[] imageData, bool skipWhenExistsImages = false) => Task.FromResult(0);

        public Task<List<int>> GetProductsWithoutDescription(int topCount) => Task.FromResult(new List<int>());
    }
}
