using System;

namespace TechagroSyncServices.Shared.Helpers
{
    // Supplier marker added to catalogue codes saved in database, so codes of different suppliers never collide.
    // Import file can contain codes with or without the marker - they are always compared without it.
    public abstract class ProductCodeAffix
    {
        // Artykul.Indeks_katalogowy is VARCHAR(20)
        public const int MaxCodeLength = 20;

        public string Value { get; }

        protected ProductCodeAffix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Affix cannot be empty.", nameof(value));

            Value = value;
        }

        // Code consisting only of the affix is treated as a plain supplier code
        public abstract bool IsPresentIn(string code);

        // Supplier code -> code with affix, without truncation
        public abstract string Add(string supplierCode);

        // Code with or without affix -> supplier code used in API requests
        public abstract string Remove(string code);

        // Supplier code -> code saved in database
        public string ToDatabaseCode(string supplierCode) => Truncate(Add(supplierCode));

        // Any code (from import file, API or database) the way it is saved in database - trimmed and cut to column length
        public static string Normalize(string code) => Truncate((code ?? string.Empty).Trim());

        public static string Truncate(string code)
        {
            if (code == null)
                return null;

            return code.Length > MaxCodeLength ? code.Substring(0, MaxCodeLength) : code;
        }
    }
}
