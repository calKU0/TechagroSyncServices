using System;
using System.Collections.Generic;
using System.Linq;

namespace TechagroApiSync.Shared.Helpers
{
    public static class ImportFilterHelper
    {
        public static IReadOnlyList<T> FilterByAllowedCodes<T>(IReadOnlyList<T> source, IEnumerable<string> allowedCodes, Func<T, string> codeSelector, out IReadOnlyList<string> missingCodes)
        {
            var allowedSet = new HashSet<string>(
                allowedCodes,
                StringComparer.OrdinalIgnoreCase);

            var filtered = source
                .Where(p => allowedSet.Contains(codeSelector(p)))
                .ToList();

            var foundSet = new HashSet<string>(
                filtered.Select(codeSelector),
                StringComparer.OrdinalIgnoreCase);

            missingCodes = allowedSet
                .Where(code => !foundSet.Contains(code))
                .ToList();

            return filtered;
        }
    }
}