using System;
using System.Collections.Generic;
using TechagroSyncServices.Shared.DTOs;

namespace TechagroApiSync.Shared.Helpers
{
    public static class MarginHelper
    {
        public static decimal CalculateMargin(decimal price, decimal defaultMargin, List<MarginRange> ranges)
        {
            foreach (var range in ranges)
            {
                if (price >= range.Min && price < range.Max)
                    return range.Margin;
            }
            return defaultMargin;
        }

        public static List<MarginRange> ParseMarginRanges(string raw)
        {
            var list = new List<MarginRange>();

            // Poprawka: użyj tablicy znaków zamiast pojedynczego chara
            var entries = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var entry in entries)
            {
                var parts = entry.Split(':');
                if (parts.Length != 2) continue;

                // Poprawka: użyj tablicy znaków zamiast pojedynczego chara
                var rangeParts = parts[0].Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                if (rangeParts.Length != 2) continue;

                if (decimal.TryParse(rangeParts[0], out decimal min) &&
                    decimal.TryParse(rangeParts[1], out decimal max) &&
                    decimal.TryParse(parts[1], out decimal margin))
                {
                    list.Add(new MarginRange { Min = min, Max = max, Margin = margin });
                }
            }

            return list;
        }
    }
}