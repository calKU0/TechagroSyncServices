using AmapartsSyncService.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmapartsSyncService.Helpers
{
    public static class GroupHelper
    {
        public static Dictionary<string, Dictionary<string, string>> GroupParameters(List<ProductParameterCsv> parameters)
        {
            var parametersGrouped = parameters
                .GroupBy(p => p.ProductCode)
                .ToDictionary(
                    g => g.Key,
                    g => g.ToDictionary(r => r.Parameter, r => r.Value)
                );

            return parametersGrouped;
        }
    }
}