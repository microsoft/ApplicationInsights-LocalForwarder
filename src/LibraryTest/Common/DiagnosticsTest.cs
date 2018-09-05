namespace Microsoft.LocalForwarder.LibraryTest
{
    using System;
    using System.IO;
    using System.Threading;
    using LocalForwarder.Common;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DiagnosticsTests
    {
        private readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

        [TestMethod]
        public void DiagnosticsTests_LogsMessageToFile()
        {
            // ARRANGE
            var guid = Guid.NewGuid();

            // ACT
            string testTraceMessage = $"Trace message here {guid.ToString()}";
            string testInfoMessage = $"Info message here {guid.ToString()}";

            Diagnostics.LogTrace(testTraceMessage);
            Diagnostics.LogInfo(testInfoMessage);

            // ASSERT
            Common.SwitchLoggerToDifferentFile();
            Thread.Sleep(TimeSpan.FromSeconds(1));

            Assert.IsTrue(SpinWait.SpinUntil(() => File.Exists("LocalForwarder-internal.log"), this.timeout));
            Assert.IsTrue(SpinWait.SpinUntil(() => File.Exists("LocalForwarder.log"), this.timeout));
            Assert.IsFalse(File.ReadAllText("LocalForwarder.log").Contains(testTraceMessage));
            Assert.IsTrue(File.ReadAllText("LocalForwarder.log").Contains(testInfoMessage));
        }
    }
}
