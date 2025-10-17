using System.ServiceProcess;

namespace AgrolandSyncService
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
                new AgrolandSyncService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}