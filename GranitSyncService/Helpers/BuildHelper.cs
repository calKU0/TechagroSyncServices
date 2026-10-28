using GranitSyncService.Constants;
using GranitSyncService.DTOs;
using System.Collections.Generic;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;

namespace GranitSyncService.Helpers
{
    public static class BuildHelper
    {
        public static ProductDto BuildProductDto(GranitItem p, decimal defaultMargin, List<MarginRange> marginRanges)
        {
            decimal applicableMargin = MarginHelper.CalculateMargin(p.NetPrice ?? 0, defaultMargin, marginRanges);

            var dto = new ProductDto
            {
                Id = 0,
                Code = p.ItemCode,
                TradingCode = p.ItemCode,
                Name = p.ItemCode,
                Quantity = p.Stock,

                NetBuyPrice = p.NetPrice ?? 0,
                GrossBuyPrice = p.GrossPrice ?? 0,

                NetSellPriceB = MarginHelper.ApplyMargin(p.NetPrice ?? 0, applicableMargin),
                GrossSellPriceB = MarginHelper.ApplyMargin(p.GrossPrice ?? 0, applicableMargin),

                Vat = 23,

                IntegrationCompany = ServiceConstants.Company
            };

            return dto;
        }
    }
}
