using System;
using System.Collections.Generic;
using System.Linq;
using TechagroSyncServices.Shared.Helpers;

namespace TechagroApiSync.Shared.Helpers
{
    public static class ImportFilterHelper
    {
        public static IReadOnlyList<T> FilterByAllowedCodes<T>(IReadOnlyList<T> source, IEnumerable<string> allowedCodes, Func<T, string> codeSelector, out IReadOnlyList<string> missingCodes)
        {
            // Codes compared the way they are saved in database, so a code longer than the column still matches its product
            var allowedSet = new HashSet<string>(allowedCodes.Select(ProductCodeAffix.Normalize), StringComparer.OrdinalIgnoreCase);
            var filtered = new List<T>();
            var foundSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in source)
            {
                var code = ProductCodeAffix.Normalize(codeSelector(item));

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
                        continue;
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