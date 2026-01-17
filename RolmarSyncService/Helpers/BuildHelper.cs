using RolmarSyncService.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechagroApiSync.Shared.DTOs;
using TechagroApiSync.Shared.Enums;
using TechagroSyncServices.Shared.DTOs;
using TechagroSyncServices.Shared.Helpers;

namespace RolmarSyncService.Helpers
{
    public static class BuildHelper
    {
        public static List<ProductDto> BuildProductDtos(List<Product> products, List<StockItem> stock, List<PhotoItem> images, decimal defaultMargin, List<MarginRange> marginRanges)
        {
            var stockDict = stock.ToDictionary(s => s.Index, s => s);
            var imageDict = images.GroupBy(i => i.Index)
                                  .ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<ProductDto>();

            foreach (var p in products)
            {
                // Safe conversions with logging
                int id = ConvertHelper.SafeConvertToInt(p.Id, "Id", p.ProductIndex);
                decimal weight = ConvertHelper.SafeConvertToDecimal(p.Weight, "Weight", p.ProductIndex);
                decimal package = ConvertHelper.SafeConvertToDecimal(p.ErpPackage, "ErpPackage", p.ProductIndex) == 0 ? 1 : ConvertHelper.SafeConvertToDecimal(p.ErpPackage, "ErpPackage", p.ProductIndex);
                decimal price = ConvertHelper.SafeConvertToDecimal(p.Price, "Price", p.ProductIndex) / package;

                decimal applicableMargin = MarginHelper.CalculateMargin(price, defaultMargin, marginRanges);

                var descBuilder = new StringBuilder($"<p>{p.Description}</p>" ?? "");

                if (p.Specifications != null && p.Specifications.Any())
                {
                    descBuilder.Append("<p><b>Parametry:</b></p>");
                    descBuilder.Append("<ul>");
                    foreach (var spec in p.Specifications.Where(x => !string.IsNullOrWhiteSpace(x.Value)))
                    {
                        // Include unit if it exists
                        string unit = string.IsNullOrWhiteSpace(spec.UnitName) ? "" : $" {spec.UnitName}";
                        descBuilder.AppendFormat("<li>{0}: {1}{2}</li>", spec.Name, spec.Value, unit);
                    }
                    descBuilder.Append("</ul>");
                }

                var dto = new ProductDto
                {
                    Id = id,
                    Code = p.ProductIndex,
                    Name = p.Name,
                    Description = descBuilder.ToString(),
                    Brand = p.Brand,
                    Weight = weight,
                    Ean = p.Ean,
                    NetBuyPrice = price,
                    GrossBuyPrice = price * 1.23m,
                    NetSellPrice = price * ((applicableMargin / 100m) + 1),
                    GrossSellPrice = price * 1.23m * ((applicableMargin / 100m) + 1),
                    Vat = 23,
                    Unit = "szt.",
                    IntegrationCompany = IntegrationCompany.ROLMAR,
                    Quantity = stockDict.TryGetValue(p.ProductIndex, out var s) ? s.Stock : 0,
                    Images = imageDict.TryGetValue(p.ProductIndex, out var imgs) ? BuildProductImages(p.ProductIndex, imgs) : new List<ImageDto>()
                };

                result.Add(dto);
            }

            return result;
        }

        private static List<ImageDto> BuildProductImages(string productCode, List<PhotoItem> images)
        {
            var result = new List<ImageDto>(images.Count);

            foreach (var img in images)
            {
                int imageIndex = 1;
                result.Add(new ImageDto
                {
                    Name = $"{productCode}_{imageIndex}",
                    Url = img.Url
                });
                imageIndex++;
            }

            return result;
        }
    }
}