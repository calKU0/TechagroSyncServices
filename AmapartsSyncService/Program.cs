using System.ServiceProcess;

namespace AmapartsSyncService
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
                new AmapartsSyncService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}