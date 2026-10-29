using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Enums;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Repositories;
using TechagroSyncServices.Shared.Settings;

namespace TechagroSyncServices.Tests.Integration
{
    // Runs the production stored procedures on a fresh test database
    [TestClass]
    [TestCategory("Integration")]
    public class ProductRepositoryIntegrationTests
    {
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
            // Products are deleted after the first miss here, waiting for more runs is covered by unit tests
            _syncService = new ProductSyncService(_repository, null, new DeleteSafetySettings
            {
                MaxMissingProductsPercent = 20m,
                MissingRunsBeforeDelete = 1,
                MissingProductsFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName() + ".json")
            });
        }

        private async Task Seed(IntegrationCompany company, params string[] codes)
        {
            foreach (var code in codes)
            {
                await _repository.UpsertProductAsync(new ProductDto
                {
                    Code = code,
                    TradingCode = code,
                    Name = code,
                    Ean = "EAN-" + code,
                    Quantity = 1,
                    NetBuyPrice = 10m,
                    GrossBuyPrice = 12.3m,
                    NetSellPriceB = 12.5m,
                    GrossSellPriceB = 15.38m,
                    Vat = 23,
                    IntegrationCompany = company
                });
            }
        }

        private static string[] Codes(IntegrationCompany company) =>
            TestDatabase.Query($"SELECT DISTINCT INDEKS_KATALOGOWY FROM ARTYKUL WHERE POLE1 LIKE '{company}%'").ToArray();

        private static int OrphanRows() =>
            TestDatabase.Count("SELECT COUNT(*) FROM CENA_ARTYKULU c WHERE NOT EXISTS (SELECT 1 FROM ARTYKUL a WHERE a.ID_ARTYKULU = c.ID_ARTYKULU)")
            + TestDatabase.Count("SELECT COUNT(*) FROM KOD_KRESKOWY k WHERE NOT EXISTS (SELECT 1 FROM ARTYKUL a WHERE a.ID_ARTYKULU = k.ID_ARTYKULU)")
            + TestDatabase.Count("SELECT COUNT(*) FROM ARTYKUL_BLOB b WHERE NOT EXISTS (SELECT 1 FROM ARTYKUL a WHERE a.INDEKS_KATALOGOWY = b.INDEKS_KATALOGOWY)");

        [TestMethod]
        public async Task Upsert_CreatesProductInBothWarehousesWithPricesAndBarcode()
        {
            await Seed(IntegrationCompany.HIFI, "SN 001HF");

            Assert.AreEqual(2, TestDatabase.Count("SELECT COUNT(*) FROM ARTYKUL WHERE INDEKS_KATALOGOWY = 'SN 001HF'"));
            Assert.AreEqual(2, TestDatabase.Count("SELECT COUNT(*) FROM CENA_ARTYKULU"));
            Assert.AreEqual(2, TestDatabase.Count("SELECT COUNT(*) FROM KOD_KRESKOWY"));
        }

        [TestMethod]
        public async Task GetProductCodes_ReturnsDistinctCodesOfCompany()
        {
            await Seed(IntegrationCompany.HIFI, "P1HF", "P2HF");
            await Seed(IntegrationCompany.KRAMP, "K1KR");

            var codes = await _repository.GetProductCodesAsync(IntegrationCompany.HIFI);

            CollectionAssert.AreEquivalent(new[] { "P1HF", "P2HF" }, codes);
        }

        [TestMethod]
        public async Task ProductMissingInApi_IsDeletedWithImagesPricesAndBarcodes()
        {
            await Seed(IntegrationCompany.HIFI, "P1HF", "P2HF", "P3HF", "OLDHF");
            await Seed(IntegrationCompany.KRAMP, "K1KR");
            await _repository.UpsertProductImageAsync("P3HF", "image", new byte[] { 1, 2, 3 });

            // P3 no longer returned by API, OLD removed from the import file
            await _syncService.DeleteNotSyncedProducts(IntegrationCompany.HIFI, new[] { "P1HF", "P2HF", "P3HF" }, new[] { "P1HF", "P2HF" }, true);

            CollectionAssert.AreEquivalent(new[] { "P1HF", "P2HF" }, Codes(IntegrationCompany.HIFI));
            CollectionAssert.AreEquivalent(new[] { "K1KR" }, Codes(IntegrationCompany.KRAMP));
            Assert.AreEqual(0, OrphanRows());
            Assert.AreEqual(6, TestDatabase.Count("SELECT COUNT(*) FROM CENA_ARTYKULU"));
        }

        [TestMethod]
        public async Task IncompleteFetch_KeepsProductsFromImportList()
        {
            await Seed(IntegrationCompany.HIFI, "P1HF", "P2HF", "P3HF", "OLDHF");

            await _syncService.DeleteNotSyncedProducts(IntegrationCompany.HIFI, new[] { "P1HF", "P2HF", "P3HF" }, new[] { "P1HF" }, false);

            CollectionAssert.AreEquivalent(new[] { "P1HF", "P2HF", "P3HF" }, Codes(IntegrationCompany.HIFI));
        }

        [TestMethod]
        public async Task ExpressVariant_IsDeletedWithBaseProduct()
        {
            await Seed(IntegrationCompany.AGRORAMI, "SKU1AR", "SKU1AR-EXP", "SKU2AR", "SKU2AR-EXP");

            // Agrorami expects express variants that are in database
            await _syncService.DeleteNotSyncedProducts(IntegrationCompany.AGRORAMI, new[] { "SKU1AR", "SKU1AR-EXP" }, new[] { "SKU1AR", "SKU1AR-EXP" }, true);

            CollectionAssert.AreEquivalent(new[] { "SKU1AR", "SKU1AR-EXP" }, Codes(IntegrationCompany.AGRORAMI));
        }

        [TestMethod]
        public async Task ExpressVariantNoLongerOffered_IsDeleted()
        {
            await Seed(IntegrationCompany.AGRORAMI, "SKU1AR", "SKU1AR-EXP");

            await _syncService.DeleteNotSyncedProducts(IntegrationCompany.AGRORAMI, new[] { "SKU1AR", "SKU1AR-EXP" }, new[] { "SKU1AR" }, true);

            CollectionAssert.AreEquivalent(new[] { "SKU1AR" }, Codes(IntegrationCompany.AGRORAMI));
        }

        [TestMethod]
        public async Task ArticlesOfOtherSuppliersOrCreatedByHand_AreNeverDeleted()
        {
            await Seed(IntegrationCompany.HIFI, "P1HF", "SAMEHF");
            await _repository.UpsertProductImageAsync("SAMEHF", "image", new byte[] { 1, 2, 3 });
            // Article with the same code created by hand in another warehouse
            TestDatabase.Execute("INSERT INTO ARTYKUL (ID_ARTYKULU, ID_MAGAZYNU, INDEKS_KATALOGOWY, NAZWA, POLE1) VALUES (999999, 3, 'SAMEHF', 'Reczny', NULL)");

            await _syncService.DeleteNotSyncedProducts(IntegrationCompany.HIFI, new[] { "P1HF" }, new[] { "P1HF" }, true);

            Assert.AreEqual(1, TestDatabase.Count("SELECT COUNT(*) FROM ARTYKUL WHERE INDEKS_KATALOGOWY = 'SAMEHF'"));
            Assert.AreEqual(1, TestDatabase.Count("SELECT COUNT(*) FROM ARTYKUL WHERE ID_ARTYKULU = 999999"));
            // Image is still used by the article created by hand
            Assert.AreEqual(1, TestDatabase.Count("SELECT COUNT(*) FROM ARTYKUL_BLOB WHERE INDEKS_KATALOGOWY = 'SAMEHF'"));
        }

        [TestMethod]
        public async Task LongCodeFromImportFile_IsNotDeleted()
        {
            // Code from import file is longer than the column - database keeps it cut to 20 characters
            await _repository.UpsertProductAsync(new ProductDto { Code = "ABCDEFGHIJKLMNOPQRSTUVWXYZ", Name = "Long", Vat = 23, IntegrationCompany = IntegrationCompany.AMA });

            await _syncService.DeleteNotSyncedProducts(IntegrationCompany.AMA, new[] { " ABCDEFGHIJKLMNOPQRSTUVWXYZ " }, new[] { "ABCDEFGHIJKLMNOPQRSTUVWXYZ" }, true);

            CollectionAssert.AreEquivalent(new[] { "ABCDEFGHIJKLMNOPQRST" }, Codes(IntegrationCompany.AMA));
        }

        [TestMethod]
        public async Task NoImportList_ProductsMissingInApiAreDeleted()
        {
            await Seed(IntegrationCompany.AGROLAND, "E1", "E2", "E3");

            await _syncService.DeleteNotSyncedProducts(IntegrationCompany.AGROLAND, null, new[] { "E1", "E2", "E4" }, true);

            CollectionAssert.AreEquivalent(new[] { "E1", "E2" }, Codes(IntegrationCompany.AGROLAND));
            Assert.AreEqual(0, OrphanRows());
        }

        [TestMethod]
        public async Task NoImportList_IncompleteFetch_DeletesNothing()
        {
            await Seed(IntegrationCompany.AGROLAND, "E1", "E2", "E3");

            await _syncService.DeleteNotSyncedProducts(IntegrationCompany.AGROLAND, null, new[] { "E1" }, false);

            CollectionAssert.AreEquivalent(new[] { "E1", "E2", "E3" }, Codes(IntegrationCompany.AGROLAND));
        }

        private static ProductDto CodeOnlyProduct(string code, string supplierCode, decimal price) => new ProductDto
        {
            // Like Kramp, Granit or HiFi - only prices and stock, the code is used as name
            Code = code,
            TradingCode = supplierCode,
            Name = supplierCode,
            Quantity = 3,
            NetBuyPrice = price,
            GrossBuyPrice = price * 1.23m,
            NetSellPriceB = price * 1.3m,
            GrossSellPriceB = price * 1.6m,
            Vat = 23,
            IntegrationCompany = IntegrationCompany.HIFI
        };

        private const string ManualData =
            "UPDATE ARTYKUL SET NAZWA = 'Filtr oleju', NAZWA_ORYG = 'Filtr oleju', WAGA = 1.5, PRODUCENT = 'HIFI', " +
            "UWAGI = 'Filtry', OPIS = 'Opis reczny', KOD_KRESKOWY = '5901234567890' WHERE INDEKS_KATALOGOWY = 'SN 001HF'";

        [TestMethod]
        public async Task ManuallyFilledData_IsKept_WhenSupplierHasNoProductData()
        {
            await _syncService.SyncToDatabaseAsync(new[] { CodeOnlyProduct("SN 001HF", "SN 001", 10m) }.ToList());
            TestDatabase.Execute(ManualData);

            await _syncService.SyncToDatabaseAsync(new[] { CodeOnlyProduct("SN 001HF", "SN 001", 20m) }.ToList());

            const string kept = "SELECT COUNT(*) FROM ARTYKUL WHERE INDEKS_KATALOGOWY = 'SN 001HF' AND NAZWA = 'Filtr oleju' AND NAZWA_ORYG = 'Filtr oleju' " +
                "AND WAGA = 1.5 AND PRODUCENT = 'HIFI' AND UWAGI = 'Filtry' AND OPIS = 'Opis reczny' AND KOD_KRESKOWY = '5901234567890'";
            Assert.AreEqual(2, TestDatabase.Count(kept));
            Assert.AreEqual(2, TestDatabase.Count("SELECT COUNT(*) FROM ARTYKUL WHERE INDEKS_KATALOGOWY = 'SN 001HF' AND CENA_ZAKUPU_NETTO = 20"));
        }

        [TestMethod]
        public async Task ProductDataFromSupplier_OverwritesDatabase()
        {
            await _syncService.SyncToDatabaseAsync(new[] { CodeOnlyProduct("SN 001HF", "SN 001", 10m) }.ToList());
            TestDatabase.Execute(ManualData);

            var product = CodeOnlyProduct("SN 001HF", "SN 001", 10m);
            product.Name = "Filtr powietrza";
            product.Weight = 2m;
            product.Brand = "HIFI FILTER";
            product.CategoriesString = "Filtry powietrza";
            product.Description = "Opis dostawcy";
            await _syncService.SyncToDatabaseAsync(new[] { product }.ToList());

            const string updated = "SELECT COUNT(*) FROM ARTYKUL WHERE INDEKS_KATALOGOWY = 'SN 001HF' AND NAZWA = 'Filtr powietrza' " +
                "AND WAGA = 2 AND PRODUCENT = 'HIFI FILTER' AND UWAGI = 'Filtry powietrza' AND OPIS LIKE '%Opis dostawcy%'";
            Assert.AreEqual(2, TestDatabase.Count(updated));
        }

        [TestMethod]
        public async Task NewProductWithoutProductData_GetsCodeAsName()
        {
            await _syncService.SyncToDatabaseAsync(new[] { CodeOnlyProduct("SN 001HF", "SN 001", 10m) }.ToList());

            Assert.AreEqual(2, TestDatabase.Count("SELECT COUNT(*) FROM ARTYKUL WHERE INDEKS_KATALOGOWY = 'SN 001HF' AND NAZWA = 'SN 001' AND WAGA = 0"));
        }
    }
}
