using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgroramiSyncService.Helpers
{
    public class FlexibleDateTimeConverter : JsonConverter<DateTime?>
    {
        private readonly string[] formats = new[]
        {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm:ssZ"
            };

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();

            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (DateTime.TryParseExact(
                value,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var dt))
            {
                return dt;
            }

            if (DateTime.TryParse(value, out dt))
                return dt;

            return null; // fail-safe instead of crash
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            else
                writer.WriteNullValue();
        }
    }
}

