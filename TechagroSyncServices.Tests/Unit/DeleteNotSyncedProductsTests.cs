using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TechagroApiSync.Shared.Enums;
using TechagroApiSync.Shared.Services;
using TechagroSyncServices.Shared.Settings;

namespace TechagroSyncServices.Tests.Unit
{
    // Decides which products stay in database - deleting by mistake is the main risk here
    [TestClass]
    public class DeleteNotSyncedProductsTests
    {
        private const IntegrationCompany Company = IntegrationCompany.HIFI;

        private FakeProductRepository _repository;
        private ProductSyncService _service;
        private string _trackerFile;

        [TestInitialize]
        public void Initialize()
        {
            _repository = new FakeProductRepository();
            _trackerFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
            _service = CreateService(missingRunsBeforeDelete: 1);
        }

        [TestCleanup]
        public void Cleanup() => File.Delete(_trackerFile);

        private ProductSyncService CreateService(int missingRunsBeforeDelete)
        {
            return new ProductSyncService(_repository, null, new DeleteSafetySettings
            {
                MaxMissingProductsPercent = 20m,
                MissingRunsBeforeDelete = missingRunsBeforeDelete,
                MissingProductsFilePath = _trackerFile
            });
        }

        private static string[] Codes(int from, int to) => Enumerable.Range(from, to - from + 1).Select(i => "P" + i).ToArray();

        [TestMethod]
        public async Task ProductMissingOnce_IsKept_WhenTwoRunsAreRequired()
        {
            _service = CreateService(missingRunsBeforeDelete: 2);

            await _service.DeleteNotSyncedProducts(Company, Codes(1, 10), Codes(1, 9), true);

            CollectionAssert.AreEquivalent(Codes(1, 10), _repository.CodesToKeep);
        }

        [TestMethod]
        public async Task ProductMissingTwiceInARow_IsDeleted_WhenTwoRunsAreRequired()
        {
            _service = CreateService(missingRunsBeforeDelete: 2);

            await _service.DeleteNotSyncedProducts(Company, Codes(1, 10), Codes(1, 9), true);
            await _service.DeleteNotSyncedProducts(Company, Codes(1, 10), Codes(1, 9), true);

            CollectionAssert.AreEquivalent(Codes(1, 9), _repository.CodesToKeep);
        }

        [TestMethod]
        public async Task ProductReturnedAgain_StartsCountingFromZero()
        {
            // e.g. product shifted between pages in one run only
            _service = CreateService(missingRunsBeforeDelete: 2);

            await _service.DeleteNotSyncedProducts(Company, Codes(1, 10), Codes(1, 9), true);
            await _service.DeleteNotSyncedProducts(Company, Codes(1, 10), Codes(1, 10), true);
            await _service.DeleteNotSyncedProducts(Company, Codes(1, 10), Codes(1, 9), true);

            CollectionAssert.AreEquivalent(Codes(1, 10), _repository.CodesToKeep);
        }

        [TestMethod]
        public async Task IncompleteFetch_NeitherCountsNorResetsMisses()
        {
            _service = CreateService(missingRunsBeforeDelete: 2);

            await _service.DeleteNotSyncedProducts(Company, Codes(1, 10), Codes(1, 9), true);
            await _service.DeleteNotSyncedProducts(Company, Codes(1, 10), Codes(1, 3), false);
            CollectionAssert.AreEquivalent(Codes(1, 10), _repository.CodesToKeep);

            await _service.DeleteNotSyncedProducts(Company, Codes(1, 10), Codes(1, 9), true);
            CollectionAssert.AreEquivalent(Codes(1, 9), _repository.CodesToKeep);
        }

        [TestMethod]
        public async Task MissCounters_SurviveServiceRestart()
        {
            await CreateService(missingRunsBeforeDelete: 2).DeleteNotSyncedProducts(Company, Codes(1, 10), Codes(1, 9), true);

            await CreateService(missingRunsBeforeDelete: 2).DeleteNotSyncedProducts(Company, Codes(1, 10), Codes(1, 9), true);

            CollectionAssert.AreEquivalent(Codes(1, 9), _repository.CodesToKeep);
        }

        [TestMethod]
        public async Task CompleteFetch_ProductMissingInApi_IsNotKept()
        {
            await _service.DeleteNotSyncedProducts(Company, Codes(1, 10), Codes(1, 9), true);

            CollectionAssert.AreEquivalent(Codes(1, 9), _repository.CodesToKeep);
        }

