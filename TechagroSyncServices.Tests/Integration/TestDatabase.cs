using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TechagroSyncServices.Tests.Integration
{
    // Creates a fresh test database from Schema.sql and the production procedures from the SQL folder.
    // Integration tests run only when TECHAGRO_TEST_SERVER holds a connection string to SQL Server,
    // e.g. "Server=localhost\SQLEXPRESS;User Id=sa;Password=...;TrustServerCertificate=True".
    public static class TestDatabase
    {
        public const string ServerVariable = "TECHAGRO_TEST_SERVER";
        private const string DatabaseName = "TechagroSyncServicesTests";

        private static readonly Regex BatchSeparator = new Regex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        public static string ConnectionString { get; private set; }

        public static void Create()
        {
            var server = Environment.GetEnvironmentVariable(ServerVariable);
            if (string.IsNullOrWhiteSpace(server))
                Assert.Inconclusive($"Set {ServerVariable} environment variable to run integration tests.");

            Drop(server);
            Execute(ToDatabase(server, "master"), $"CREATE DATABASE [{DatabaseName}] COLLATE Polish_CI_AS");
            ConnectionString = ToDatabase(server, DatabaseName);

            var databaseFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Integration", "Database");
            RunScript(File.ReadAllText(Path.Combine(databaseFolder, "Schema.sql")));

            foreach (var procedureFile in Directory.GetFiles(Path.Combine(databaseFolder, "Procedures"), "*.sql"))
                RunScript(ToCreateOrAlter(File.ReadAllText(procedureFile)));
        }

        public static void Drop()
        {
            var server = Environment.GetEnvironmentVariable(ServerVariable);
            if (!string.IsNullOrWhiteSpace(server))
                Drop(server);
        }

        public static void Execute(string sql) => Execute(ConnectionString, sql);

        public static List<string> Query(string sql)
        {
            var result = new List<string>();
            using (var connection = new SqlConnection(ConnectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        result.Add(Convert.ToString(reader[0]));
                }
            }
            return result;
        }

        public static int Count(string sql) => int.Parse(Query(sql).Single());

        private static void Drop(string server)
        {
            Execute(ToDatabase(server, "master"),
                $"IF DB_ID('{DatabaseName}') IS NOT NULL BEGIN ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}]; END");
        }

        // Production scripts are written for the TECHAGRO database with ALTER/CREATE
        private static string ToCreateOrAlter(string script)
        {
            script = Regex.Replace(script, @"^\s*USE\s+\[?\w+\]?\s*$", string.Empty, RegexOptions.Multiline | RegexOptions.IgnoreCase);
            return Regex.Replace(script, @"\b(CREATE\s+OR\s+ALTER|ALTER|CREATE)\s+PROCEDURE", "CREATE OR ALTER PROCEDURE", RegexOptions.IgnoreCase);
        }

        private static void RunScript(string script)
        {
            foreach (var batch in BatchSeparator.Split(script).Where(b => !string.IsNullOrWhiteSpace(b)))
                Execute(ConnectionString, batch);
        }

        private static void Execute(string connectionString, string sql)
        {
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        private static string ToDatabase(string server, string database)
        {
            return new SqlConnectionStringBuilder(server) { InitialCatalog = database }.ConnectionString;
        }
    }
}
