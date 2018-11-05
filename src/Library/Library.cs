using Microsoft.LocalForwarder.Library.Utils;

namespace Microsoft.LocalForwarder.Library
{
    using ApplicationInsights;
    using ApplicationInsights.Channel;
    using Common;
    using Inputs.Contracts;
    using Inputs.GrpcInput;
    using Grpc.Core;

    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;

    using Opencensus.Proto.Agent.Common.V1;
    using Opencensus.Proto.Agent.Trace.V1;
    using Opencensus.Proto.Trace.V1;

    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Exception = System.Exception;
    using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;

    public class Library
    {
        private readonly TelemetryClient telemetryClient;

        private readonly GrpcAiInput gRpcAiInput = null;
        private readonly GrpcOpenCensusInput gRpcOpenCensusInput = null;

        private readonly Configuration config;
        private readonly string ocToAiInstrumentationKey;
        private readonly string liveMetricsStreamInstrumentationKey;
        private readonly string liveMetricsStreamAuthenticationApiKey;
        private readonly OpenCensusClientCache<string, Node> opencensusPeers;
        private readonly TraceConfig DefaultOpencensusConfig;

        private readonly TimeSpan statsTracingTimeout = TimeSpan.FromMinutes(1);
        
        /// <summary>
        /// For unit tests only.
        /// </summary>
        internal Library(string configuration, TelemetryClient telemetryClient, TimeSpan? statsTracingTimeout = null) : this(configuration)
        {
            this.telemetryClient = telemetryClient;
            this.statsTracingTimeout = statsTracingTimeout ?? this.statsTracingTimeout;
        }

        public bool IsRunning { get; private set; } = false;

        public Library(string configuration)
        {
            this.config = new Configuration(configuration);

            this.ocToAiInstrumentationKey = config.OpenCensusToApplicationInsights_InstrumentationKey;
            this.liveMetricsStreamInstrumentationKey = config.ApplicationInsights_LiveMetricsStreamInstrumentationKey;
            this.liveMetricsStreamAuthenticationApiKey = config.ApplicationInsights_LiveMetricsStreamAuthenticationApiKey;
            this.opencensusPeers = new OpenCensusClientCache<string, Node>();
            this.DefaultOpencensusConfig = new TraceConfig
                {
                    ConstantSampler = new ConstantSampler
                    {
                        Decision = true
                    }
                };

            Diagnostics.LogInfo(
                FormattableString.Invariant($"Loaded configuration. {Environment.NewLine}{configuration}"));

            try
            {
                var activeConfiguration = TelemetryConfiguration.Active;
                activeConfiguration.InstrumentationKey = this.liveMetricsStreamInstrumentationKey;

                var channel = new ServerTelemetryChannel();
                channel.Initialize(activeConfiguration);
                activeConfiguration.TelemetryChannel = channel;

                var builder = activeConfiguration.DefaultTelemetrySink.TelemetryProcessorChainBuilder;

                QuickPulseTelemetryProcessor processor = null;
                builder.Use((next) =>
                {
                    processor = new QuickPulseTelemetryProcessor(next);
                    return processor;
                });

                if (config.ApplicationInsights_AdaptiveSampling_Enabled == true)
                {
                    builder.UseAdaptiveSampling(config.ApplicationInsights_AdaptiveSampling_MaxOtherItemsPerSecond ?? 5, excludedTypes: "Event");
                    builder.UseAdaptiveSampling(config.ApplicationInsights_AdaptiveSampling_MaxEventsPerSecond ?? 5, includedTypes: "Event");
                }

                builder.Build();

                var quickPulseModule = new QuickPulseTelemetryModule() { AuthenticationApiKey = this.liveMetricsStreamAuthenticationApiKey };
                quickPulseModule.Initialize(activeConfiguration);
                quickPulseModule.RegisterTelemetryProcessor(processor);

                this.telemetryClient = new TelemetryClient(activeConfiguration);
            }
            catch (Exception e)
            {
                Diagnostics.LogError(
                    FormattableString.Invariant($"Could not initialize AI SDK. {e.ToString()}"));

                throw new InvalidOperationException(
                    FormattableString.Invariant($"Could not initialize AI SDK. {e.ToString()}"), e);
            }

            try
            {
                if (this.config.ApplicationInsightsInput_Enabled == true && this.config.ApplicationInsightsInput_Port.HasValue)
                {
                    this.gRpcAiInput = new GrpcAiInput(this.config.ApplicationInsightsInput_Host, this.config.ApplicationInsightsInput_Port.Value);

                    Diagnostics.LogInfo(
                        FormattableString.Invariant($"We will listen for AI data on {this.config.ApplicationInsightsInput_Host}:{this.config.ApplicationInsightsInput_Port}"));
                }
                else
                {
                    Diagnostics.LogInfo(
                        FormattableString.Invariant($"We will not listen for AI data"));
                }
            }
            catch (Exception e)
            {
                Diagnostics.LogError(
                    FormattableString.Invariant($"Could not create the gRPC AI channel. {e.ToString()}"));

                throw new InvalidOperationException(
                    FormattableString.Invariant($"Could not create the gRPC AI channel. {e.ToString()}"), e);
            }

            try
            {
                if (this.config.OpenCensusInput_Enabled == true && this.config.OpenCensusInput_Port.HasValue)
                {
                    this.gRpcOpenCensusInput = new GrpcOpenCensusInput(this.config.OpenCensusInput_Host, this.config.OpenCensusInput_Port.Value);

                    Diagnostics.LogInfo(
                        FormattableString.Invariant($"We will listen for OpenCensus data on {this.config.OpenCensusInput_Host}:{this.config.OpenCensusInput_Port}"));
                }
                else
                {
                    Diagnostics.LogInfo(
                        FormattableString.Invariant($"We will not listen for OpenCensus data"));
                }
            }
            catch (Exception e)
            {
                Diagnostics.LogError(
                    FormattableString.Invariant($"Could not create the gRPC OpenCensus channel. {e.ToString()}"));

                throw new InvalidOperationException(
                    FormattableString.Invariant($"Could not create the gRPC OpenCensus channel. {e.ToString()}"), e);
            }
        }

