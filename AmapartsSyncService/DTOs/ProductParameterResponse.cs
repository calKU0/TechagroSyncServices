﻿namespace AmapartsSyncService.DTOs
{
    public class ProductParameterCsv
    {
        [CsvHelper.Configuration.Attributes.Name("Kod produktu")]
        public string ProductCode { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("Parametr")]
        public string Parameter { get; set; } = string.Empty;

        [CsvHelper.Configuration.Attributes.Name("Wartość")]
        public string Value { get; set; } = string.Empty;
    }
}