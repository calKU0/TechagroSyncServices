using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TechagroApiSync.Shared.Helpers;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;

namespace TechagroSyncServices.Tests.Unit
{
    [TestClass]
    public class ImportCodesTests
    {
        private string _filePath;

        [TestInitialize]
        public void Initialize() => _filePath = Path.GetTempFileName();

        [TestCleanup]
        public void Cleanup() => File.Delete(_filePath);

        private List<ImportCode> Read(ProductCodeAffix affix, params string[] lines)
        {
            File.WriteAllLines(_filePath, lines);
            return FileUtils.ReadImportCodes(_filePath, affix);
        }

        [TestMethod]
        public void CodesWithAndWithoutSuffix_AreTheSameProduct()
        {
            var codes = Read(new ProductCodeSuffix("KR"), "12345", "12345KR", "  67890kr ", "", "ABC");

            CollectionAssert.AreEqual(new[] { "12345", "67890", "ABC" }, codes.Select(c => c.SupplierCode).ToList());
            CollectionAssert.AreEqual(new[] { "12345KR", "67890KR", "ABCKR" }, codes.Select(c => c.DatabaseCode).ToList());
        }

        [TestMethod]
        public void CodesWithAndWithoutPrefix_AreTheSameProduct()
        {
            var codes = Read(new ProductCodePrefix("AB"), "ABX1", "X1", "X2");

            CollectionAssert.AreEqual(new[] { "X1", "X2" }, codes.Select(c => c.SupplierCode).ToList());
            CollectionAssert.AreEqual(new[] { "ABX1", "ABX2" }, codes.Select(c => c.DatabaseCode).ToList());
        }

        [TestMethod]
        public void Normalizer_IsAppliedToSupplierAndDatabaseCode()
        {
            File.WriteAllLines(_filePath, new[] { "sn 001", "SN 001HF" });
            var codes = FileUtils.ReadImportCodes(_filePath, new ProductCodeSuffix("HF"), code => code.ToUpperInvariant());

            Assert.HasCount(1, codes);
            Assert.AreEqual("SN 001", codes[0].SupplierCode);
            Assert.AreEqual("SN 001HF", codes[0].DatabaseCode);
        }

        [TestMethod]
        public void MissingFile_ReturnsEmptyList()
        {
            Assert.IsEmpty(FileUtils.ReadImportCodes(_filePath + ".missing", new ProductCodeSuffix("HF")));
        }

        [TestMethod]
        public void FilterByAllowedCodes_MatchesCodesFromFileWithAndWithoutPrefix()
        {
            var allowed = Read(new ProductCodePrefix("AB"), "ABX1", "X2", "X9").Select(c => c.DatabaseCode).ToList();
            var products = new List<ProductDto> { new ProductDto { Code = "ABX1" }, new ProductDto { Code = "ABX2" }, new ProductDto { Code = "ABX3" } };

            var filtered = ImportFilterHelper.FilterByAllowedCodes(products, allowed, p => p.Code, out var missing);

            CollectionAssert.AreEqual(new[] { "ABX1", "ABX2" }, filtered.Select(p => p.Code).ToList());
            CollectionAssert.AreEqual(new[] { "ABX9" }, missing.ToList());
        }

        [TestMethod]
        public void FilterByAllowedCodes_MatchesExpressVariant()
        {
            var allowed = Read(new ProductCodeSuffix("AR"), "SKU1").Select(c => c.DatabaseCode).ToList();
            var products = new List<ProductDto> { new ProductDto { Code = "SKU1AR" }, new ProductDto { Code = "SKU1AR-EXP" } };

            var filtered = ImportFilterHelper.FilterByAllowedCodes(products, allowed, p => p.Code, out _);

            Assert.HasCount(2, filtered);
        }
    }
}
