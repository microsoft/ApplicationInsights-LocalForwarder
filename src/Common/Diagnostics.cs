namespace Microsoft.LocalForwarder.Common
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Xml;
    using NLog;
    using NLog.Config;

    public static class Diagnostics
    {
        private static readonly Logger logger;
        private static SpinLock spinLock = new SpinLock();

        //!!! no test coverage in this file

        static Diagnostics()
        {
            //!!!
            System.Diagnostics.Trace.WriteLine("Diagnostics static ctr");

            logger = LogManager.GetCurrentClassLogger();

            try
            {
                if (LogManager.Configuration?.LoggingRules?.Any() == true)
                {
                    System.Diagnostics.Trace.WriteLine("Config file read");

                    // config file has been read, use that
                }
                else
                {
                    System.Diagnostics.Trace.WriteLine("Reading default");

                    // no config file, use default config
                    string nlogConfigXml = ReadDefaultConfiguration();

                    System.Diagnostics.Trace.WriteLine("Read default: " + nlogConfigXml);


                    SetDefaultConfiguration(nlogConfigXml);

                    System.Diagnostics.Trace.WriteLine("Set default");
                }
            }
            catch (Exception e)
            {
                // telemetry can never crash the application, swallow the exception
                // this probably means no logging
                //!!!
                System.Diagnostics.Trace.WriteLine($"Crashed! {e.ToString()}");
            }
        }

        private static void SetDefaultConfiguration(string configXml)
        {
            using (var sr = new StringReader(configXml))
            {
                using (var xr = XmlReader.Create(sr))
                {
                    LogManager.Configuration = new XmlLoggingConfiguration(xr, null);
                }
            }

            LogManager.ReconfigExistingLoggers();
        }

        private static string ReadDefaultConfiguration()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Microsoft.LocalForwarder.Common.NLog.config";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static void Log(string message, LogLevel logLevel)
        {
            bool lockTaken = false;
            spinLock.Enter(ref lockTaken);

            if (lockTaken)
            {
                // ok to lose the message in an unlikely case that lockTaken is false
                logger.Log(logLevel, message);

                spinLock.Exit();
            }
        }

        /// <summary>
        /// Do not use during normal operation.
        /// Unit test use only.
        /// </summary>
        internal static void Shutdown(TimeSpan timeout)
        {
            LogManager.Flush(timeout);

            LogManager.Shutdown();
        }

        public static void Flush(TimeSpan timeout)
        {
            LogManager.Flush(timeout);
        }

        public static void LogTrace(string message)
        {
            Diagnostics.Log(message, LogLevel.Trace);
        }

        public static void LogInfo(string message)
        {
            Diagnostics.Log(message, LogLevel.Info);
        }

        public static void LogWarn(string message)
        {
            Diagnostics.Log(message, LogLevel.Warn);
        }

        public static void LogError(string message)
        {
            Diagnostics.Log(message, LogLevel.Error);
        }

    }
}
