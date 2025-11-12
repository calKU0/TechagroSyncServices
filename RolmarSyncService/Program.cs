using System.ServiceProcess;

namespace RolmarSyncService
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
                new RolmarSyncService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}