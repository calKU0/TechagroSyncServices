using AgrobisSyncService.Constants;
using AgrobisSyncService.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TechagroApiSync.Shared.DTOs;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;

namespace AgrobisSyncService.Helpers
{
    public class BuildHelper
    {
        public static ProductDto BuildProductDto(ProductsResponse p, decimal defaultMargin, List<MarginRange> marginRanges)
        {
            decimal applicableMargin = MarginHelper.CalculateMargin(p.PriceNet, defaultMargin, marginRanges);
            decimal supplierDiscount = AppSettingsLoader.GetSupplierDiscount();
            supplierDiscount = Math.Min(Math.Max(supplierDiscount, 0m), 100m);
            decimal discountedPriceNet = p.PriceNet * (1 - (supplierDiscount / 100m));

            var descBuilder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(p.Description))
                descBuilder.Append($"<p>{p.Description.Trim()}</p>");

            var dto = new ProductDto
            {
                Id = p.Id,
                Code = "AB" + p.Index.Trim(),
                TradingCode = p.Index.Trim(),
                Ean = (p.Ean ?? string.Empty).Trim(),
                Name = Regex.Replace((p.Name ?? string.Empty).Trim(), @"\s+", " "),
                Quantity = p.Quantity,
                Description = descBuilder.ToString(),
                CategoriesString = BuildCategoriesString(p.Categories),
                Brand = (p.Brand ?? string.Empty).Trim(),

                NetBuyPrice = discountedPriceNet,
                GrossBuyPrice = discountedPriceNet * 1.23m,

                NetSellPriceB = discountedPriceNet * ((applicableMargin / 100m) + 1),
                GrossSellPriceB = discountedPriceNet * 1.23m * ((applicableMargin / 100m) + 1),

                Vat = 1.23m,
                Weight = p.Weight,
                Unit = p.SaleUnit,

                IntegrationCompany = ServiceConstants.Company
            };

            dto.Images = p.Photos
                .Where(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
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

        private static string BuildCategoriesString(Categories categories)
        {
            if (categories == null)
                return string.Empty;

            var leafPaths = new List<string>();
            BuildCategoryPaths(categories, "", leafPaths);
            return string.Join("|", leafPaths.Distinct());
        }

        private static void BuildCategoryPaths(Categories category, string currentPath, List<string> leafPaths)
        {
            if (category == null || string.IsNullOrWhiteSpace(category.Name))
                return;

            var name = category.Name?.Trim() ?? string.Empty;
            var path = string.IsNullOrEmpty(currentPath) ? name : $"{currentPath} > {name}";

            if (category.Child == null || string.IsNullOrWhiteSpace(category.Child.Name))
            {
                if (!string.IsNullOrWhiteSpace(path))
                    leafPaths.Add(path);
            }
            else
            {
                BuildCategoryPaths(category.Child, path, leafPaths);
            }
        }

        private static void BuildCategoryPaths(Child child, string currentPath, List<string> leafPaths)
        {
            if (child == null || string.IsNullOrWhiteSpace(child.Name))
                return;

            var name = child.Name?.Trim() ?? string.Empty;
            var path = string.IsNullOrEmpty(currentPath) ? name : $"{currentPath} > {name}";

            if (child.ChildCategory == null || string.IsNullOrWhiteSpace(child.ChildCategory.Name))
            {
                if (!string.IsNullOrWhiteSpace(path))
                    leafPaths.Add(path);
            }
            else
            {
                BuildCategoryPaths(child.ChildCategory, path, leafPaths);
            }
        }
    }
}

