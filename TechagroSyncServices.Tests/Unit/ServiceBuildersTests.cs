using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.DTOs;

namespace TechagroSyncServices.Tests.Unit
{
    [TestClass]
    public class ServiceBuildersTests
    {
        private static readonly List<MarginRange> NoMarginRanges = new List<MarginRange>();

        [TestMethod]
        public void HiFi_ProductDto_HasSuffixedCodeAndPrices()
        {
            var product = new HiFiSyncService.DTOs.HiFiProduct { Reference = "SN 001", NetPrice = 100m, Stock = 7, Currency = "PLN" };

            var dto = HiFiSyncService.Helpers.BuildHelper.BuildProductDto(product, 25m, NoMarginRanges);

            Assert.AreEqual("SN 001HF", dto.Code);
            Assert.AreEqual("SN 001", dto.TradingCode);
            Assert.AreEqual("SN 001", dto.Name);
            Assert.AreEqual(7m, dto.Quantity);
            Assert.AreEqual(123m, dto.GrossBuyPrice);
            Assert.AreEqual(125m, dto.NetSellPriceB);
            Assert.AreEqual(IntegrationCompany.HIFI, dto.IntegrationCompany);
        }

        [TestMethod]
        public void Kramp_ProductDto_HasSuffixedCode()
        {
            var state = new KrampSyncService.DTOs.ProductState { Sku = "12345", NetPrice = 100m, Stock = 3 };

            var dto = KrampSyncService.Helpers.BuildHelper.BuildProductDto(state, 25m, NoMarginRanges);

            Assert.AreEqual("12345KR", dto.Code);
            Assert.AreEqual("12345", dto.TradingCode);
            Assert.AreEqual(IntegrationCompany.KRAMP, dto.IntegrationCompany);
        }
    }
}
