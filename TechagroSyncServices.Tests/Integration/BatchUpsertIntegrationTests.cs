using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Enums;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Repositories;
using TechagroSyncServices.Shared.Settings;

namespace TechagroSyncServices.Tests.Integration
{
    // Batch upsert must save products exactly like UpsertProduct, which stays as fallback for failed batches
    [TestClass]
    [TestCategory("Integration")]
    public class BatchUpsertIntegrationTests
    {
        private const string ArtykulColumns =
            "ID_MAGAZYNU, ID_KATEGORII, ID_CENY_DOM, VAT_ZAKUPU, VAT_SPRZEDAZY, NAZWA, NAZWA_ORYG, NAZWA2, STAN, ILOSC_EDYTOWANA, " +
            "OD_DOSTAWCOW, DO_ODBIORCOW, INDEKS_HANDLOWY, RODZAJ, CENA_ZAKUPU_BRUTTO, CENA_ZAKUPU_NETTO, OSTRZEZENIE, POKAZUJ_OSTRZEZENIE, " +
            "WAGA, JED_WAGI, PODLEGA_RABATOWANIU, NAZWA_INTRASTAT, PRODUCENT, ID_KATEGORII_TREE, UWAGI, DATA_ZM_CZ, LOKALIZACJA, SEMAFOR, " +
            "ID_JEDNOSTKI, ID_JEDNOSTKI_INTRASTAT, ID_JEDNOSTKI_REF, ID_JEDNOSTKI_SPRZ, ID_JEDNOSTKI_ZAK, POLE1, OPIS";

        private ProductRepository _repository;
        private ProductSyncService _syncService;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context) => TestDatabase.Create();

        [ClassCleanup]
        public static void ClassCleanup() => TestDatabase.Drop();

