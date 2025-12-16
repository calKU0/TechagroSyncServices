using System.Collections.Generic;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.DTOs;

namespace TechagroApiSync.Shared.Services
{
    public interface IProductSyncService
    {
        Task SyncToDatabaseAsync(List<ProductDto> products);

        Task DeleteNotSyncedProducts(List<string> productCodes, IntegrationCompany integrationCompany);
    }
}