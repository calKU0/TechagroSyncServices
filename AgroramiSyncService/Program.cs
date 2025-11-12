using System.ServiceProcess;

namespace AgroramiSyncService
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
                new AgroramiSyncService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}