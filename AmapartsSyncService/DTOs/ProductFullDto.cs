using System.Collections.Generic;

namespace AmapartsSyncService.DTOs
{
    public class ProductFullDto
    {
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public int StockQuantity { get; set; }
        public decimal NetPurchasePrice { get; set; }
        public List<string> Photos { get; set; } = new List<string>();

        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
    }
}