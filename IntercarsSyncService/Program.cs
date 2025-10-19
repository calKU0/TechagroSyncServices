using System.ServiceProcess;

namespace IntercarsSyncService
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
                new IntercarsSyncService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}