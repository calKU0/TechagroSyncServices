using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
    }
}