using System.Collections.Generic;

namespace HermonSyncService.DTOs
{
    public class ProductsDetailResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public ProductPrice ClientPrice { get; set; }
        public ProductPrice AdditionalCosts { get; set; }
        public List<Availability> BranchesAvailability { get; set; }
        public string Unit { get; set; }
        public string ProducerName { get; set; }
        public string Error { get; set; }
    }

    public class ProductPrice
    {
        public decimal NetPrice { get; set; }
        public decimal GrossPrice { get; set; }
        public string CurrencyCode { get; set; }
        public decimal TaxRate { get; set; }
    }

    public class Availability
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Quantity { get; set; }
    }
}