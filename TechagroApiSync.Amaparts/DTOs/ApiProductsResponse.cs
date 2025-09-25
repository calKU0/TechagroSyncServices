using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Serialization;

namespace TechagroApiSync.Amaparts.DTOs
{
    public class ApiProductsResponse
    {
        [CsvHelper.Configuration.Attributes.Name("Zdjęcia produktu")]
        public string ProductImageUrl { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("Kod produktu")]
        public string ProductCode { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("Okładka foto")]
        public string CoverImageUrl { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("Nazwa produktu")]
        public string ProductName { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("Opis produktu")]
        public string Description { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("Kategoria główna")]
        public string MainCategory { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("Producent")]
        public string Manufacturer { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("Stan magazynowy")]
        public int StockQuantity { get; set; }

        [CsvHelper.Configuration.Attributes.Name("Cena netto zakupu")]
        public decimal NetPurchasePrice { get; set; }
    }
}