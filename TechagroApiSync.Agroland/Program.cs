using System.ServiceProcess;

namespace TechagroApiSync.Agroland
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        private static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new AgrolandApiSyncService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}