        [TestMethod]
        public async Task IncompleteFetch_KeepsWholeImportList()
        {
            await _service.DeleteNotSyncedProducts(Company, Codes(1, 10), Codes(1, 3), false);

            CollectionAssert.AreEquivalent(Codes(1, 10), _repository.CodesToKeep);
        }

        [TestMethod]
        public async Task EmptyApiResponse_KeepsWholeImportList()
        {
            await _service.DeleteNotSyncedProducts(Company, Codes(1, 3), new string[0], true);

            CollectionAssert.AreEquivalent(Codes(1, 3), _repository.CodesToKeep);
        }

        [TestMethod]
        public async Task ManyMissingProducts_LooksLikeApiError_KeepsWholeImportList()
        {
            // 7 of 10 missing - above 5 products and above 20%
            await _service.DeleteNotSyncedProducts(Company, Codes(1, 10), Codes(1, 3), true);

            CollectionAssert.AreEquivalent(Codes(1, 10), _repository.CodesToKeep);
        }

        [TestMethod]
        public async Task FewMissingProducts_AreDeletedEvenAboveLimitPercent()
        {
            // 5 of 10 missing - 50%, but small changes are always accepted
            await _service.DeleteNotSyncedProducts(Company, Codes(1, 10), Codes(1, 5), true);

            CollectionAssert.AreEquivalent(Codes(1, 5), _repository.CodesToKeep);
        }

        [TestMethod]
        public async Task MissingProductsBelowLimitPercent_AreDeleted()
        {
            // 30 of 200 missing - 15%
            await _service.DeleteNotSyncedProducts(Company, Codes(1, 200), Codes(1, 170), true);

            CollectionAssert.AreEquivalent(Codes(1, 170), _repository.CodesToKeep);
        }

        [TestMethod]
        public async Task Codes_AreComparedIgnoringCase_AndWithoutDuplicates()
        {
            await _service.DeleteNotSyncedProducts(Company, new[] { "SN 001HF", "sn 001hf", "SA 12000HF" }, new[] { "SN 001HF", "sa 12000hf" }, true);

            Assert.HasCount(2, _repository.CodesToKeep);
        }

        [TestMethod]
        public async Task CodesLongerThanColumnOrWithSpaces_AreComparedAsSavedInDatabase()
        {
            _repository.ProductCodesInDatabase = new[] { "550-7906-00M KR&#211", "SN 001" }.ToList();

            await _service.DeleteNotSyncedProducts(Company, null, new[] { "550-7906-00M KR&#211;TKI", " SN 001 " }, true);

            CollectionAssert.AreEquivalent(new[] { "550-7906-00M KR&#211", "SN 001" }, _repository.CodesToKeep);
        }

        [TestMethod]
        public async Task NoImportList_CompleteFetch_KeepsProductsReturnedByApi()
        {
            _repository.ProductCodesInDatabase = Codes(1, 10).ToList();

            await _service.DeleteNotSyncedProducts(Company, null, Codes(1, 8).Concat(new[] { "P11" }), true);

            CollectionAssert.AreEquivalent(Codes(1, 8), _repository.CodesToKeep);
        }

        [TestMethod]
        public async Task NoImportList_IncompleteFetch_DeletesNothing()
        {
            _repository.ProductCodesInDatabase = Codes(1, 10).ToList();

            await _service.DeleteNotSyncedProducts(Company, null, Codes(1, 3), false);

            Assert.AreEqual(0, _repository.DeleteCalls);
        }

        [TestMethod]
        public async Task NoImportList_ManyMissingProducts_DeletesNothing()
        {
            _repository.ProductCodesInDatabase = Codes(1, 10).ToList();

            await _service.DeleteNotSyncedProducts(Company, null, Codes(1, 2), true);

            Assert.AreEqual(0, _repository.DeleteCalls);
        }

        [TestMethod]
        public async Task DatabaseError_IsLoggedAndNotThrown()
        {
            _repository.ThrowOnDelete = true;

            await _service.DeleteNotSyncedProducts(Company, Codes(1, 3), Codes(1, 3), true);

            Assert.AreEqual(1, _repository.DeleteCalls);
        }
    }
}
