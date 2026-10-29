using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TechagroSyncServices.Shared.Helpers;

namespace TechagroSyncServices.Tests.Unit
{
    [TestClass]
    public class ProductCodeAffixTests
    {
        private static readonly ProductCodeSuffix Suffix = new ProductCodeSuffix("HF");
        private static readonly ProductCodePrefix Prefix = new ProductCodePrefix("RL");

        [TestMethod]
        public void Suffix_IsAddedAtTheEnd()
        {
            Assert.AreEqual("SN 001HF", Suffix.ToDatabaseCode("SN 001"));
        }

        [TestMethod]
        public void Suffix_IsRemovedIgnoringCase()
        {
            Assert.AreEqual("SN 001", Suffix.Remove("SN 001HF"));
            Assert.AreEqual("SN 001", Suffix.Remove("SN 001hf"));
            Assert.AreEqual("SN 001", Suffix.Remove("SN 001"));
        }

        [TestMethod]
        public void Prefix_IsAddedAtTheBeginning()
        {
            Assert.AreEqual("RL123", Prefix.ToDatabaseCode("123"));
        }

        [TestMethod]
        public void Prefix_IsRemovedIgnoringCase()
        {
            Assert.AreEqual("123", Prefix.Remove("RL123"));
            Assert.AreEqual("123", Prefix.Remove("rl123"));
            Assert.AreEqual("123", Prefix.Remove("123"));
        }

        [TestMethod]
        public void Prefix_IsNotTreatedAsSuffix()
        {
            Assert.AreEqual("123RL", Prefix.Remove("123RL"));
            Assert.AreEqual("HF123", Suffix.Remove("HF123"));
        }

        [TestMethod]
        public void CodeEqualToAffix_IsNotStripped()
        {
            Assert.AreEqual("HF", Suffix.Remove("HF"));
            Assert.AreEqual("RL", Prefix.Remove("RL"));
        }

        [TestMethod]
        public void DatabaseCode_IsTruncatedTo20Characters()
        {
            Assert.AreEqual("ABCDEFGHIJKLMNOPQRSH", new ProductCodeSuffix("HR").ToDatabaseCode("ABCDEFGHIJKLMNOPQRS"));
            Assert.AreEqual(ProductCodeAffix.MaxCodeLength, Prefix.ToDatabaseCode("123456789012345678901234").Length);
        }

        [TestMethod]
        public void TruncatedDatabaseCode_FromImportFile_MapsToTheSameDatabaseCode()
        {
            var suffix = new ProductCodeSuffix("HR");
            var databaseCode = suffix.ToDatabaseCode("ABCDEFGHIJKLMNOPQRS");

            Assert.AreEqual(databaseCode, suffix.ToDatabaseCode(suffix.Remove(databaseCode)));
        }

        [TestMethod]
        public void EmptyAffix_IsNotAllowed()
        {
            Assert.ThrowsExactly<ArgumentException>(() => new ProductCodeSuffix(" "));
            Assert.ThrowsExactly<ArgumentException>(() => new ProductCodePrefix(null));
        }
    }
}
