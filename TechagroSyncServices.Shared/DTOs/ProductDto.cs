using System;
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
        public decimal NetBuyPrice { get; set; } = 0;
        public decimal GrossBuyPrice { get; set; } = 0;
        public decimal NetSellPriceB { get; set; } = 0;
        public decimal GrossSellPriceB { get; set; } = 0;
        public decimal? NetSellPriceC { get; set; }
        public decimal? GrossSellPriceC { get; set; }
        public string CategoriesString { get; set; }
        public decimal Vat { get; set; } = 0;
        public decimal Quantity { get; set; } = 0;
        public string Unit { get; set; } = "szt.";
        public decimal Weight { get; set; } = 0;
        public string Brand { get; set; }
        public IntegrationCompany IntegrationCompany { get; set; }
        public string Description { get; set; }
        public List<ImageDto> Images { get; set; } = new List<ImageDto>();

        // Suppliers without product data (e.g. Kramp, Granit, HiFi) send the code as name.
        // Such name is used only for new products and never overwrites a name filled in manually.
        public bool HasOwnName()
        {
            if (string.IsNullOrWhiteSpace(Name))
                return false;

            var name = Name.Trim();
            return !string.Equals(name, Code, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, TradingCode, StringComparison.OrdinalIgnoreCase);
        }
    }
}