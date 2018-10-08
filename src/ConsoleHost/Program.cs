namespace Microsoft.LocalForwarder.ConsoleHost
{
    using System;
    using System.IO;
    using System.Threading;
    using Library;
    using System.Reflection;
    using System.Collections;
    using System.Linq;
    using System.Xml.Linq;
    using System.Collections.Generic;

    internal class Program
    {
        private static readonly MethodInfo LoadInstancesDefinition = typeof(Program).GetRuntimeMethods().First(m => m.Name == "LoadInstances");

        protected static void LoadInstances<T>(XElement definition, ICollection<T> instances, int modules)
        {
            int a = 1 + 2;
            return;
        }

        static void Main(string[] args)
        {
            //!!!
            Common.Diagnostics.LogInfo("Started execution");

            if(args.Length > 1)
            {
                throw new ArgumentException($"Too many arguments: {string.Join(',', args)}");
            }

            // a non-interactive session is something like a Linux daemon where no user input is available
            bool nonInteractiveMode = args.Length > 0 && string.Equals(args[0], "noninteractive", StringComparison.OrdinalIgnoreCase);

            var type = Type.GetType("Microsoft.ApplicationInsights.Extensibility.Implementation.TelemetryConfigurationFactory, Microsoft.ApplicationInsights");
            //Console.WriteLine("Full name: " + typeof(SomeClass).AssemblyQualifiedName);

            Console.WriteLine("Type found: " + type.AssemblyQualifiedName);
            foreach (var m in type.GetRuntimeMethods().OrderBy(method => method.Name))
            {
                Common.Diagnostics.LogInfo(m.Name);
            }

            Common.Diagnostics.LogInfo(Environment.NewLine);
            var obj = new SomeClass();
            //obj.SomeMethod();
            obj.SomeGenericMethod<int>();
            //!!!
            Common.Diagnostics.LogInfo("Listing methods:");

            //!!!
            foreach (var m in typeof(SomeClass).GetRuntimeMethods().OrderBy(method => method.Name))
            {
                Common.Diagnostics.LogInfo(m.Name);
            }

            Common.Diagnostics.LogInfo("Finished");

            //Console.WriteLine(LoadInstancesDefinition.Name);
            
            Common.Diagnostics.LogInfo("Starting the console host...");
            //!!!
            Console.WriteLine("Logged once");
            Console.WriteLine($"Mode: noninteractive={nonInteractiveMode}");

            Host host = new Host();

            try
            {
                Common.Diagnostics.LogInfo("Starting the host...");

                string config = ReadConfiguration();

                host.Run(config, TimeSpan.FromSeconds(5));

                Common.Diagnostics.LogInfo("The host is running");
            }
            catch (Exception e)
            {
                Common.Diagnostics.LogInfo(FormattableString.Invariant($"Unexpected error while starting the host. {e.ToString()}"));
                throw;
            }
            finally
            {
                if (!nonInteractiveMode)
                {
                    Console.ReadLine();
                }
                else
                {
                    Thread.Sleep(Timeout.InfiniteTimeSpan);
                }
            }

            try
            {
                Common.Diagnostics.LogInfo("Stopping the console host...");

                Common.Diagnostics.LogInfo("Stopping the host...");

                host.Stop();

                Common.Diagnostics.LogInfo("The host is stopped");
            }
            catch (Exception e)
            {
                Common.Diagnostics.LogInfo(FormattableString.Invariant($"Unexpected error while stopping the host. {e.ToString()}"));
            }
            finally
            {
                Common.Diagnostics.LogInfo("The console host is stopped");

                if (!nonInteractiveMode)
                {
                    Console.ReadLine();
                }
            }

            Common.Diagnostics.LogInfo("The console host has exited");
        }

        private static string ReadConfiguration()
        {
            try
            {
                return File.ReadAllText("LocalForwarder.config");
            }
            catch (Exception e)
            {
                throw new ArgumentException(FormattableString.Invariant($"Could not read the configuration file. {e.ToString()}"), e);
            }
        }
    }
}
