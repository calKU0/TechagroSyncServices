using TechagroSyncServices.Manager.Enums;

namespace TechagroSyncServices.Manager.Models
{
    public class LogLine
    {
        public LogLevel Level { get; set; }
        public string Message { get; set; }
    }
}