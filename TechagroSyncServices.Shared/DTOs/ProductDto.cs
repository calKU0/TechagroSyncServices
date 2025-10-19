namespace TechagroSyncServices.Shared.DTOs
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string TradingCode { get; set; }
        public string Name { get; set; }
        public string Ean { get; set; }
        public decimal NetBuyPrice { get; set; }
        public decimal GrossBuyPrice { get; set; }
        public decimal NetSellPrice { get; set; }
        public decimal GrossSellPrice { get; set; }
        public int Vat { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal Weight { get; set; }
        public string Brand { get; set; }
        public string IntegrationCompany { get; set; }
    }
}