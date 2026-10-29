using HiFiSyncService.Constants;
using HiFiSyncService.DTOs;
using System.Collections.Generic;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;

namespace HiFiSyncService.Helpers
{
    public static class BuildHelper
    {
        private const decimal Vat = 23m;

        public static ProductDto BuildProductDto(HiFiProduct p, decimal defaultMargin, List<MarginRange> marginRanges)
        {
            decimal netPrice = p.NetPrice;
            decimal grossPrice = netPrice * (1 + (Vat / 100m));
            decimal applicableMargin = MarginHelper.CalculateMargin(netPrice, defaultMargin, marginRanges);

            return new ProductDto
            {
                Id = 0,
                Code = ServiceConstants.CodeSuffix.ToDatabaseCode(p.Reference),
                TradingCode = p.Reference,
                Name = p.Reference,
                Quantity = p.Stock,

                NetBuyPrice = netPrice,
                GrossBuyPrice = grossPrice,

                NetSellPriceB = MarginHelper.ApplyMargin(netPrice, applicableMargin),
                GrossSellPriceB = MarginHelper.ApplyMargin(grossPrice, applicableMargin),

                Vat = Vat,

                IntegrationCompany = ServiceConstants.Company
            };
        }
    }
}
