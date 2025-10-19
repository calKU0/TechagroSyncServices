namespace IntercarsSyncService.DTOs
{
    public class ProductResponse
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
    }
}