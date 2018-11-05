namespace Microsoft.LocalForwarder.LibraryTest.Library.Inputs.GrpcInput
{
    using LocalForwarder.Library.Inputs.GrpcInput;
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Opencensus.Proto.Agent.Common.V1;
    using Opencensus.Proto.Agent.Trace.V1;
    using Opencensus.Proto.Trace.V1;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class GrpcOpenCensusInputTests
    {
        private static readonly Random Rand = new Random();
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
        private static readonly UpdatedLibraryConfig ConfigResponse = new UpdatedLibraryConfig
        {
            Config = new TraceConfig
            {
                ConstantSampler = new ConstantSampler { Decision = true }
            }
        };

        [TestMethod]
        public void GrpcOpenCensusInputTests_StartsAndStops()
        {
            // ARRANGE
            int port = GetPort();
            var input = new GrpcOpenCensusInput("localhost", port);

            // ACT
            input.Start(null, null);

            Assert.IsTrue(SpinWait.SpinUntil(() => input.IsRunning, GrpcOpenCensusInputTests.DefaultTimeout));

            input.Stop();

            Assert.IsTrue(SpinWait.SpinUntil(() => !input.IsRunning, GrpcOpenCensusInputTests.DefaultTimeout));

            // ASSERT
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task GrpcOpenCensusInputTests_CantStartWhileRunning()
        {
            // ARRANGE
            int port = GetPort();
            var input = new GrpcOpenCensusInput("localhost", port);

            input.Start(null, null);

            Assert.IsTrue(SpinWait.SpinUntil(() => input.IsRunning, GrpcOpenCensusInputTests.DefaultTimeout));

            // ACT
            input.Start(null, null);

            // ASSERT
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void GrpcOpenCensusInputTests_CantStopWhileStopped()
        {
            // ARRANGE
            int port = GetPort();
            var input = new GrpcOpenCensusInput("localhost", port);
            
            // ACT
            input.Stop();

            // ASSERT
        }

        [TestMethod]
        public async Task GrpcOpenCensusInputTests_ReceivesSpans()
        {
            // ARRANGE
            int batchesReceived = 0;
            ExportTraceServiceRequest receivedBatch = null;

            int port = GetPort();
            var input = new GrpcOpenCensusInput("localhost", port);
            input.Start(
                (telemetryBatch, callContext) =>
                {
                    batchesReceived++;
                    receivedBatch = telemetryBatch;
                    return new ExportTraceServiceResponse();
                },
                null);
            Assert.IsTrue(SpinWait.SpinUntil(() => input.IsRunning, GrpcOpenCensusInputTests.DefaultTimeout));

            var grpcWriter = new GrpcWriter(false, port);

            // ACT
            ExportTraceServiceRequest batch = new ExportTraceServiceRequest();
            batch.Spans.Add(new Span() {Name = new TruncatableString() {Value = "Event1"}});

            await grpcWriter.Write(batch).ConfigureAwait(false);

            // ASSERT
            Common.AssertIsTrueEventually(
                () => input.GetStats().BatchesReceived == 1 && batchesReceived == 1 &&
                      receivedBatch.Spans.Single().Name.Value == "Event1", GrpcOpenCensusInputTests.DefaultTimeout);

            input.Stop();
            Assert.IsTrue(SpinWait.SpinUntil(() => !input.IsRunning, GrpcOpenCensusInputTests.DefaultTimeout));
        }

        [TestMethod]
        public async Task GrpcOpenCensusInputTests_ReceivesSpansWithNode()
        {
            // ARRANGE
            int batchesReceived = 0;
            ExportTraceServiceRequest receivedBatch = null;

            int port = GetPort();
            var input = new GrpcOpenCensusInput("localhost", port);
            input.Start(
                (telemetryBatch, callContext) =>
                {
                    batchesReceived++;
                    receivedBatch = telemetryBatch;
                    return new ExportTraceServiceResponse();
                },
                null);
            Assert.IsTrue(SpinWait.SpinUntil(() => input.IsRunning, GrpcOpenCensusInputTests.DefaultTimeout));

            var grpcWriter = new GrpcWriter(false, port);

            // ACT
            ExportTraceServiceRequest batch = new ExportTraceServiceRequest();
            batch.Spans.Add(new Span { Name = new TruncatableString { Value = "Event1" } });
            batch.Node = new Node {Identifier = new ProcessIdentifier {Pid = 2}};

            await grpcWriter.Write(batch).ConfigureAwait(false);

            // ASSERT
            Common.AssertIsTrueEventually(
                () => input.GetStats().BatchesReceived == 1 && batchesReceived == 1 &&
                      receivedBatch.Node.Identifier.Pid == 2 &&
                      receivedBatch.Spans.Single().Name.Value == "Event1",
                      GrpcOpenCensusInputTests.DefaultTimeout);

            input.Stop();
            Assert.IsTrue(SpinWait.SpinUntil(() => !input.IsRunning, GrpcOpenCensusInputTests.DefaultTimeout));
        }

        [TestMethod]
        public async Task GrpcOpenCensusInputTests_ReceivesConfig()
        {
            // ARRANGE
            int configsReceived = 0;
            CurrentLibraryConfig receivedConfigRequest = null;

            int port = GetPort();
            var input = new GrpcOpenCensusInput("localhost", port);
            input.Start(
                (telemetryBatch, callContext) => null,
                (configRequest, callContext) =>
                {
                    configsReceived++;
                    receivedConfigRequest = configRequest;
                    return ConfigResponse;
                });
            Assert.IsTrue(SpinWait.SpinUntil(() => input.IsRunning, GrpcOpenCensusInputTests.DefaultTimeout));

            var grpcWriter = new GrpcWriter(false, port);

            // ACT
            CurrentLibraryConfig request =
                new CurrentLibraryConfig
                {
                    Config = new TraceConfig { RateLimitingSampler = new RateLimitingSampler{Qps = 1}}
                };

            await grpcWriter.Write(request).ConfigureAwait(false);

            // ASSERT
            Common.AssertIsTrueEventually(
                () => input.GetStats().ConfigsReceived == 1 && configsReceived == 1 &&
                      receivedConfigRequest.Config.RateLimitingSampler.Qps == 1, 
                GrpcOpenCensusInputTests.DefaultTimeout);

            input.Stop();
            Assert.IsTrue(SpinWait.SpinUntil(() => !input.IsRunning, GrpcOpenCensusInputTests.DefaultTimeout));
        }

        [TestMethod]
        public async Task GrpcOpenCensusInputTests_ReceivesConfigWithNode()
        {
            // ARRANGE
            int configsReceived = 0;
            CurrentLibraryConfig receivedConfigRequest = null;

            int port = GetPort();
            var input = new GrpcOpenCensusInput("localhost", port);
            input.Start(
                (telemetryBatch, callContext) => null,
                (configRequest, callContext) =>
                {
                    configsReceived++;
                    receivedConfigRequest = configRequest;
                    return ConfigResponse;
                });
            Assert.IsTrue(SpinWait.SpinUntil(() => input.IsRunning, GrpcOpenCensusInputTests.DefaultTimeout));

            var grpcWriter = new GrpcWriter(false, port);

            // ACT
            CurrentLibraryConfig request =
                new CurrentLibraryConfig
                {
                    Config = new TraceConfig { RateLimitingSampler = new RateLimitingSampler { Qps = 1 } },
                    Node = new Node { Identifier = new ProcessIdentifier {Pid = 2}}
                };

            await grpcWriter.Write(request).ConfigureAwait(false);

            // ASSERT
            Common.AssertIsTrueEventually(
                () => input.GetStats().ConfigsReceived == 1 && configsReceived == 1 &&
                      receivedConfigRequest.Node.Identifier.Pid == 2 &&
                      receivedConfigRequest.Config.RateLimitingSampler.Qps == 1,
                GrpcOpenCensusInputTests.DefaultTimeout);

            input.Stop();
            Assert.IsTrue(SpinWait.SpinUntil(() => !input.IsRunning, GrpcOpenCensusInputTests.DefaultTimeout));
        }

        [TestMethod]
        public void GrpcOpenCensusInputTests_ReceivesDataFromMultipleClients()
        {
            // ARRANGE
            int batchesReceived = 0;
            ExportTraceServiceRequest receivedBatch = null;
            
            int port = GetPort();
            var input = new GrpcOpenCensusInput("localhost", port);
            input.Start(
                (exportSpanRequest, callContext) =>
                {
                    Interlocked.Increment(ref batchesReceived);
                    receivedBatch = exportSpanRequest;
                    return new ExportTraceServiceResponse();
                },
                (configRequest, callContext) => ConfigResponse);
            Assert.IsTrue(SpinWait.SpinUntil(() => input.IsRunning, GrpcOpenCensusInputTests.DefaultTimeout));

            // ACT
            ExportTraceServiceRequest batch = new ExportTraceServiceRequest();
            batch.Spans.Add(new Span() {Name = new TruncatableString() {Value = "Event1"}});

            Parallel.For(0, 1000, new ParallelOptions() {MaxDegreeOfParallelism = 1000}, async i =>
            {
                var grpcWriter = new GrpcWriter(false, port);

                await grpcWriter.Write(batch).ConfigureAwait(false);
            });

            // ASSERT
            Common.AssertIsTrueEventually(
                () => input.GetStats().BatchesReceived == 1000 && batchesReceived == 1000, GrpcOpenCensusInputTests.DefaultTimeout);

            input.Stop();
            Assert.IsTrue(SpinWait.SpinUntil(() => !input.IsRunning, GrpcOpenCensusInputTests.DefaultTimeout));
        }

        [TestMethod]
        public async Task GrpcOpenCensusInputTests_StopsWhileWaitingForData()
        {
            // ARRANGE
            int batchesReceived = 0;
            ExportTraceServiceRequest receivedBatch = null;

            int port = GetPort();
            var input = new GrpcOpenCensusInput("localhost", port);

            input.Start(
                (exportSpanRequest, callContext) =>
                {
                    batchesReceived++;
                    receivedBatch = exportSpanRequest;
                    return new ExportTraceServiceResponse();
                },
                (configRequest, callContext) => ConfigResponse);

            Assert.IsTrue(SpinWait.SpinUntil(() => input.IsRunning, GrpcOpenCensusInputTests.DefaultTimeout));

            var grpcWriter = new GrpcWriter(false, port);

            ExportTraceServiceRequest batch = new ExportTraceServiceRequest();
            batch.Spans.Add(new Span() { Name = new TruncatableString() { Value = "Event1" } });

            await grpcWriter.Write(batch).ConfigureAwait(false);

            Common.AssertIsTrueEventually(
                () => input.GetStats().BatchesReceived == 1 && batchesReceived == 1 &&
                      receivedBatch.Spans.Single().Name.Value == "Event1", GrpcOpenCensusInputTests.DefaultTimeout);

            // ACT
            input.Stop();
            
            // ASSERT
            Common.AssertIsTrueEventually(
                () => !input.IsRunning && input.GetStats().BatchesReceived == 1 && batchesReceived == 1 &&
                      receivedBatch.Spans.Single().Name.Value == "Event1", GrpcOpenCensusInputTests.DefaultTimeout);

            Common.AssertIsTrueEventually(() => input.GetStats().BatchesResponsesSent == 1);
        }

        [TestMethod]
        public async Task GrpcOpenCensusInputTests_StopsAndRestarts()
        {
            // ARRANGE
            int batchesReceived = 0;
            ExportTraceServiceRequest receivedBatch = null;

            int port = GetPort();
            var input = new GrpcOpenCensusInput("localhost", port);

            input.Start(
                (exportSpanRequest, callContext) =>
                {
                    batchesReceived++;
                    receivedBatch = exportSpanRequest;
                    return new ExportTraceServiceResponse();
                },
                (configRequest, callContext) => ConfigResponse);

            Assert.IsTrue(SpinWait.SpinUntil(() => input.IsRunning, GrpcOpenCensusInputTests.DefaultTimeout));

            var grpcWriter = new GrpcWriter(false, port);

            ExportTraceServiceRequest batch = new ExportTraceServiceRequest();
            batch.Spans.Add(new Span() { Name = new TruncatableString() { Value = "Event1" } });

            await grpcWriter.Write(batch).ConfigureAwait(false);

            Common.AssertIsTrueEventually(
                () => input.GetStats().BatchesReceived == 1 && batchesReceived == 1 &&
                      receivedBatch.Spans.Single().Name.Value == "Event1", GrpcOpenCensusInputTests.DefaultTimeout);

            // ACT
            input.Stop();

            Common.AssertIsTrueEventually(
                () => !input.IsRunning && input.GetStats().BatchesReceived == 1 && batchesReceived == 1 &&
                      receivedBatch.Spans.Single().Name.Value == "Event1", GrpcOpenCensusInputTests.DefaultTimeout);

            input.Start(
                (exportSpanRequest, callContext) =>
                { 
                    batchesReceived++;
                    receivedBatch = exportSpanRequest;
                    return new ExportTraceServiceResponse();
                },
                (configRequest, callContext) => ConfigResponse);

            Assert.IsTrue(SpinWait.SpinUntil(() => input.IsRunning, GrpcOpenCensusInputTests.DefaultTimeout));

            grpcWriter = new GrpcWriter(false, port);
            batch.Spans.Single().Name.Value = "Event2";
            await grpcWriter.Write(batch).ConfigureAwait(false);

            // ASSERT
            Common.AssertIsTrueEventually(
                () => input.IsRunning && input.GetStats().BatchesReceived == 1 && batchesReceived == 2 &&
                      receivedBatch.Spans.Single().Name.Value == "Event2", GrpcOpenCensusInputTests.DefaultTimeout);
            Common.AssertIsTrueEventually(() => input.GetStats().BatchesResponsesSent == 1);
        }

        [TestMethod]
        public async Task GrpcOpenCensusInputTests_HandlesExceptionsInSpanRequestsProcessingHandler()
        {
            // ARRANGE
            int port = GetPort();
            var input = new GrpcOpenCensusInput("localhost", port);

            input.Start((exportSpanRequest, callContext) => throw new InvalidOperationException(), (configRequest, callContext) => ConfigResponse);

            Assert.IsTrue(SpinWait.SpinUntil(() => input.IsRunning, GrpcOpenCensusInputTests.DefaultTimeout));

            var grpcWriter = new GrpcWriter(false, port);

            ExportTraceServiceRequest batch = new ExportTraceServiceRequest();
            batch.Spans.Add(new Span() { Name = new TruncatableString() { Value = "Event1" } });

            // ACT
            await grpcWriter.Write(batch).ConfigureAwait(false);

            // ASSERT

            // must have handled the exception by logging it
            // should still be able to process items
            Common.AssertIsTrueEventually(
                () => input.IsRunning && input.GetStats().BatchesReceived == 0 && input.GetStats().BatchesFailed == 1,
                GrpcOpenCensusInputTests.DefaultTimeout);

            await grpcWriter.Write(batch).ConfigureAwait(false);

            Common.AssertIsTrueEventually(
                () => input.IsRunning && input.GetStats().BatchesReceived == 0 && input.GetStats().BatchesFailed == 2,
                GrpcOpenCensusInputTests.DefaultTimeout);
            Assert.AreEqual(0, input.GetStats().BatchesResponsesSent);
        }

        [TestMethod]
        public async Task GrpcOpenCensusInputTests_HandlesExceptionsInConfigRequestsProcessingHandler()
        {
            // ARRANGE
            int port = GetPort();
            var input = new GrpcOpenCensusInput("localhost", port);

            input.Start((exportSpanRequest, callContext) => null, (cr, cc) => throw new InvalidOperationException());

            Assert.IsTrue(SpinWait.SpinUntil(() => input.IsRunning, GrpcOpenCensusInputTests.DefaultTimeout));

            var grpcWriter = new GrpcWriter(false, port);

            CurrentLibraryConfig configRequest = new CurrentLibraryConfig
            {
                Config = new TraceConfig {RateLimitingSampler = new RateLimitingSampler {Qps = 1}}
            };

            // ACT
            await grpcWriter.Write(configRequest).ConfigureAwait(false);

            // ASSERT

            // must have handled the exception by logging it
            // should still be able to process items
            Common.AssertIsTrueEventually(
                () => input.IsRunning && input.GetStats().ConfigsReceived == 0 && input.GetStats().ConfigsFailed == 1,
                GrpcOpenCensusInputTests.DefaultTimeout);

            await grpcWriter.Write(configRequest).ConfigureAwait(false);

            Common.AssertIsTrueEventually(
                () => input.IsRunning && input.GetStats().ConfigsReceived == 0 && input.GetStats().ConfigsFailed == 2,
                GrpcOpenCensusInputTests.DefaultTimeout);
        }

        private static int GetPort()
        {
            // dynamic port range
            return Rand.Next(49152, 65535);
        }
    }
}
