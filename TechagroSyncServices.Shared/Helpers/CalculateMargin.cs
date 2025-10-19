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
    }
}