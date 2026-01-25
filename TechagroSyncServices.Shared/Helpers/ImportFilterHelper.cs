using System;
using System.Collections.Generic;
using System.Linq;

namespace TechagroApiSync.Shared.Helpers
{
    public static class ImportFilterHelper
    {
        public static IReadOnlyList<T> FilterByAllowedCodes<T>(IReadOnlyList<T> source, IEnumerable<string> allowedCodes, Func<T, string> codeSelector, out IReadOnlyList<string> missingCodes)
        {
            var allowedSet = new HashSet<string>(allowedCodes, StringComparer.OrdinalIgnoreCase);
            var filtered = new List<T>();
            var foundSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in source)
            {
                var code = codeSelector(item) ?? string.Empty;

                if (allowedSet.Contains(code))
                {
                    filtered.Add(item);
                    foundSet.Add(code);
                    continue;
                }

                // Obsługa kodów z sufiksem "-exp"
                if (code.EndsWith("-exp", StringComparison.OrdinalIgnoreCase) && code.Length > 4)
                {
                    var baseCode = code.Substring(0, code.Length - 4);
                    if (allowedSet.Contains(baseCode))
                    {
                        filtered.Add(item);
                        foundSet.Add(baseCode);
                    }
                }
            }

            missingCodes = allowedSet
                .Where(code => !foundSet.Contains(code))
                .ToList();

            return filtered;
        }
    }
}