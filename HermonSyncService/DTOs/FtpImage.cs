namespace HermonSyncService.DTOs
{
    public class FtpImage
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public byte[] Data { get; set; }
    }
}