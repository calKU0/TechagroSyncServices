using Serilog;
using System.Globalization;

namespace RolmarSyncService.Helpers
{
    public static class ConvertHelper
    {
        public static int SafeConvertToInt(string value, string fieldName, string productIndex)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out int result))
                return result;

            Log.Warning("Invalid int conversion for field '{Field}' in product '{ProductIndex}'. Value: '{Value}'", fieldName, productIndex, value);
            return 0;
        }

        public static decimal SafeConvertToDecimal(string value, string fieldName, string productIndex)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0m;

            // Normalize decimal separator
            value = value.Replace(',', '.');

            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
                return result;

            Log.Warning("Invalid decimal conversion for field '{Field}' in product '{ProductIndex}'. Value: '{Value}'", fieldName, productIndex, value);

            return 0m;
        }
    }
}