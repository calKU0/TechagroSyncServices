namespace HiFiSyncService.DTOs
{
    // Live price and stock of a single HIFI reference
    public class HiFiProduct
    {
        public string Reference { get; set; }
        public decimal NetPrice { get; set; }
        public string Currency { get; set; }
        public decimal Stock { get; set; }
    }
}
