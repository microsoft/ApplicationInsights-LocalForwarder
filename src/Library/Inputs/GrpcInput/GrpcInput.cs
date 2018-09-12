namespace Microsoft.LocalForwarder.Library.Inputs.GrpcInput
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Contracts;
    using Grpc.Core;
    using Opencensus.Proto.Agent.Trace.V1;

    /// <summary>
    /// gRpc-based input
    /// </summary>
    class GrpcInput<TTelemetryBatch, TResponse>
    {
        private readonly GrpcAiServer aiServer = null;
        private readonly GrpcOpenCensusServer openCensusServer = null;

        private CancellationTokenSource cts;
        private Action<TTelemetryBatch, ServerCallContext> onBatchReceived;
        private Func<CurrentLibraryConfig, ServerCallContext, UpdatedLibraryConfig> onConfigReceived;
        private Server server;
        private InputStats stats;
        private readonly string host;
        private readonly int port;

        public GrpcInput(string host, int port)
        {
            this.host = host;
            this.port = port;

            if (typeof(TTelemetryBatch) == typeof(TelemetryBatch))
            {
                this.aiServer = new GrpcAiServer(
                    async (IAsyncStreamReader<TelemetryBatch> requestStream, IServerStreamWriter<AiResponse> responseStream, ServerCallContext context)
                        => await this.OnSendTelemetryBatch((IAsyncStreamReader<TTelemetryBatch>) requestStream,
                            (IServerStreamWriter<TResponse>) responseStream,
                            context).ConfigureAwait(false)
                );
            }
            else if (typeof(TTelemetryBatch) == typeof(ExportTraceServiceRequest))
            {
                this.openCensusServer = new GrpcOpenCensusServer(
                    async (requestStream, responseStream, context)
                        => await this.OnSendTelemetryBatch((IAsyncStreamReader<TTelemetryBatch>) requestStream,
                            (IServerStreamWriter<TResponse>) responseStream,
                            context).ConfigureAwait(false),

                    async (configRequestStream, configResponseStream, context)
                        => await this.OnSendConfig(
                            configRequestStream,
                            configResponseStream,
                            context).ConfigureAwait(false)
                    );
            }
            else
            {
                throw new ArgumentException(FormattableString.Invariant($"Grpc contract is not supported by the input. Unsupported type: {typeof(TTelemetryBatch)}"));
            }
        }

        public void Start(
            Action<TTelemetryBatch, ServerCallContext> onBatchReceived,
            Func<CurrentLibraryConfig, ServerCallContext, UpdatedLibraryConfig> onConfigReceived)
        {
            if (this.IsRunning)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant($"Can't Start the input, it's already running"));
            }

            this.stats = new InputStats();
            this.cts = new CancellationTokenSource();
            this.onBatchReceived = onBatchReceived;
            this.onConfigReceived = onConfigReceived;
            try
            {
                this.server = new Server
                {
                    Services = {this.aiServer != null ? AITelemetryService.BindService(this.aiServer) : TraceService.BindService(this.openCensusServer)},
                    Ports = {new ServerPort(this.host, this.port, ServerCredentials.Insecure)}
                };

                this.server.Start();

                this.IsRunning = true;
            }
            catch (System.Exception e)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant($"Could not initialize the gRPC server. {e.ToString()}"));
            }
        }

        public void Stop()
        {
            if (!this.IsRunning)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant($"Can't Stop the input, it's not currently running"));
            }

            try
            {
                this.server.KillAsync().Wait(TimeSpan.FromSeconds(5));
            }
            finally
            {

                this.cts.Cancel();

                this.IsRunning = false;
            }
        }

        public bool IsRunning { get; private set; }

        public InputStats GetStats()
        {
            return this.stats;
        }

        private async Task OnSendTelemetryBatch(IAsyncStreamReader<TTelemetryBatch> requestStream,
            IServerStreamWriter<TResponse> responseStream,
            ServerCallContext context)
        {
            try
            {
                while (await requestStream.MoveNext(this.cts.Token).ConfigureAwait(false))
                {
                    TTelemetryBatch batch = requestStream.Current;

                    try
                    {
                        this.onBatchReceived?.Invoke(batch, context);

                        Interlocked.Increment(ref this.stats.BatchesReceived);
                    }
                    catch (System.Exception e)
                    {
                        // unexpected exception occured while processing the batch
                        Interlocked.Increment(ref this.stats.BatchesFailed);

                        Diagnostics.LogError(FormattableString.Invariant($"Unknown exception while processing a batch received through the gRpc input. {e.ToString()}"));
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // we have been stopped

            }
            catch (System.Exception e)
            {
                // unexpected exception occured
                Diagnostics.LogError(FormattableString.Invariant($"Unknown exception while reading from gRpc stream. {e.ToString()}"));

                this.Stop();
            }
        }

        private async Task OnSendConfig(IAsyncStreamReader<CurrentLibraryConfig> requestStream,
            IServerStreamWriter<UpdatedLibraryConfig> responseStream,
            ServerCallContext context)
        {
            if (this.onConfigReceived == null)
            {
                return;
            }

            try
            {
                while (await requestStream.MoveNext(this.cts.Token).ConfigureAwait(false))
                {
                    CurrentLibraryConfig configRequest = requestStream.Current;

                    try
                    {
                        await responseStream.WriteAsync(this.onConfigReceived(configRequest, context)).ConfigureAwait(false);

                        Interlocked.Increment(ref this.stats.ConfigsReceived);
                    }
                    catch (System.Exception e)
                    {
                        // unexpected exception occured while processing the config request
                        Interlocked.Increment(ref this.stats.ConfigsFailed);

                        Diagnostics.LogError(FormattableString.Invariant($"Unknown exception while processing a config request through the OpenCensus gRpc input. {e.ToString()}"));
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // we have been stopped
            }
            catch (System.Exception e)
            {
                // unexpected exception occured
                Diagnostics.LogError(FormattableString.Invariant($"Unknown exception while reading config from gRpc stream. {e.ToString()}"));

                this.Stop();
            }
        }
        #region gRPC servers

        private class GrpcAiServer : AITelemetryService.AITelemetryServiceBase
        {
            private readonly
                Func<IAsyncStreamReader<TelemetryBatch>, IServerStreamWriter<AiResponse>, ServerCallContext, Task>
                onSendTelemetryBatch;

            public GrpcAiServer(
                Func<IAsyncStreamReader<TelemetryBatch>, IServerStreamWriter<AiResponse>, ServerCallContext, Task>
                    onSendTelemetryBatch)
            {
                this.onSendTelemetryBatch =
                    onSendTelemetryBatch ?? throw new ArgumentNullException(nameof(onSendTelemetryBatch));
            }

            public override async Task SendTelemetryBatch(IAsyncStreamReader<TelemetryBatch> requestStream,
                IServerStreamWriter<AiResponse> responseStream,
                ServerCallContext context)
            {
                await this.onSendTelemetryBatch.Invoke(requestStream, responseStream, context).ConfigureAwait(false);
            }
        }

        private class GrpcOpenCensusServer : TraceService.TraceServiceBase
        {
            private readonly
                Func<IAsyncStreamReader<ExportTraceServiceRequest>, IServerStreamWriter<ExportTraceServiceResponse>, ServerCallContext, Task
                >
                onSendTelemetryBatch;

            private readonly
                Func<IAsyncStreamReader<CurrentLibraryConfig>, IServerStreamWriter<UpdatedLibraryConfig>, ServerCallContext, Task
                >
                onConfigRequest;

            public GrpcOpenCensusServer(
                Func<IAsyncStreamReader<ExportTraceServiceRequest>, IServerStreamWriter<ExportTraceServiceResponse>, ServerCallContext, Task
                    >
                    onSendTelemetryBatch,
                Func<IAsyncStreamReader<CurrentLibraryConfig>, IServerStreamWriter<UpdatedLibraryConfig>, ServerCallContext, Task
                    >
                    onConfigRequest)
            {
                this.onSendTelemetryBatch =
                    onSendTelemetryBatch ?? throw new ArgumentNullException(nameof(onSendTelemetryBatch));

                this.onConfigRequest =
                    onConfigRequest ?? throw new ArgumentNullException(nameof(onConfigRequest));
            }

            public override async Task Export(IAsyncStreamReader<ExportTraceServiceRequest> requestStream,
                IServerStreamWriter<ExportTraceServiceResponse> responseStream,
                ServerCallContext context)
            {
                await this.onSendTelemetryBatch.Invoke(requestStream, responseStream, context).ConfigureAwait(false);
            }

            public override async Task Config(IAsyncStreamReader<CurrentLibraryConfig> configRequestStream,
                IServerStreamWriter<UpdatedLibraryConfig> configResponseStream,
                ServerCallContext context)
            {
                await this.onConfigRequest.Invoke(configRequestStream, configResponseStream, context)
                    .ConfigureAwait(false);
            }
        }

        #endregion
    }
}