using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Helpers;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;
using TechagroSyncServices.Shared.Services;

namespace TechagroSyncServices.Tests.Unit
{
    // E-mail errors must never stop synchronization and new products must not be lost
    [TestClass]
    [DoNotParallelize]
    public class NewProductsNotifierTests
    {
        private static readonly string ExportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Export");
        private static readonly string SnapshotPath = Path.Combine(ExportPath, "products.json");

        private FakeEmailService _email;

        [TestInitialize]
        public void Initialize()
        {
            if (Directory.Exists(ExportPath))
                Directory.Delete(ExportPath, true);

            _email = new FakeEmailService();
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(ExportPath))
                Directory.Delete(ExportPath, true);
        }

        private static List<ProductDto> Products(params string[] codes) => codes.Select(c => new ProductDto { Code = c, Name = "Product " + c }).ToList();

        private static async Task SaveSnapshot(params string[] codes) => await SnapshotChangeDetector.SaveSnapshotAsync(SnapshotPath, Products(codes));

        private static async Task<HashSet<string>> SnapshotCodes() => await SnapshotChangeDetector.LoadCodesFromSnapshotAsync(SnapshotPath);

        [TestMethod]
        public async Task NewProducts_AreSentAndSnapshotIsSaved()
        {
            await SaveSnapshot("A");

            bool done = await new NewProductsNotifier(_email, "Test").DetectAndNotifyAsync(Products("A", "B"), true);

            Assert.IsTrue(done);
            Assert.AreEqual(1, _email.Sent);
            CollectionAssert.AreEquivalent(new[] { "A", "B" }, (await SnapshotCodes()).ToArray());
        }

        [TestMethod]
        public async Task EmailError_IsNotThrown_AndProductsAreReportedAgain()
        {
            await SaveSnapshot("A");
            _email.Fail = true;

            bool done = await new NewProductsNotifier(_email, "Test").DetectAndNotifyAsync(Products("A", "B"), true);

            Assert.IsFalse(done);
            CollectionAssert.AreEquivalent(new[] { "A" }, (await SnapshotCodes()).ToArray());

            _email.Fail = false;
            Assert.IsTrue(await new NewProductsNotifier(_email, "Test").DetectAndNotifyAsync(Products("A", "B"), true));
            Assert.AreEqual(1, _email.Sent);
        }

        [TestMethod]
        public async Task FirstRun_SavesSnapshotWithoutEmail()
        {
            bool done = await new NewProductsNotifier(_email, "Test").DetectAndNotifyAsync(Products("A", "B"), true);

            Assert.IsTrue(done);
            Assert.AreEqual(0, _email.Sent);
            Assert.HasCount(2, await SnapshotCodes());
        }

        [TestMethod]
        public async Task TooManyNewProducts_AreNotSent()
        {
            await SaveSnapshot("A");

            bool done = await new NewProductsNotifier(_email, "Test", maxNewProducts: 1).DetectAndNotifyAsync(Products("A", "B", "C"), true);

            Assert.IsTrue(done);
            Assert.AreEqual(0, _email.Sent);
            // Not e-mailed, but still listed in the CSV file
            Assert.HasCount(1, Directory.GetFiles(Path.Combine(ExportPath, "Nowe"), "*.csv"));
        }

        [TestMethod]
        public async Task IncompleteFetch_IsNotComparedNorSaved()
        {
            await SaveSnapshot("A", "B", "C");

            bool done = await new NewProductsNotifier(_email, "Test").DetectAndNotifyAsync(Products("A", "D"), false);

            Assert.IsFalse(done);
            Assert.AreEqual(0, _email.Sent);
            CollectionAssert.AreEquivalent(new[] { "A", "B", "C" }, (await SnapshotCodes()).ToArray());
        }

        private class FakeEmailService : IEmailService
        {
            public bool Fail { get; set; }
            public int Sent { get; private set; }

            public Task SendEmailAsync(string to, string from, string subject, string htmlBody)
            {
                if (Fail)
                    throw new InvalidOperationException("SMTP server unavailable");

                Sent++;
                return Task.CompletedTask;
            }
        }
    }
}
