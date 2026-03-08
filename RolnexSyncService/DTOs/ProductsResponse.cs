using CsvHelper.Configuration.Attributes;
using RolnexSyncService.Helpers;
using System;

namespace RolnexSyncService.DTOs
{
    public class ProductsResponse
    {
        [Name("ean")]
        public string Ean { get; set; }

        [Name("id")]
        public int Id { get; set; }

        [Name("sku")]
        public string Sku { get; set; }

        [Name("name")]
        public string Name { get; set; }

        [Name("model")]
        public string Model { get; set; }

        [Name("desc")]
        public string Desc { get; set; }

        [Name("url")]
        public string Url { get; set; }

        [Name("photo")]
        public string Photo { get; set; }

        [Name("photo1")]
        public string Photo1 { get; set; }

        [Name("photo2")]
        public string Photo2 { get; set; }

        [Name("photo3")]
        public string Photo3 { get; set; }

        [Name("Kategoria")]
        public string Categories { get; set; }

        [Name("unit")]
        public string Unit { get; set; }

        [Name("weight")]
        [TypeConverter(typeof(FlexibleDecimalConverter))]
        public decimal Weight { get; set; }

        [Name("PKWiU")]
        public string PKWiU { get; set; }

        [Name("inStock")]
        public bool InStock { get; set; }

        [Name("qty")]
        [TypeConverter(typeof(FlexibleDecimalConverter))]
        public decimal Qty { get; set; }

        [Name("availability")]
        [Format("dd.MM.yyyy")]
        public DateTime? Availability { get; set; }

        [Name("requiredBox")]
        public bool RequiredBox { get; set; }

        [Name("quantityPerBox")]
        [TypeConverter(typeof(FlexibleDecimalConverter))]
        public decimal QuantityPerBox { get; set; }

        [Name("priceAfterDiscountNet")]
        [TypeConverter(typeof(FlexibleDecimalConverter))]
        public decimal PriceAfterDiscountNet { get; set; }

        [Name("vat")]
        public int Vat { get; set; }

        [Name("retailPriceGross")]
        [TypeConverter(typeof(FlexibleDecimalConverter))]
        public decimal RetailPriceGross { get; set; }
        [Name("brand")]
        public string Brand { get; set; }
        [Name("manufacturer_code")]
        public string CrossNumbers { get; set; }
    }
}