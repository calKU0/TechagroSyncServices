using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;

namespace TechagroApiSync.Shared.Helpers
{
    public static class SnapshotChangeDetector
    {
        public static async Task<IReadOnlyList<T>> DetectNewAsync<T>(string snapshotPath, IReadOnlyList<T> current, Func<T, string> keySelector)
        {
            var previousKeys = await LoadCodesFromSnapshotAsync(snapshotPath);

            if (previousKeys.Count == 0)
                return Array.Empty<T>();

            var newItems = new List<T>();
            foreach (var item in current)
            {
                if (!previousKeys.Contains(keySelector(item)))
                    newItems.Add(item);
            }

            return newItems;
        }

        public static async Task<HashSet<string>> LoadCodesFromSnapshotAsync(string snapshotPath)
        {
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(snapshotPath))
                return codes;

            using (var fs = File.OpenRead(snapshotPath))
            using (var sr = new StreamReader(fs))
            using (var reader = new JsonTextReader(sr))
            {
                while (await reader.ReadAsync())
                {
                    if (reader.TokenType == JsonToken.PropertyName && string.Equals((string)reader.Value, "code", StringComparison.OrdinalIgnoreCase))
                    {
                        if (await reader.ReadAsync() && reader.TokenType == JsonToken.String)
                        {
                            codes.Add((string)reader.Value);
                        }
                    }
                }
            }

            return codes;
        }

        public static Task SaveSnapshotAsync<T>(string snapshotPath, IReadOnlyList<T> data)
        {
            return FileUtils.WriteToJsonAsync(data, snapshotPath);
        }

        public static async Task<string> SaveProductsSnapshotCsvAsync(string folderPath, string fileName, IReadOnlyList<ProductDto> products)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException("Folder path cannot be empty.", nameof(folderPath));
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be empty.", nameof(fileName));

            Directory.CreateDirectory(folderPath);
            var filePath = Path.Combine(folderPath, fileName);

            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                await writer.WriteLineAsync("Numer katalogowy;Nazwa;Opis");

                foreach (var product in products)
                {
                    var line = string.Join(";",
                        EscapeCsvAsText(product.Code),
                        EscapeCsvAsText(product.Name),
                        EscapeCsvAsText(product.Description));
                    await writer.WriteLineAsync(line);
                }
            }

            return filePath;
        }

        public static void CleanOldSnapshots(string folderPath, int daysToKeep = 31)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException("Folder path cannot be empty.", nameof(folderPath));

            if (!Directory.Exists(folderPath))
                return;

            var cutoff = DateTime.Now.AddDays(-daysToKeep);
            foreach (var filePath in Directory.EnumerateFiles(folderPath))
            {
                var lastWriteTime = File.GetLastWriteTime(filePath);
                if (lastWriteTime < cutoff)
                    File.Delete(filePath);
            }
        }

        private static string EscapeCsvAsText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var normalized = value
                .Replace(";", ",")
                .Replace("\r\n", " ")
                .Replace("\r", " ")
                .Replace("\n", " ");
            var escaped = normalized.Replace("\"", "\"\"");
            return string.Format("=\"{0}\"", escaped);
        }
    }
}