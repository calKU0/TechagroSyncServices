using System.Collections.Generic;
using TechagroApiSync.Shared.DTOs;
using TechagroApiSync.Shared.Enums;

namespace TechagroSyncServices.Shared.DTOs
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string TradingCode { get; set; } = "test";
        public string Name { get; set; } = "test";
        public string Ean { get; set; } = "test";
        public decimal NetBuyPrice { get; set; } = 0;
        public decimal GrossBuyPrice { get; set; } = 0;
        public decimal NetSellPriceB { get; set; } = 0;
        public decimal GrossSellPriceB { get; set; } = 0;
        public decimal? NetSellPriceC { get; set; } = 0;
        public decimal? GrossSellPriceC { get; set; } = 0;
        public string CategoriesString { get; set; } = "test";
        public decimal Vat { get; set; } = 0;
        public decimal Quantity { get; set; } = 0;
        public string Unit { get; set; } = "szt.";
        public decimal Weight { get; set; } = 0;
        public string Brand { get; set; } = "test";
        public IntegrationCompany IntegrationCompany { get; set; }
        public string Description { get; set; } = "test";
        public List<ImageDto> Images { get; set; } = new List<ImageDto>();
    }
}