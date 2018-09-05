namespace Microsoft.LocalForwarder.ConsoleHost
{
    using System;
    using System.IO;
    using System.Threading;
    using Library;

    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length > 1)
            {
                throw new ArgumentException($"Too many arguments: {string.Join(',', args)}");
            }

            // a non-interactive session is something like a Linux daemon where no user input is available
            bool nonInteractiveMode = args.Length > 0 && string.Equals(args[0], "noninteractive", StringComparison.OrdinalIgnoreCase);

            Common.Diagnostics.LogInfo("Starting the console host...");

            Common.Diagnostics.LogInfo($"Mode: noninteractive={nonInteractiveMode}");

            Host host = new Host();

            try
            {
                Common.Diagnostics.LogInfo("Starting the host...");

                string config = ReadConfiguratiion();

                host.Run(config, TimeSpan.FromSeconds(5));

                Common.Diagnostics.LogInfo("The host is running");
            }
            catch (Exception e)
            {
                Common.Diagnostics.LogError(FormattableString.Invariant($"Unexpected error while starting the host. {e.ToString()}"));
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
                Common.Diagnostics.LogError(FormattableString.Invariant($"Unexpected error while stopping the host. {e.ToString()}"));
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

        private static string ReadConfiguratiion()
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
