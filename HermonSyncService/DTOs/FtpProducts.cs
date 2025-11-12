namespace HermonSyncService.DTOs
{
    public class FtpProducts
    {
        [CsvHelper.Configuration.Attributes.Name("Kod towaru")]
        public string Code { get; set; }

        [CsvHelper.Configuration.Attributes.Name("Nazwa towaru")]
        public string Name { get; set; }

        [CsvHelper.Configuration.Attributes.Name("kod kreskowy")]
        public string Ean { get; set; }

        [CsvHelper.Configuration.Attributes.Name("Jednostka sprzedaży")]
        public string Unit { get; set; }

        [CsvHelper.Configuration.Attributes.Name("Kod CN")]
        public string CN { get; set; }

        [CsvHelper.Configuration.Attributes.Name("Masa Netto")]
        public decimal Weight { get; set; }

        [CsvHelper.Configuration.Attributes.Name("Uwagi do towaru")]
        public string Description { get; set; }
    }
}