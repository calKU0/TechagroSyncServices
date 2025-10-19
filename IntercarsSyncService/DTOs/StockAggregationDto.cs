namespace IntercarsSyncService.DTOs
{
    public class StockAggregationDto
    {
        public string TowKod { get; set; }
        public int TotalAvailability { get; set; }
        public decimal WholesalePrice { get; set; }
        public decimal SumPrice { get; set; }
        public decimal RetailPrice { get; set; }
    }
}