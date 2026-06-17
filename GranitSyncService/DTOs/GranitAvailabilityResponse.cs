using System.Collections.Generic;
using System.Globalization;
using System.Xml.Serialization;

namespace GranitSyncService.DTOs
{
    [XmlRoot("availabilty_response")]
    public class GranitAvailabilityResponse
    {
        [XmlElement("customernumber")]
        public string CustomerNumber { get; set; } = string.Empty;

        [XmlElement("item")]
        public List<GranitItem> Items { get; set; } = new List<GranitItem>();
    }
    public class GranitItem
    {
        [XmlAttribute("itemcode")]
        public string ItemCode { get; set; } = string.Empty;

        [XmlElement("available_on_stock")]
        public string AvailableOnStockRaw { get; set; }

        [XmlElement("grossprice")]
        public string GrossPriceRaw { get; set; }

        [XmlIgnore]
        public decimal? GrossPrice
        {
            get
            {
                return decimal.TryParse(GrossPriceRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
                    ? (decimal?)value
                    : null;
            }
        }

        [XmlElement("netprice")]
        public string NetPriceRaw { get; set; }

        [XmlIgnore]
        public decimal? NetPrice
        {
            get
            {
                return decimal.TryParse(NetPriceRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
                    ? (decimal?)value
                    : null;
            }
        }

        [XmlElement("currency")]
        public string Currency { get; set; } = string.Empty;

        [XmlIgnore]
        public decimal Stock
        {
            get
            {
                return decimal.TryParse(AvailableOnStockRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var s)
                    ? s
                    : 0;
            }
        }
    }
}
