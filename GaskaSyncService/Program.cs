using System.ServiceProcess;

namespace GaskaSyncService
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
                new GaskaApiService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}