        public void Run()
        {
            if (this.IsRunning)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant($"Can't Run the library, it's already running"));
            }

            try
            {
                try
                {
                    this.gRpcAiInput?.Start(this.OnAiBatchReceived, null);
                }
                catch (Exception e)
                {
                    Diagnostics.LogError(
                        FormattableString.Invariant($"Could not start the gRPC AI channel. {e.ToString()}"));

                    throw new InvalidOperationException(
                        FormattableString.Invariant($"Could not start the gRPC AI channel. {e.ToString()}"), e);
                }

                try
                {
                    this.gRpcOpenCensusInput?.Start(this.OnOcBatchReceived, this.OnOcConfigReceived);
                }
                catch (Exception e)
                {
                    Diagnostics.LogError(
                        FormattableString.Invariant($"Could not start the gRPC OpenCensus channel. {e.ToString()}"));

                    throw new InvalidOperationException(
                        FormattableString.Invariant($"Could not start the gRPC OpenCensus channel. {e.ToString()}"), e);
                }
            }
            catch (Exception)
            {
                // something went wrong, so stop both inputs to ensure consistent state
                this.EmergencyShutdownAllInputs();

                throw;
            }

            this.IsRunning = true;

            Task.Run(async () => await this.TraceStatsWorker().ConfigureAwait(false));
        }

        public void Stop()
        {
            if (!this.IsRunning)
            {
                throw new InvalidOperationException(
                    FormattableString.Invariant($"Can't Stop the library, it's not currently running"));
            }

            try
            {
                try
                {
                    this.gRpcAiInput?.Stop();
                }
                catch (Exception e)
                {
                    Diagnostics.LogError(FormattableString.Invariant($"Could not stop the gRPC AI channel. {e.ToString()}"));

                    throw new InvalidOperationException(
                        FormattableString.Invariant($"Could not stop the gRPC AI channel. {e.ToString()}"), e);
                }

                try
                {
                    this.gRpcOpenCensusInput?.Stop();
                }
                catch (Exception e)
                {
                    Diagnostics.LogError(FormattableString.Invariant($"Could not stop the gRPC OpenCensus channel. {e.ToString()}"));

                    throw new InvalidOperationException(
                        FormattableString.Invariant($"Could not stop the gRPC OpenCensus channel. {e.ToString()}"), e);
                }
            }
            finally
            {
                this.IsRunning = false;
            }
        }

        /// <summary>
        /// Processes an incoming telemetry batch for AI channel.
        /// </summary>
        /// <remarks>This method may be called from multiple threads concurrently.</remarks>
        private AiResponse OnAiBatchReceived(TelemetryBatch batch, ServerCallContext callContext)
        {
            try
            {
                // send incoming telemetry items to the telemetryClient
                foreach (Telemetry telemetry in batch.Items)
                {
                    ITelemetry convertedTelemetry = null;

                    try
                    {
                        Diagnostics.LogTrace($"AI message received: {batch.Items.Count} items, first item: {batch.Items.First().InstrumentationKey}");

                        switch (telemetry.DataCase)
                        {
                            case Telemetry.DataOneofCase.Event:
                                convertedTelemetry = AiTelemetryConverter.ConvertEventToSdkApi(telemetry);
                                break;
                            case Telemetry.DataOneofCase.Message:
                                convertedTelemetry = AiTelemetryConverter.ConvertTraceToSdkApi(telemetry);
                                break;
                            case Telemetry.DataOneofCase.Metric:
                                convertedTelemetry = AiTelemetryConverter.ConvertMetricToSdkApi(telemetry);
                                break;
                            case Telemetry.DataOneofCase.Exception:
                                convertedTelemetry = AiTelemetryConverter.ConvertExceptionToSdkApi(telemetry);
                                break;
                            case Telemetry.DataOneofCase.Dependency:
                                convertedTelemetry = AiTelemetryConverter.ConvertDependencyToSdkApi(telemetry);
                                break;
                            case Telemetry.DataOneofCase.Availability:
                                convertedTelemetry = AiTelemetryConverter.ConvertAvailabilityToSdkApi(telemetry);
                                break;
                            case Telemetry.DataOneofCase.PageView:
                                convertedTelemetry = AiTelemetryConverter.ConvertPageViewToSdkApi(telemetry);
                                break;
                            case Telemetry.DataOneofCase.Request:
                                convertedTelemetry = AiTelemetryConverter.ConvertRequestToSdkApi(telemetry);
                                break;
                            case Telemetry.DataOneofCase.None:
                                throw new ArgumentException(
                                    FormattableString.Invariant($"Empty AI telemetry item encountered"));
                            default:
                                throw new ArgumentException(
                                    FormattableString.Invariant($"Unknown AI telemetry item type encountered"));
                        }
                    }
                    catch (Exception e)
                    {
                        // an unexpected issue during conversion
                        // log and carry on
                        Diagnostics.LogError(
                            FormattableString.Invariant(
                                $"Could not convert an incoming AI telemetry item. {e.ToString()}"));
                    }

                    try
                    {
                        if (convertedTelemetry != null)
                        {
                            this.telemetryClient.Track(convertedTelemetry);
                        }
                    }
                    catch (Exception e)
                    {
                        // an unexpected issue while tracking an item
                        // log and carry on
                        Diagnostics.LogError(
                            FormattableString.Invariant(
                                $"Could not track an incoming AI telemetry item. {e.ToString()}"));
                    }
                }
            }
            catch (Exception e)
            {
                // an unexpected issue while processing the batch
                // log and carry on
                Diagnostics.LogError(
                    FormattableString.Invariant(
                        $"Could not process an incoming AI telemetry batch. {e.ToString()}"));
            }

            return new AiResponse();
        }

        /// <summary>
        /// Processes incoming trace config request and responds with always sample config.
        /// </summary>
        /// <param name="configRequest">Incoming config request.</param>
        /// <param name="callContext">Call context.</param>
        /// <returns>LocalForwarder trace config.</returns>
        private UpdatedLibraryConfig OnOcConfigReceived(CurrentLibraryConfig configRequest,
            ServerCallContext callContext)
        {
            TryGetOrUpdatePeerInfo(configRequest.Node, callContext, out var _);

            Diagnostics.LogTrace($"Got config request from {callContext.Peer} with {configRequest.Config?.SamplerCase}");

            return new UpdatedLibraryConfig { Config = DefaultOpencensusConfig };
        }

        /// <summary>
        /// Gets or updates opencensus peer info. 
        /// </summary>
        /// <param name="originalNode">Node in the message (or null).</param>
        /// <param name="callContext">Call context.</param>
        /// <param name="peerInfo">Cached peer info (or the new one).</param>
        /// <returns>True is peer info was found/avaialble, false otherwise.</returns>
        private bool TryGetOrUpdatePeerInfo(Node originalNode, ServerCallContext callContext, out Node peerInfo)
        {
            if (originalNode != null)
            {
                this.telemetryClient.TrackNodeEvent(originalNode, callContext.Method, callContext.Peer, this.ocToAiInstrumentationKey);
                peerInfo = opencensusPeers.AddOrUpdate(callContext.Peer, originalNode);
                return true;
            }

            return opencensusPeers.TryGet(callContext.Peer, out peerInfo);
        }

        /// <summary>
        /// Processes an incoming telemetry batch for OpenCensus channel.
        /// </summary>
        /// <remarks>This method may be called from multiple threads concurrently.</remarks>
        private ExportTraceServiceResponse OnOcBatchReceived(ExportTraceServiceRequest batch, ServerCallContext callContext)
        {
            try
            {
                TryGetOrUpdatePeerInfo(batch.Node, callContext, out Node peerInfo);

                // send incoming telemetry items to the telemetryClient
                foreach (Span span in batch.Spans)
                {
                    try
                    {
                        Diagnostics.LogTrace($"OpenCensus message received: {batch.Spans.Count} spans, first span: {batch.Spans.FirstOrDefault()?.Name}");

                        this.telemetryClient.TrackSpan(span, peerInfo, this.ocToAiInstrumentationKey);
                    }
                    catch (Exception e)
                    {
                        // an unexpected issue while tracking an item
                        // log and carry on
                        Diagnostics.LogError(
                            FormattableString.Invariant(
                                $"Could not track an incoming OpenCensus telemetry item. {e.ToString()}"));
                    }
                }
            }
            catch (Exception e)
            {
                // an unexpected issue while processing the batch
                // log and carry on
                Diagnostics.LogError(
                    FormattableString.Invariant(
                        $"Could not process an incoming OpenCensus telemetry batch. {e.ToString()}"));
            }

            return new ExportTraceServiceResponse();
        }

        private async Task TraceStatsWorker()
        {
            while (this.IsRunning)
            {
                try
                {
                    if (this.gRpcAiInput?.IsRunning == true)
                    {
                        Common.Diagnostics.LogInfo($"AI input: [{this.gRpcAiInput.GetStats()}]");
                    }

                    if (this.gRpcOpenCensusInput?.IsRunning == true)
                    {
                        Common.Diagnostics.LogInfo($"OpenCensus input: [{this.gRpcOpenCensusInput.GetStats()}]");
                    }
                }
                catch (Exception e)
                {
                    Common.Diagnostics.LogInfo($"Unexpected exception in the stats thread: {e.ToString()}");
                }

                await Task.Delay(this.statsTracingTimeout).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Shuts down all inputs in case at least one of them failed.
        /// </summary>
        /// <remarks>We don't care about exceptions here, this is the best effort to clean things up.</remarks>
        private void EmergencyShutdownAllInputs()
        {
            try
            {
                this.gRpcAiInput?.Stop();
            }
            catch (Exception)
            {
                // swallow any further exceptions
            }

            try
            {
                this.gRpcOpenCensusInput?.Stop();
            }
            catch (Exception)
            {
                // swallow any further exceptions
            }
        }
    }
}
