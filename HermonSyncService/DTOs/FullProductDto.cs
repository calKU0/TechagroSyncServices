using System.Collections.Generic;

namespace HermonSyncService.DTOs
{
    public class FullProductDto
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Ean { get; set; }
        public string Unit { get; set; }
        public string CN { get; set; }
        public decimal Weight { get; set; }
        public string Description { get; set; }
        public string Brand { get; set; }
        public decimal PriceNet { get; set; }
        public decimal PriceGross { get; set; }
        public decimal Tax { get; set; }
        public decimal Quantity { get; set; }
        public List<(string, byte[])> Images { get; set; }
    }
}