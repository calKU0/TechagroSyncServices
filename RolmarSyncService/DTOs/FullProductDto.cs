using System.Collections.Generic;

namespace RolmarSyncService.DTOs
{
    public class FullProductDto
    {
        public int Id { get; set; }
        public string Ean { get; set; }
        public string ProductIndex { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Weight { get; set; }
        public string Unit { get; set; }
        public string Brand { get; set; }
        public string Cn { get; set; }
        public List<Specification> Specifications { get; set; }
        public decimal Price { get; set; }
        public decimal Stock { get; set; }
        public List<PhotoItem> Images { get; set; }
    }
}