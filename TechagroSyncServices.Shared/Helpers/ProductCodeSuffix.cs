using System;

namespace TechagroSyncServices.Shared.Helpers
{
    // Supplier suffix, e.g. "SN 001" -> "SN 001HF"
    public sealed class ProductCodeSuffix : ProductCodeAffix
    {
        public ProductCodeSuffix(string value) : base(value)
        {
        }

        public override bool IsPresentIn(string code)
        {
            return !string.IsNullOrEmpty(code)
                && code.Length > Value.Length
                && code.EndsWith(Value, StringComparison.OrdinalIgnoreCase);
        }

        public override string Add(string supplierCode) => (supplierCode ?? string.Empty) + Value;

        public override string Remove(string code) => IsPresentIn(code) ? code.Substring(0, code.Length - Value.Length) : code;
    }
}
