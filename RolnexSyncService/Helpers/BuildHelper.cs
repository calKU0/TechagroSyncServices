using RolnexSyncService.Constants;
using RolnexSyncService.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TechagroApiSync.Shared.DTOs;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;

namespace RolnexSyncService.Helpers
{
    public static class BuildHelper
    {
        public static ProductDto BuildProductDto(ProductsResponse p, decimal defaultMargin, List<MarginRange> marginRanges)
        {
            decimal applicableMargin = MarginHelper.CalculateMargin(p.PriceAfterDiscountNet, defaultMargin, marginRanges);

            var descBuilder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(p.Desc))
                descBuilder.Append($"<p>{p.Desc}</p>");

            if (!string.IsNullOrWhiteSpace(p.Brand))
                descBuilder.Append($"<p><b>Producent: </b>{p.Brand}</p>");

            if (!string.IsNullOrWhiteSpace(p.CrossNumbers))
                descBuilder.Append($"<p><b>Numery referencyjne: </b>{p.CrossNumbers}</p>");

            var dto = new ProductDto
            {
                Id = p.Id,
                Code = ServiceConstants.CodePrefix.ToDatabaseCode(p.Id.ToString()),
                TradingCode = p.Sku,
                Name = p.Name + " " + p.Sku,
                Quantity = p.Qty,
                Description = descBuilder.ToString(),
                CategoriesString = BuildCategoriesString(p.Categories),
                Brand = p.Brand,

                NetBuyPrice = p.PriceAfterDiscountNet,
                GrossBuyPrice = p.PriceAfterDiscountNet * (1 + (p.Vat / 100m)),

                NetSellPriceB = MarginHelper.ApplyMargin(p.PriceAfterDiscountNet, applicableMargin),
                GrossSellPriceB = MarginHelper.ApplyMargin(p.PriceAfterDiscountNet * (1 + (p.Vat / 100m)), applicableMargin),

                Vat = p.Vat,
                Weight = p.Weight,
                Unit = p.Unit,

                IntegrationCompany = ServiceConstants.Company
            };

            // ---------- Images ----------
            var imageUrls = new[]
            {
                p.Photo,
                p.Photo1,
                p.Photo2,
                p.Photo3
            }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => Uri.IsWellFormedUriString(x, UriKind.Absolute))
            .Distinct()
            .ToList();

            dto.Images = imageUrls
                .Select(url =>
                {
                    var uri = new Uri(url);
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(uri.AbsolutePath);

                    return new ImageDto
                    {
                        Name = fileName,
                        Url = url
                    };
                })
                .ToList();

            return dto;
        }

        private static string BuildCategoriesString(string categories)
        {
            if (string.IsNullOrWhiteSpace(categories))
                return string.Empty;

            var paths = categories
                .Split(';')
                .Select(p => p.Replace("\\", " > ").Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            var leafPaths = new List<string>();

            foreach (var path in paths)
            {
                bool isParent = paths.Any(other =>
                    other != path &&
                    other.StartsWith(path + " > ", StringComparison.OrdinalIgnoreCase));

                if (!isParent)
                    leafPaths.Add(path);
            }

            return string.Join("|", leafPaths.Distinct());
        }
    }
}