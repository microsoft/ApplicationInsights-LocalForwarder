namespace Microsoft.LocalForwarder.Library
{
    using ApplicationInsights;
    using ApplicationInsights.Channel;
    using Common;
    using Inputs.Contracts;
    using Inputs.GrpcInput;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
    using Opencensus.Proto.Exporter;
    using Opencensus.Proto.Trace;
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Exception = System.Exception;

    public class Library
    {
        private readonly TelemetryClient telemetryClient;

        private readonly GrpcAiInput gRpcAiInput = null;
        private readonly GrpcOpenCensusInput gRpcOpenCensusInput = null;

        private readonly Configuration config;
        private readonly string ocToAiInstrumentationKey;
        private readonly string liveMetricsStreamInstrumentationKey;

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

            Diagnostics.LogInfo(
                FormattableString.Invariant($"Loaded configuration. {Environment.NewLine}{configuration}"));

            try
            {
                var activeConfiguration = TelemetryConfiguration.Active;
                activeConfiguration.InstrumentationKey = this.liveMetricsStreamInstrumentationKey;

                QuickPulseTelemetryProcessor processor = null;

                activeConfiguration.TelemetryProcessorChainBuilder
                    .Use((next) =>
                    {
                        processor = new QuickPulseTelemetryProcessor(next);
                        return processor;
                    })
                    .Build();

                var quickPulseModule = new QuickPulseTelemetryModule();
                quickPulseModule.Initialize(activeConfiguration);
                quickPulseModule.RegisterTelemetryProcessor(processor);

                this.telemetryClient = new TelemetryClient();
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
                if (this.config.ApplicationInsightsInput_Enabled)
                {
                    this.gRpcAiInput = new GrpcAiInput(this.config.ApplicationInsightsInput_Host, this.config.ApplicationInsightsInput_Port);

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
                if (this.config.OpenCensusInput_Enabled)
                {
                    this.gRpcOpenCensusInput = new GrpcOpenCensusInput(this.config.OpenCensusInput_Host, this.config.OpenCensusInput_Port);

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
                    this.gRpcAiInput?.Start(this.OnAiBatchReceived);
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
                    this.gRpcOpenCensusInput?.Start(this.OnOcBatchReceived);
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

            Task.Run(async () => await this.TraceStatsWorker().ConfigureAwait(false));

            this.IsRunning = true;
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
        private void OnAiBatchReceived(TelemetryBatch batch)
        {
            try
            {
                // send incoming telemetry items to the telemetryClient
                foreach (Telemetry telemetry in batch.Items)
                {
                    ITelemetry convertedTelemetry = null;

                    try
                    {
                        //!!!
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
        }

        /// <summary>
        /// Processes an incoming telemetry batch for OpenCensus channel.
        /// </summary>
        /// <remarks>This method may be called from multiple threads concurrently.</remarks>
        private void OnOcBatchReceived(ExportSpanRequest batch)
        {
            try
            {
                // send incoming telemetry items to the telemetryClient
                foreach (Span span in batch.Spans)
                {
                    try
                    {
                        //!!!
                        Diagnostics.LogTrace($"OpenCensus message received: {batch.Spans.Count} spans, first span: {batch.Spans.First().Name}");

                        this.telemetryClient.TrackSpan(span, this.ocToAiInstrumentationKey);
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
