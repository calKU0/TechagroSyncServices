using System;

namespace KrampSyncService.DTOs
{
    public class RequestsState
    {
        public DateTime Date { get; set; }
        public int PricesRequests { get; set; }
        public int AtpRequests { get; set; }
    }
}
