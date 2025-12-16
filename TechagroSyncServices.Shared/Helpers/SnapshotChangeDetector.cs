using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TechagroSyncServices.Shared.Helpers;

namespace TechagroApiSync.Shared.Helpers
{
    public static class SnapshotChangeDetector
    {
        public static async Task<IReadOnlyList<T>> DetectNewAsync<T>(string snapshotPath, IReadOnlyList<T> current, Func<T, string> keySelector)
        {
            var previous = File.Exists(snapshotPath)
                ? await FileUtils.ReadFromJsonAsync<T>(snapshotPath)
                : new List<T>();

            if (!previous.Any())
                return Array.Empty<T>();

            var previousKeys = new HashSet<string>(
                previous.Select(keySelector),
                StringComparer.OrdinalIgnoreCase);

            return current
                .Where(c => !previousKeys.Contains(keySelector(c)))
                .ToList();
        }

        public static Task SaveSnapshotAsync<T>(string snapshotPath, IReadOnlyList<T> data)
        {
            return FileUtils.WriteToJsonAsync(data, snapshotPath);
        }
    }
}