using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntercarsSyncService.Helpers
{
    public static class CsvHelperUtility
    {
        public static List<T> ParseCsvFromZip<T>(byte[] zipBytes)
        {
            using (var zipStream = new MemoryStream(zipBytes))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                var csvEntry = archive.Entries
                    .FirstOrDefault(e => e.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));

                if (csvEntry == null)
                    return new List<T>();

                using (var csvStream = csvEntry.Open())
                using (var reader = new StreamReader(csvStream, Encoding.UTF8))
                {
                    var config = new CsvConfiguration(new CultureInfo("pl-PL"))
                    {
                        Delimiter = ";",
                        HasHeaderRecord = true,
                        MissingFieldFound = null,
                        BadDataFound = null,
                        PrepareHeaderForMatch = args => args.Header?.Replace("_", "")?.ToLowerInvariant()
                    };

                    using (var csv = new CsvReader(reader, config))
                    {
                        var records = csv.GetRecords<T>().ToList();
                        return records;
                    }
                }
            }
        }
    }
}