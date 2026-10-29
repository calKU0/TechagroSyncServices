using System.Collections.Generic;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.DTOs;

namespace TechagroApiSync.Shared.Services
{
    public interface IProductSyncService
    {
        Task SyncToDatabaseAsync(List<ProductDto> products);

        // Deletes products that are not in the import list or are no longer returned by supplier API.
        // importCodes == null means the service syncs every product returned by API (no import list).
        Task DeleteNotSyncedProducts(IntegrationCompany integrationCompany, IReadOnlyCollection<string> importCodes, IEnumerable<string> fetchedCodes, bool isFetchComplete);
    }
}
