using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System.Globalization;

namespace RolnexSyncService.Helpers
{
    public class FlexibleDecimalConverter : DecimalConverter
    {
        public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0m;

            text = text.Trim();

            // normalize decimal separator
            text = text.Replace(",", ".");

            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                return value;

            throw new TypeConverterException(this, memberMapData, text, row.Context, $"Cannot convert '{text}' to decimal.");
        }
    }
}
