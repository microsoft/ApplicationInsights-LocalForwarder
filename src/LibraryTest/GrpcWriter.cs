namespace Microsoft.LocalForwarder.LibraryTest
{
    using Grpc.Core;
    using LocalForwarder.Library.Inputs.Contracts;
    using Microsoft.LocalForwarder.Common;
    using Opencensus.Proto.Agent.Trace.V1;
    using System;
    using System.Threading.Tasks;

    public class GrpcWriter : IDisposable
    {
        private readonly bool aiMode;

        readonly AsyncDuplexStreamingCall<TelemetryBatch, AiResponse> aiStreamingCall;
        readonly AsyncDuplexStreamingCall<ExportTraceServiceRequest, ExportTraceServiceResponse> openCensusExportStreamingCall;
        readonly AsyncDuplexStreamingCall<CurrentLibraryConfig, UpdatedLibraryConfig> openCensusConfigStreamingCall;
        private int port;
        private readonly Channel channel;
        private readonly AITelemetryService.AITelemetryServiceClient aiClient;
        private readonly TraceService.TraceServiceClient ocClient;

        public GrpcWriter(bool aiMode, int port)
        {
            this.aiMode = aiMode;
            this.port = port;

            try
            {
                this.channel = new Channel($"127.0.0.1:{this.port}", ChannelCredentials.Insecure);

                if (aiMode)
                {
                    this.aiClient = new AITelemetryService.AITelemetryServiceClient(channel);
                    this.aiStreamingCall = this.aiClient.SendTelemetryBatch();
                }
                else
                {
                    // OpenCensus
                    this.ocClient = new TraceService.TraceServiceClient(channel);
                    this.openCensusExportStreamingCall = this.ocClient.Export();
                    this.openCensusConfigStreamingCall = this.ocClient.Config();
                }
            }
            catch (System.Exception e)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant($"Error initializing the gRpc test client. {e.ToString()}"));
            }
        }

        public async Task Write(TelemetryBatch batch)
        {
            if (!this.aiMode)
            {
                throw new InvalidOperationException("Incorrect mode");
            }

            try
            {
                await this.aiStreamingCall.RequestStream.WriteAsync(batch).ConfigureAwait(false);
            }
            catch (System.Exception e)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant($"Error sending a message via gRpc. {e.ToString()}"));
            }
        }

        public async Task Write(ExportTraceServiceRequest batch)
        {
            if (this.aiMode)
            {
                throw new InvalidOperationException("Incorrect mode");
            }

            try
            {
                await this.openCensusExportStreamingCall.RequestStream.WriteAsync(batch).ConfigureAwait(false);
            }
            catch (System.Exception e)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant($"Error sending a message via gRpc. {e.ToString()}"));
            }
        }

        public async Task Write(CurrentLibraryConfig config)
        {
            if (this.aiMode)
            {
                throw new InvalidOperationException("Incorrect mode");
            }

            try
            {
                await this.openCensusConfigStreamingCall.RequestStream.WriteAsync(config).ConfigureAwait(false);
            }
            catch (System.Exception e)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant($"Error sending a message via gRpc. {e.ToString()}"));
            }
        }

        public void Dispose()
        {
            this.aiStreamingCall?.Dispose();
            this.openCensusExportStreamingCall?.Dispose();
            this.openCensusConfigStreamingCall?.Dispose();

            try
            {
                this.channel.ShutdownAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch (System.Exception e)
            {
                Diagnostics.LogError(FormattableString.Invariant($"Could not stop the gRPC writer's channel. {e.ToString()}"));
            }
        }
    }
}
