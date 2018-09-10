namespace Microsoft.LocalForwarder.Library.Inputs.GrpcInput
{
    using Opencensus.Proto.Agent.Trace.V1;

    class GrpcOpenCensusInput : GrpcInput<ExportTraceServiceRequest, ExportTraceServiceResponse>
    {
        public GrpcOpenCensusInput(string host, int port) : base(host, port)
        {
        }
    }
}