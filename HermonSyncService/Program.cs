using System.ServiceProcess;

namespace HermonSyncService
{
    internal static class Program
    {
        /// <summary>
        /// Główny punkt wejścia dla aplikacji.
        /// </summary>
        private static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new HermonSyncService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}