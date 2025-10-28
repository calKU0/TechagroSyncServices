using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RolmarSyncService.DTOs
{
    public class PhotosResponse
    {
        [JsonPropertyName("result")]
        public List<PhotoItem> Result { get; set; }
    }

    public class PhotoItem
    {
        [JsonPropertyName("main")]
        public string Main { get; set; }

        [JsonPropertyName("index")]
        public string Index { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}
