using IntercarsSyncService.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntercarsSyncService.Helpers
{
    public static class ProductComparer
    {
        public static List<FullProductDto> FindNewProducts(List<FullProductDto> previousProducts, List<FullProductDto> currentProducts)
        {
            var previousKeys = previousProducts
                .Select(p => p.TowKod?.Trim().ToUpperInvariant())
                .Where(k => !string.IsNullOrEmpty(k))
                .ToHashSet();

            var newProducts = currentProducts
                .Where(p => !previousKeys.Contains(p.TowKod?.Trim().ToUpperInvariant()))
                .ToList();

            return newProducts;
        }
    }
}