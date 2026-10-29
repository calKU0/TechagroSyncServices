using System;

namespace TechagroSyncServices.Shared.Helpers
{
    // Supplier prefix, e.g. "123" -> "RL123"
    public sealed class ProductCodePrefix : ProductCodeAffix
    {
        public ProductCodePrefix(string value) : base(value)
        {
        }

        public override bool IsPresentIn(string code)
        {
            return !string.IsNullOrEmpty(code)
                && code.Length > Value.Length
                && code.StartsWith(Value, StringComparison.OrdinalIgnoreCase);
        }

        public override string Add(string supplierCode) => Value + (supplierCode ?? string.Empty);

        public override string Remove(string code) => IsPresentIn(code) ? code.Substring(Value.Length) : code;
    }
}
