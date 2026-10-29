using KrampSyncService.Clients;
using KrampSyncService.Services;
using KrampSyncService.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace TechagroSyncServices.Tests.Unit
{
    // Kramp refreshes prices in parts because of the daily limit, so only SKUs confirmed as missing can be deleted
    [TestClass]
    public class KrampMissingSkusTests
    {
        private string _directory;

        [TestInitialize]
        public void Initialize()
        {
            _directory = Path.Combine(Path.GetTempPath(), "KrampTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TestCleanup]
        public void Cleanup() => Directory.Delete(_directory, true);

        [TestMethod]
        public async Task OnlySkusAnsweredWithoutPrice_AreMissing()
        {
            var now = DateTime.Now.ToString("s");
            var statesPath = Path.Combine(_directory, "products-state.json");
            File.WriteAllText(statesPath,
                "[{\"Sku\":\"K1\",\"NetPrice\":10,\"Stock\":5,\"PriceUpdatedAt\":\"" + now + "\"}," +
                "{\"Sku\":\"K2\",\"NetPrice\":null,\"PriceUpdatedAt\":\"" + now + "\"}," +
                "{\"Sku\":\"K3\"}]");

            // Daily limit 0 - no requests are sent, only saved states are used
            var settings = new KrampApiSettings { BaseUrl = "https://example.invalid/", DailyRequestsLimit = 0, PricesBatchSize = 25, AtpBatchSize = 20 };
            var limiter = new RequestLimiter(settings, Path.Combine(_directory, "requests-state.json"));
            var syncService = new SyncService(new KrampApiClient(settings, limiter), limiter, settings, statesPath);

            await syncService.RefreshProductsAsync(new List<string> { "K1", "K2", "k3" });

            CollectionAssert.AreEqual(new[] { "K2" }, syncService.GetMissingSkus());
        }
    }
}
