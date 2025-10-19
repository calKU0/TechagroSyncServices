using System;

namespace IntercarsSyncService.DTOs
{
    public class ApiFile
    {
        public string FileName { get; set; }
        public string Url { get; set; }
        public DateTime? DateCreated { get; set; }
    }
}