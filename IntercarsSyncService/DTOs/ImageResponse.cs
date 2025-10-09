using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntercarsSyncService.DTOs
{
    public class ImageResponse
    {
        public string TowKod { get; set; }
        public string IcIndex { get; set; }
        public string ImageLink { get; set; }
        public int SortNr { get; set; }
        public bool Watermark { get; set; }
    }

}
