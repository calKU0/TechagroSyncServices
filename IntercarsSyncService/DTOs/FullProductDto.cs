using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntercarsSyncService.DTOs
{
    public class FullProductDto
    {
        public string TowKod { get; set; }
        public string IcIndex { get; set; }
        public string TecDoc { get; set; }
        public string TecDocProd { get; set; }
        public string ArticleNumber { get; set; }
        public string Manufacturer { get; set; }
        public string ShortDescription { get; set; }
        public string Description { get; set; }
        public string Barcodes { get; set; }
        public decimal? PackageWeight { get; set; }
        public decimal? PackageLength { get; set; }
        public decimal? PackageWidth { get; set; }
        public decimal? PackageHeight { get; set; }
        public string CustomCode { get; set; }
        public bool BlockedReturn { get; set; }
        public string Gtu { get; set; }

        // Stock & price aggregated fields
        public int TotalAvailability { get; set; }
        public decimal WholesalePrice { get; set; }
        public decimal CorePrice { get; set; }
        public decimal SumPrice { get; set; }
        public decimal RetailPrice { get; set; }

        // Images
        public List<ImageResponse> Images { get; set; } = new List<ImageResponse>();

    }
}
