using System;
using System.Collections.Generic;
using System.Linq;
using TechagroSyncServices.Shared.Helpers;

namespace TechagroSyncServices.Shared.Services
{
    // Remembers in how many complete fetches in a row a product was not returned by API.
    // A product skipped only once (e.g. shifted between pages while paging, short API glitch) is never deleted.
    // Counters are persisted, so service restarts do not reset them.
    public class MissingProductsTracker
    {
        private readonly string _filePath;

        public MissingProductsTracker(string filePath)
        {
            _filePath = filePath;
        }

        // Counts the current misses and returns how many complete fetches in a row each code was missing.
        // Codes returned by API again are forgotten, so they start from zero next time.
        public Dictionary<string, int> Track(IEnumerable<string> missingCodes)
        {
            var previous = new Dictionary<string, int>(FileUtils.ReadJson<Dictionary<string, int>>(_filePath), StringComparer.OrdinalIgnoreCase);

            var current = missingCodes
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(code => code, code => (previous.TryGetValue(code, out var count) ? count : 0) + 1, StringComparer.OrdinalIgnoreCase);

            FileUtils.WriteJson(_filePath, current);
            return current;
        }
    }
}
