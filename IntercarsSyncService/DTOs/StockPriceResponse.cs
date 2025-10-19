namespace IntercarsSyncService.DTOs
{
    public class StockPriceDto
    {
        public string TowKod { get; set; }
        public string Warehouse { get; set; }
        public int Availability { get; set; }
        public decimal WholesalePrice { get; set; }
        public decimal SumPrice { get; set; }
        public decimal RetailPrice { get; set; }
    }
}