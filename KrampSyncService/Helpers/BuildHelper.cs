using KrampSyncService.Constants;
using KrampSyncService.DTOs;
using System.Collections.Generic;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;

namespace KrampSyncService.Helpers
{
    public static class BuildHelper
    {
        private const decimal Vat = 23m;

        public static ProductDto BuildProductDto(ProductState p, decimal defaultMargin, List<MarginRange> marginRanges)
        {
            decimal netPrice = p.NetPrice ?? 0;
            decimal grossPrice = netPrice * (1 + (Vat / 100m));
            decimal applicableMargin = MarginHelper.CalculateMargin(netPrice, defaultMargin, marginRanges);

            return new ProductDto
            {
                Id = 0,
                Code = p.Sku,
                TradingCode = p.Sku,
                Name = p.Sku,
                Quantity = p.Stock ?? 0,

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
