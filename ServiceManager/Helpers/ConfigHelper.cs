using ServiceManager.Models;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            // Other settings
            new ConfigField { Key = "LogsExpirationDays", Label = "Ilość dni zachowania logów", Group = "Inne ustawienia" },
            new ConfigField { Key = "FetchIntervalMinutes", Label = "Odświeżanie stanu/ceny (min)", Group = "Inne ustawienia" },
            new ConfigField { Key = "Margin%", Label = "Własna marża (%)", Group = "Inne ustawienia" },
        };

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