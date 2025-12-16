using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace TechagroSyncServices.Shared.Helpers
{
    public static class FileUtils
    {
        public static async Task WriteToJsonAsync<T>(IEnumerable<T> data, string filePath)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            using (var stream = File.Create(filePath))
            {
                await JsonSerializer.SerializeAsync(stream, data, options);
            }
        }

        public static async Task<List<T>> ReadFromJsonAsync<T>(string filePath)
        {
            if (!File.Exists(filePath))
                return new List<T>();

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
                return new List<T>();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            using (var stream = File.OpenRead(filePath))
            {
                return await JsonSerializer.DeserializeAsync<List<T>>(stream, options) ?? new List<T>();
            }
        }

        public static List<string> ReadImportList(string filePath)
        {
            if (!File.Exists(filePath))
                return new List<string>();

            return File.ReadAllLines(filePath)
                       .Select(line => line.Trim())
                       .Where(line => !string.IsNullOrWhiteSpace(line))
                       .Distinct(StringComparer.OrdinalIgnoreCase)
                       .ToList();
        }
    }
}