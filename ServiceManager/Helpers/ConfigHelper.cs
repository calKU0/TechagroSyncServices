using ServiceManager.Models;
using System.Data.Common;

namespace ServiceManager.Helpers
{
    public static class ConfigHelper
    {
        public static readonly List<ConfigField> AllFields = new()
        {
            // Gaska API
            new ConfigField { Key = "GaskaApiBaseUrl", Label = "Adres API Gąska", Group = "Gąska API" , IsEnabled = false},
            new ConfigField { Key = "GaskaApiAcronym", Label = "Skrót Gąska", Group = "Gąska API" , IsEnabled = false},
            new ConfigField { Key = "GaskaApiPerson", Label = "Osoba kontaktowa", Group = "Gąska API" , IsEnabled = false},
            new ConfigField { Key = "GaskaApiPassword", Label = "Hasło", Group = "Gąska API", IsEnabled = false },
            new ConfigField { Key = "GaskaApiKey", Label = "Klucz API", Group = "Gąska API" , IsEnabled = false},
            new ConfigField { Key = "GaskaApiProductsPerPage", Label = "Produkty na stronę", Group = "Gąska API" , IsEnabled = false},
            new ConfigField { Key = "GaskaApiProductsInterval", Label = "Interwał pobierania produktów", Group = "Gąska API" , IsEnabled = false},
            new ConfigField { Key = "GaskaApiProductPerDay", Label = "Produkty dziennie", Group = "Gąska API" , IsEnabled = false},
            new ConfigField { Key = "GaskaApiProductInterval", Label = "Interwał produktów", Group = "Gąska API", IsEnabled = false},

            // Intercars API
            new ConfigField { Key = "IntercarsApiBaseUrl", Label = "Adres API Intercars", Group = "Intercars API" , IsEnabled = false},
            new ConfigField { Key = "IntercarsApiUsername", Label = "Użytkownik", Group = "Intercars API" , IsEnabled = false},
            new ConfigField { Key = "IntercarsApiPassword", Label = "Hasło", Group = "Intercars API" , IsEnabled = false},

            // Agroland API
            new ConfigField { Key = "AgrolandApiBaseUrl", Label = "Adres API Agroland", Group = "Agroland API" , IsEnabled = false},
            new ConfigField { Key = "AgrolandApiKey", Label = "Klucz API", Group = "Intercars API" , IsEnabled = false},

            // Amaparts API
            new ConfigField { Key = "ApiBaseUrl", Label = "Adres API Amaparts", Group = "Amaparts API" , IsEnabled = false},
            new ConfigField { Key = "ApiKey", Label = "Klucz API", Group = "Amaparts API" , IsEnabled = false},

            // Agrorami API
            new ConfigField { Key = "AgroramiApiBaseUrl", Label = "Adres API Agrorami", Group = "Agrorami API" , IsEnabled = false},
            new ConfigField { Key = "AgroramiLogin", Label = "Użytkownik", Group = "Agrorami API" , IsEnabled = false},
            new ConfigField { Key = "AgroramiPassword", Label = "Hasło", Group = "Agrorami API" , IsEnabled = false},

            // Rolmar API
            new ConfigField { Key = "RolmarApiBaseUrl", Label = "Adres API Rolmar", Group = "Rolmar API" , IsEnabled = false},
            new ConfigField { Key = "RolmarApiKey", Label = "Klucz API", Group = "Rolmar API" , IsEnabled = false},

            // Hermon API
            new ConfigField { Key = "HermonApiBaseUrl", Label = "Adres API Hermon", Group = "Hermon API" , IsEnabled = false},
            new ConfigField { Key = "HermonApiUsername", Label = "Użytkownik", Group = "Hermon API" , IsEnabled = false},
            new ConfigField { Key = "HermonApiPassword", Label = "Hasło", Group = "Hermon API" , IsEnabled = false},

            // Hermon FTP
            new ConfigField { Key = "HermonFtpIp", Label = "Adres FTP Hermon", Group = "Hermon FTP" , IsEnabled = false},
            new ConfigField { Key = "HermonFtpPort", Label = "Port", Group = "Hermon FTP" , IsEnabled = false},
            new ConfigField { Key = "HermonFtpUsername", Label = "Użytkownik", Group = "Hermon FTP" , IsEnabled = false},
            new ConfigField { Key = "HermonFtpPassword", Label = "Hasło", Group = "Hermon FTP" , IsEnabled = false},

            // Other settings
            new ConfigField { Key = "EmailsToNotify", Label = "Adresy email do powiadomień (rozdzielone średnikiem)", Group = "Inne ustawienia" },
            new ConfigField { Key = "LogsExpirationDays", Label = "Ilość dni zachowania logów", Group = "Inne ustawienia" },
            new ConfigField { Key = "FetchIntervalMinutes", Label = "Odświeżanie stanu/ceny (min)", Group = "Inne ustawienia" },
            new ConfigField { Key = "DefaultMargin", Label = "Podstawowa marża (%)", Group = "Inne ustawienia" },
        };

        public static List<MarginRange> ParseMarginRanges(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new List<MarginRange>();
            return value.Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(MarginRange.Parse)
                        .ToList();
        }

        public static string SerializeMarginRanges(IEnumerable<MarginRange> ranges)
        {
            return string.Join(";", ranges.Select(r => r.ToString()));
        }

        public static Dictionary<string, string> ParseConnectionString(string connString)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connString };
            return builder.Cast<KeyValuePair<string, object>>()
                          .ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "");
        }

        public static readonly HashSet<string> ExcludedConnectionStringKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "MultipleActiveResultSets"
        };

        public static readonly Dictionary<string, string> ConnectionStringKeyTranslations = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Data Source", "Serwer" },
            { "Server", "Serwer" },
            { "Initial Catalog", "Baza danych" },
            { "Database", "Baza danych" },
            { "User ID", "Użytkownik" },
            { "User", "Użytkownik" },
            { "Password", "Hasło" },
        };
    }
}