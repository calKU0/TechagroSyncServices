using AmapartsSyncService.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TechagroApiSync.Shared.DTOs;
using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;

namespace AmapartsSyncService.Helpers
{
    public static class BuildHelper
    {
        public static ProductDto BuildProductDto(ApiProductsResponse p, Dictionary<string, Dictionary<string, string>> parametersGrouped, decimal defaultMargin, List<MarginRange> marginRanges)
        {
            decimal applicableMargin = MarginHelper.CalculateMargin(p.NetPurchasePrice, defaultMargin, marginRanges);

            var dto = new ProductDto
            {
                Id = 0,
                Code = p.ProductCode,
                Ean = null,
                Name = p.ProductName,
                Quantity = p.StockQuantity,
                NetBuyPrice = p.NetPurchasePrice,
                GrossBuyPrice = p.NetPurchasePrice * 1.23m,
                NetSellPrice = p.NetPurchasePrice * ((applicableMargin / 100m) + 1),
                GrossSellPrice = p.NetPurchasePrice * 1.23m * ((applicableMargin / 100m) + 1),
                Vat = 23,
                Weight = 0,
                Brand = p.Manufacturer,
                Unit = "szt.",
                IntegrationCompany = IntegrationCompany.AMA
            };

            // ---------- Description ----------
            var attributes = parametersGrouped.TryGetValue(p.ProductCode, out var attrs) ? attrs : new Dictionary<string, string>();
            var descBuilder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(p.Description))
                descBuilder.Append(p.Description);

            if (attributes.Any())
            {
                descBuilder.Append("<p><b>Parametry: </b></p>");
                descBuilder.Append("<ul>");
                foreach (var attr in attributes.Where(x => !string.IsNullOrWhiteSpace(x.Value)))
                {
                    descBuilder.AppendFormat("<li>{0}: {1}</li>", attr.Key, attr.Value);
                }
                descBuilder.Append("</ul>");
            }

            dto.Description = descBuilder.ToString();

            // ---------- Images ----------
            dto.Images = (p.ProductImageUrl ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().Trim('"'))
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x) &&
                    Uri.IsWellFormedUriString(x, UriKind.Absolute))
                .Select(url =>
                {
                    string path = new Uri(url).AbsolutePath;
                    string firstSegment = path.TrimStart('/').Split('/')[0];
                    string imageId = firstSegment.Split('-')[0];

                    return new ImageDto
                    {
                        Name = $"{p.ProductCode}_{imageId}",
                        Url = url
                    };
                })
                .ToList();

            return dto;
        }
    }
}