using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechagroApiSync.Amaparts.DTOs
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