        [TestInitialize]
        public void Initialize()
        {
            TestDatabase.Execute("DELETE FROM ARTYKUL; DELETE FROM ARTYKUL_BLOB; DELETE FROM CENA_ARTYKULU; DELETE FROM KOD_KRESKOWY;");
            _repository = new ProductRepository(TestDatabase.ConnectionString);
            _syncService = new ProductSyncService(_repository, null, new DeleteSafetySettings
            {
                MaxMissingProductsPercent = 100m,
                MissingRunsBeforeDelete = 1,
                MissingProductsFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName() + ".json")
            });
        }

        private static ProductDto FullProduct(string code, string ean) => new ProductDto
        {
            Id = 77,
            Code = code,
            TradingCode = "6204-2RS",
            Name = "Łożysko kulkowe zamknięte dwustronnie 6204 2RS z nazwą dłuższą niż czterdzieści znaków",
            Ean = ean,
            Quantity = 7.5m,
            NetBuyPrice = 10.12345m,
            GrossBuyPrice = 12.45m,
            NetSellPriceB = 13.5m,
            GrossSellPriceB = 16.61m,
            NetSellPriceC = 12m,
            GrossSellPriceC = 14.76m,
            Vat = 23,
            Weight = 0.25m,
            Brand = "SKF",
            CategoriesString = "Łożyska|Łożyska kulkowe",
            Unit = "kg",
            Description = "<p>Opis</p>",
            IntegrationCompany = IntegrationCompany.HIFI
        };

        private static async Task<int[]> Batch(params ProductDto[] products) =>
            await new ProductRepository(TestDatabase.ConnectionString).UpsertProductsAsync(products.Select(p => (p, p.Description)).ToList());

        private async Task Single(ProductDto product)
        {
            await _repository.UpsertProductAsync(product);
            await _repository.UpdateProductDescriptionAsync(product.Code, product.Description);
        }

        private static int Differences(string singleCode, string batchCode)
        {
            string Rows(string code) => $"SELECT {ArtykulColumns} FROM ARTYKUL WHERE INDEKS_KATALOGOWY = '{code}'";
            string Prices(string code) =>
                "SELECT a.ID_MAGAZYNU, c.ID_CENY, c.SYM_WAL, c.PRZELICZNIK_WAL, c.OBL_WG_CEN_ZAK, c.CENA_NETTO, c.CENA_BRUTTO, c.UWAGI, c.AKT_CEN_PRZY_DOSTAWIE " +
                $"FROM CENA_ARTYKULU c JOIN ARTYKUL a ON a.ID_ARTYKULU = c.ID_ARTYKULU WHERE a.INDEKS_KATALOGOWY = '{code}'";
            string Barcodes(string code) =>
                "SELECT k.ID_MAGAZYNU, RIGHT(k.KOD_KRESKOWY, 3) AS Koncowka, k.ID_JEDNOSTKI, k.DOM_ILOSC, k.PRZENOS_OPIS " +
                $"FROM KOD_KRESKOWY k JOIN ARTYKUL a ON a.ID_ARTYKULU = k.ID_ARTYKULU AND a.ID_MAGAZYNU = k.ID_MAGAZYNU WHERE a.INDEKS_KATALOGOWY = '{code}'";

            int count = 0;
            foreach (var query in new System.Func<string, string>[] { Rows, Prices, Barcodes })
            {
                count += TestDatabase.Count($"SELECT COUNT(*) FROM ({query(singleCode)} EXCEPT {query(batchCode)}) d");
                count += TestDatabase.Count($"SELECT COUNT(*) FROM ({query(batchCode)} EXCEPT {query(singleCode)}) d");
            }
            return count;
        }

        [TestMethod]
        public async Task BatchUpsert_SavesSameDataAsSingleUpsert()
        {
            // Insert
            await Single(FullProduct("S1HF", "5900000000111,5900000000112"));
            await Batch(FullProduct("B1HF", "5900000001111,5900000001112"));
            Assert.AreEqual(0, Differences("S1HF", "B1HF"));
            Assert.AreEqual(4, TestDatabase.Count("SELECT COUNT(*) FROM KOD_KRESKOWY k JOIN ARTYKUL a ON a.ID_ARTYKULU = k.ID_ARTYKULU WHERE a.INDEKS_KATALOGOWY = 'B1HF'"));

            // Update with new data
            var single = FullProduct("S1HF", "5900000000111");
            var batch = FullProduct("B1HF", "5900000001111");
            foreach (var p in new[] { single, batch })
            {
                p.Name = "Nowa nazwa";
                p.Quantity = 1;
                p.NetSellPriceB = 20m;
                p.Weight = 1m;
                p.Description = "<p>Nowy opis</p>";
            }
            await Single(single);
            await Batch(batch);
            Assert.AreEqual(0, Differences("S1HF", "B1HF"));

            // Product in every warehouse points to the first one created
            Assert.AreEqual(2, TestDatabase.Count(
                "SELECT COUNT(*) FROM ARTYKUL a WHERE INDEKS_KATALOGOWY = 'B1HF' AND ID_ARTYKULU_Prod = (SELECT MIN(ID_ARTYKULU) FROM ARTYKUL WHERE INDEKS_KATALOGOWY = 'B1HF')"));
            Assert.AreEqual(0, TestDatabase.Count("SELECT COUNT(*) FROM ARTYKUL WHERE INDEKS_KATALOGOWY = 'B1HF' AND ID_MAGAZYNU = 1 AND STAN <> 0"));
        }

        [TestMethod]
        public async Task BatchUpsert_ReportsInsertedUpdatedAndUnchangedProducts()
        {
            var product = FullProduct("P1HF", "5900000000111");

            Assert.AreEqual(1, (await Batch(product))[0]);
            Assert.AreEqual(0, (await Batch(product))[0]);

            product.Quantity = 3;
            Assert.AreEqual(2, (await Batch(product))[0]);

            product.NetSellPriceC = 99m;
            Assert.AreEqual(2, (await Batch(product))[0]);

            // Change of letter case is a change too
            product.Name = product.Name.ToUpper();
            Assert.AreEqual(2, (await Batch(product))[0]);
            Assert.AreEqual(0, (await Batch(product))[0]);
        }

        [TestMethod]
        public async Task WrongProductInBatch_OtherProductsAreSaved()
        {
            var wrong = FullProduct("BADHF", null);
            wrong.NetBuyPrice = 1e20m; // does not fit in DECIMAL(18,4)

            await _syncService.SyncToDatabaseAsync(new List<ProductDto> { FullProduct("OK1HF", null), wrong, FullProduct("OK2HF", null) });

            CollectionAssert.AreEquivalent(new[] { "OK1HF", "OK2HF" },
                TestDatabase.Query("SELECT DISTINCT INDEKS_KATALOGOWY FROM ARTYKUL").ToArray());
            Assert.AreEqual(2, TestDatabase.Count("SELECT COUNT(*) FROM ARTYKUL WHERE INDEKS_KATALOGOWY = 'OK1HF' AND OPIS LIKE '%Opis%'"));
        }

        [TestMethod]
        public async Task RepeatedCodeAndBarcodeInBatch_AreSavedOnce()
        {
            var first = FullProduct("P1HF", "5900000000111");
            var repeated = FullProduct("P1HF", "5900000000111");
            repeated.Quantity = 99;
            var sharedBarcode = FullProduct("P2HF", "5900000000111,5900000000222");

            var results = await Batch(first, repeated, sharedBarcode);

            CollectionAssert.AreEqual(new[] { 1, 1, 1 }, results);

            Assert.AreEqual(1, TestDatabase.Count("SELECT COUNT(*) FROM ARTYKUL WHERE INDEKS_KATALOGOWY = 'P1HF' AND ID_MAGAZYNU = 6 AND STAN = 99"));
            Assert.AreEqual(4, TestDatabase.Count("SELECT COUNT(*) FROM ARTYKUL"));
            Assert.AreEqual(2, TestDatabase.Count("SELECT COUNT(*) FROM KOD_KRESKOWY WHERE KOD_KRESKOWY = '5900000000111'"));
            Assert.AreEqual(2, TestDatabase.Count(
                "SELECT COUNT(*) FROM KOD_KRESKOWY k JOIN ARTYKUL a ON a.ID_ARTYKULU = k.ID_ARTYKULU WHERE a.INDEKS_KATALOGOWY = 'P2HF'"));
        }

        [TestMethod]
        public async Task ManyBatches_SaveEveryProduct()
        {
            var products = Enumerable.Range(1, 1203).Select(i => FullProduct($"P{i}HF", $"59{i:D11}")).ToList();

            await _syncService.SyncToDatabaseAsync(products);

            Assert.AreEqual(2406, TestDatabase.Count("SELECT COUNT(*) FROM ARTYKUL"));
            Assert.AreEqual(2406, TestDatabase.Count("SELECT COUNT(DISTINCT ID_ARTYKULU) FROM ARTYKUL"));
            Assert.AreEqual(4812, TestDatabase.Count("SELECT COUNT(*) FROM CENA_ARTYKULU"));
        }

        [TestMethod]
        public async Task SalePrices_AreRoundedLikeInWapro()
        {
            var product = FullProduct("P1HF", null);
            product.NetBuyPrice = 18.71m;
            product.GrossBuyPrice = 18.71m * 1.23m;
            product.NetSellPriceB = 18.71m * 1.25m;
            product.GrossSellPriceB = 18.71m * 1.23m * 1.25m;

            await _syncService.SyncToDatabaseAsync(new List<ProductDto> { product });
            var tenGroszy = new ProductSyncService(_repository, null, null, 0.10m);
            var other = FullProduct("P2HF", null);
            other.NetSellPriceB = 23.3875m;
            other.GrossSellPriceB = 28.766625m;
            await tenGroszy.SyncToDatabaseAsync(new List<ProductDto> { other });

            // Purchase prices are not rounded
            Assert.AreEqual(2, TestDatabase.Count("SELECT COUNT(*) FROM ARTYKUL WHERE INDEKS_KATALOGOWY = 'P1HF' AND CENA_ZAKUPU_NETTO = 18.71 AND CENA_ZAKUPU_BRUTTO = 23.0133"));
            Assert.AreEqual(1, TestDatabase.Count("SELECT COUNT(*) FROM CENA_ARTYKULU c JOIN ARTYKUL a ON a.ID_ARTYKULU = c.ID_ARTYKULU WHERE a.INDEKS_KATALOGOWY = 'P1HF' AND c.ID_CENY = 2 AND c.CENA_NETTO = 23.39 AND c.CENA_BRUTTO = 28.77"));
            Assert.AreEqual(1, TestDatabase.Count("SELECT COUNT(*) FROM CENA_ARTYKULU c JOIN ARTYKUL a ON a.ID_ARTYKULU = c.ID_ARTYKULU WHERE a.INDEKS_KATALOGOWY = 'P2HF' AND c.ID_CENY = 2 AND c.CENA_NETTO = 23.40 AND c.CENA_BRUTTO = 28.80"));

            // Rounded prices are what the next run compares with, so the same supplier price is no change
            var rounded = FullProduct("P2HF", null);
            rounded.NetSellPriceB = 23.40m;
            rounded.GrossSellPriceB = 28.80m;
            rounded.GrossSellPriceC = 14.80m;
            rounded.Description = null;
            Assert.AreEqual(0, (await Batch(rounded))[0]);
        }

        // Copy of the shop integrator trigger from production database, which registers only one new product per statement
        private const string IntegratorTrigger =
            "CREATE TRIGGER dbo._INTEGRATOR_CENA_ZMIANA_TRIG ON dbo.CENA_ARTYKULU AFTER INSERT, UPDATE, DELETE AS BEGIN " +
            "SET NOCOUNT ON; IF ((SELECT TRIGGER_NESTLEVEL()) > 1) RETURN; " +
            "INSERT INTO dbo._integrator_artykul_zmiana (id_obiektu, do_przetworzenia, przetwarzane_przez, id_sklepu) " +
            "select top 1 i.ID_ARTYKULU, 0, 0, '' FROM Inserted i left join dbo._integrator_artykul_zmiana z WITH (NOLOCK) on i.ID_ARTYKULU = z.id_obiektu where z.id_obiektu is null; " +
            "update dbo._integrator_artykul_zmiana set do_przetworzenia = (do_przetworzenia | 16) FROM Inserted i inner join dbo._integrator_artykul_zmiana z WITH (NOLOCK) on i.ID_ARTYKULU = z.id_obiektu; " +
            "IF EXISTS (SELECT 1 FROM DELETED) update dbo._integrator_artykul_zmiana set do_przetworzenia = (do_przetworzenia | 16) FROM DELETED i inner join dbo._integrator_artykul_zmiana z WITH (NOLOCK) on i.ID_ARTYKULU = z.id_obiektu; " +
            "END";

        [TestMethod]
        public async Task ShopIntegrator_GetsEveryProductWithChangedPrices()
        {
            TestDatabase.Execute("CREATE TABLE dbo._integrator_artykul_zmiana (id_obiektu INT NOT NULL, do_przetworzenia INT NOT NULL, przetwarzane_przez INT NOT NULL, id_sklepu VARCHAR(50) NULL)");
            TestDatabase.Execute(IntegratorTrigger);

            try
            {
                var products = Enumerable.Range(1, 30).Select(i => FullProduct($"P{i}HF", null)).ToList();
                await _syncService.SyncToDatabaseAsync(products);

                // Every new product in both warehouses, like when prices were saved one by one
                Assert.AreEqual(60, TestDatabase.Count("SELECT COUNT(*) FROM _integrator_artykul_zmiana WHERE do_przetworzenia = 16 AND przetwarzane_przez = 0 AND id_sklepu = ''"));
                Assert.AreEqual(0, TestDatabase.Count("SELECT COUNT(*) FROM ARTYKUL a WHERE NOT EXISTS (SELECT 1 FROM _integrator_artykul_zmiana z WHERE z.id_obiektu = a.ID_ARTYKULU)"));
                Assert.AreEqual(60, TestDatabase.Count("SELECT COUNT(DISTINCT id_obiektu) FROM _integrator_artykul_zmiana"));

                // Integrator processed changes, then price of one product changed
                TestDatabase.Execute("UPDATE _integrator_artykul_zmiana SET do_przetworzenia = 0");
                products[4].NetSellPriceB += 1;
                await _syncService.SyncToDatabaseAsync(products);

                Assert.AreEqual(2, TestDatabase.Count(
                    "SELECT COUNT(*) FROM _integrator_artykul_zmiana z JOIN ARTYKUL a ON a.ID_ARTYKULU = z.id_obiektu WHERE z.do_przetworzenia = 16 AND a.INDEKS_KATALOGOWY = 'P5HF'"));
                Assert.AreEqual(2, TestDatabase.Count("SELECT COUNT(*) FROM _integrator_artykul_zmiana WHERE do_przetworzenia <> 0"));
                Assert.AreEqual(60, TestDatabase.Count("SELECT COUNT(*) FROM _integrator_artykul_zmiana"));
            }
            finally
            {
                TestDatabase.Execute("DROP TRIGGER dbo._INTEGRATOR_CENA_ZMIANA_TRIG; DROP TABLE dbo._integrator_artykul_zmiana;");
            }
        }

        [TestMethod]
        public async Task ProductThatCannotBeDeleted_DoesNotStopDeletingOthers()
        {
            var products = Enumerable.Range(1, 620).Select(i => FullProduct($"P{i}HF", null)).ToList();
            await _syncService.SyncToDatabaseAsync(products);
            // Like a product used in documents
            TestDatabase.Execute(
                "CREATE TRIGGER TR_BlockDelete ON ARTYKUL AFTER DELETE AS " +
                "IF EXISTS (SELECT 1 FROM deleted WHERE INDEKS_KATALOGOWY = 'P7HF') THROW 50000, 'Product is used in documents', 1;");

            try
            {
                await _syncService.DeleteNotSyncedProducts(IntegrationCompany.HIFI, new[] { "P1HF" }, new[] { "P1HF" }, true);
            }
            finally
            {
                TestDatabase.Execute("DROP TRIGGER TR_BlockDelete");
            }

            CollectionAssert.AreEquivalent(new[] { "P1HF", "P7HF" }, TestDatabase.Query("SELECT DISTINCT INDEKS_KATALOGOWY FROM ARTYKUL").ToArray());
            Assert.AreEqual(4, TestDatabase.Count("SELECT COUNT(*) FROM CENA_ARTYKULU c JOIN ARTYKUL a ON a.ID_ARTYKULU = c.ID_ARTYKULU WHERE a.INDEKS_KATALOGOWY = 'P7HF'"));
            Assert.AreEqual(8, TestDatabase.Count("SELECT COUNT(*) FROM CENA_ARTYKULU"));
        }
    }
}
