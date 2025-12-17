using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace IntercarsSyncService.Helpers
{
    public static class CsvHelperUtility
    {
        public static IEnumerable<T> ParseCsvFromZipStream<T>(Stream zipStream)
        {
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: false))
            {
                var csvEntry = archive.Entries
                    .FirstOrDefault(e => e.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));

                if (csvEntry == null)
                    yield break;

                using (var csvStream = csvEntry.Open())
                using (var reader = new StreamReader(csvStream, Encoding.UTF8, true, 1024 * 8))
                {
                    var config = new CsvConfiguration(new CultureInfo("pl-PL"))
                    {
                        Delimiter = ";",
                        HasHeaderRecord = true,
                        MissingFieldFound = null,
                        BadDataFound = null,
                        PrepareHeaderForMatch = args =>
                            args.Header?.Replace("_", "").ToLowerInvariant()
                    };

                    using (var csv = new CsvReader(reader, config))
                    {
                        foreach (var record in csv.GetRecords<T>())
                        {
                            yield return record; // streams one row at a time
                        }
                    }
                }
            }
        }
    }
}