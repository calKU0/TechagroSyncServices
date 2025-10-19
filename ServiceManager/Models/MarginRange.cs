namespace ServiceManager.Models
{
    public class MarginRange
    {
        public decimal Min { get; set; }
        public decimal Max { get; set; }
        public decimal Margin { get; set; }

        public override string ToString() => $"{Min}-{Max}: {Margin}";

        public static MarginRange Parse(string s)
        {
            var parts = s.Split(':');
            var range = parts[0].Split('-');
            return new MarginRange
            {
                Min = decimal.Parse(range[0]),
                Max = decimal.Parse(range[1]),
                Margin = decimal.Parse(parts[1])
            };
        }
    }
}