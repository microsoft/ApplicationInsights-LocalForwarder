namespace Microsoft.LocalForwarder.WindowsServiceHost
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Reflection;
    using System.ServiceProcess;
    using System.Xml.Linq;

    static class Program
    {
        static void LoadInstances1<T>(XElement definition, ICollection<T> instances, int modules)
        {
            int a = 1 + 2;
            return;
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            foreach (var m in typeof(Program).GetRuntimeMethods())
            {
                Console.WriteLine(m.Name);
            }

            if (Environment.UserInteractive && Debugger.IsAttached)
            {
                // debugging
                var service = new LocalForwarderHostService();

                service.TestStartStop(TimeSpan.FromSeconds(10));
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                new LocalForwarderHostService()
                };
                ServiceBase.Run(ServicesToRun);
            }
        }
    }
}
