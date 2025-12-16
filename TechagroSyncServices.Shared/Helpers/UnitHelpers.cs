namespace TechagroApiSync.Shared.Helpers
{
    public static class UnitHelpers
    {
        public static string MapUnitLabel(string unitLabel)
        {
            if (string.IsNullOrWhiteSpace(unitLabel))
                return "szt.";
            unitLabel = unitLabel.ToLower();

            switch (unitLabel)
            {
                case "piece":
                case "sztuka":
                    return "szt.";

                case "kilogram":
                case "kg":
                    return "kg";

                case "gram":
                case "g":
                    return "g";

                case "litr":
                case "litre":
                case "l":
                    return "litr";

                case "meter":
                case "mb":
                case "m":
                    return "mb";

                case "pack":
                case "package":
                case "opakowanie":
                case "paczka":
                    return "opak.";

                case "komplet":
                    return "kpl.";

                default:
                    return "szt.";
            }
        }
    }
}