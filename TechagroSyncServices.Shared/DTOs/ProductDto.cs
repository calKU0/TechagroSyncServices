using System.Collections.Generic;
using TechagroApiSync.Shared.DTOs;
using TechagroApiSync.Shared.Enums;

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
        public decimal NetSellPriceB { get; set; }
        public decimal GrossSellPriceB { get; set; }
        public decimal? NetSellPriceC { get; set; }
        public decimal? GrossSellPriceC { get; set; }
        public string CategoriesString { get; set; }
        public decimal Vat { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal Weight { get; set; }
        public string Brand { get; set; }
        public IntegrationCompany IntegrationCompany { get; set; }
        public string Description { get; set; }
        public List<ImageDto> Images { get; set; } = new List<ImageDto>();
    }
}