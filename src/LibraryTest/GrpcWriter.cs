namespace Microsoft.LocalForwarder.LibraryTest
{
    using Grpc.Core;
    using LocalForwarder.Library.Inputs.Contracts;
    using Opencensus.Proto.Agent.Trace.V1;
    using System;
    using System.Threading.Tasks;

    public class GrpcWriter
    {
        private readonly bool aiMode;

        readonly AsyncDuplexStreamingCall<TelemetryBatch, AiResponse> aiStreamingCall;
        readonly AsyncDuplexStreamingCall<ExportTraceServiceRequest, ExportTraceServiceResponse> openCensusExportStreamingCall;
        readonly AsyncDuplexStreamingCall<ConfigTraceServiceRequest, ConfigTraceServiceResponse> openCensusConfigStreamingCall;
        private int port;

        public GrpcWriter(bool aiMode, int port)
        {
            this.aiMode = aiMode;
            this.port = port;

            try
            {
                var channel = new Channel($"127.0.0.1:{this.port}", ChannelCredentials.Insecure);

                if (aiMode)
                {
                    var client = new AITelemetryService.AITelemetryServiceClient(channel);
                    this.aiStreamingCall = client.SendTelemetryBatch();
                }
                else
                {
                    // OpenCensus
                    var client = new TraceService.TraceServiceClient(channel);
                    this.openCensusExportStreamingCall = client.Export();
                    this.openCensusConfigStreamingCall = client.Config();
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

        public async Task Write(ConfigTraceServiceRequest config)
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
    }
}
