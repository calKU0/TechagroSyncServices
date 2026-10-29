using System;

namespace AgroramiSyncService.DTOs
{
    // Persisted, so new products are reported from the last successful check also after service restart
    public class NewProductsCheckState
    {
        public DateTime? LastCheckUtc { get; set; }
    }
}
