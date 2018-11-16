namespace Microsoft.LocalForwarder.Library
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using ApplicationInsights;
    using ApplicationInsights.Channel;
    using ApplicationInsights.DataContracts;
    using ApplicationInsights.Extensibility.Implementation;
    using Google.Protobuf;
    using Opencensus.Proto.Agent.Common.V1;
    using Opencensus.Proto.Trace.V1;

    static class OpenCensusTelemetryConverter
    {
        public static class SpanAttributeConstants
        {
            public const string SpanKindKey = "span.kind";

            public const string ServerSpanKind = "server";
            public const string ClientSpanKind = "client";
            public const string ProducerSpanKind = "producer";
            public const string ConsumerSpanKind = "consumer";

            public const string HttpUrlKey = "http.url";
            public const string HttpMethodKey = "http.method";
            public const string HttpStatusCodeKey = "http.status_code";
            public const string HttpPathKey = "http.path";
            public const string HttpHostKey = "http.host";
            public const string HttpPortKey = "http.port";
            public const string HttpRouteKey = "http.route";
            public const string HttpUserAgentKey = "http.user_agent";

            public const string ErrorKey = "error";
            public const string ErrorStackTrace = "error.stack.trace";
        }

        private const string StatusDescriptionPropertyName = "statusDescription";
        private const string LinkPropertyName = "link";
        private const string LinkSpanIdPropertyName = "spanId";
        private const string LinkTraceIdPropertyName = "traceId";
        private const string LinkTypePropertyName = "type";
        private static readonly string AssemblyVersion = GetAssemblyVersionString();

        private const string PeerPropertyKey = "peer";
        private const string OpenCensusExporterVersionPropertyKey = "oc_exporter_version";
        private const string LocalForwarderVersion = "lf_version";
        private const string StartTimestampPropertyKey = "process_start_ts";

        private static readonly uint[] Lookup32 = CreateLookup32();

        private static readonly Dictionary<LibraryInfo.Types.Language, string> FriendlyLanguageNames =
            CacheLanguageNames();

        public static void TrackNodeEvent(this TelemetryClient telemetryClient, Node peerInfo, string eventName, string peer, string ikey)
        {
            EventTelemetry nodeEvent = new EventTelemetry(string.Concat(eventName, ".node"));
            SetPeerInfo(nodeEvent, peerInfo);

            nodeEvent.Properties[PeerPropertyKey] = peer;
            if (peerInfo.LibraryInfo != null)
            {
                nodeEvent.Properties[OpenCensusExporterVersionPropertyKey] = peerInfo.LibraryInfo.ExporterVersion;
                nodeEvent.Properties[LocalForwarderVersion] = AssemblyVersion;
            }

            if (peerInfo.Identifier?.StartTimestamp != null )
            {
                nodeEvent.Properties[StartTimestampPropertyKey] =
                    peerInfo.Identifier.StartTimestamp.ToDateTime().ToString("o");
            }

            if (peerInfo.Attributes != null)
            {
                foreach (var attribute in peerInfo.Attributes)
                {
                    if (!nodeEvent.Properties.ContainsKey(attribute.Key))
                    {
                        nodeEvent.Properties.Add(attribute);
                    }
                }
            }

            nodeEvent.Context.InstrumentationKey = ikey;
            telemetryClient.TrackEvent(nodeEvent);
        }

        public static void TrackSpan(this TelemetryClient telemetryClient, Span span, Node peerInfo, string ikey)
        {
            if (span == null)
            {
                return;
            }

            if (GetSpanKind(span) == Span.Types.SpanKind.Client)
            {
                telemetryClient.TrackDependencyFromSpan(span, peerInfo, ikey);
            }
            else
            {
                telemetryClient.TrackRequestFromSpan(span, peerInfo, ikey);
            }

            if (span.TimeEvents != null)
            {
                foreach (var evnt in span.TimeEvents.TimeEvent)
                {
                    telemetryClient.TrackTraceFromTimeEvent(evnt, span, peerInfo, ikey);
                }
            }
        }

        private static Span.Types.SpanKind GetSpanKind(Span span)
        {
            if (span.Attributes?.AttributeMap != null && span.Attributes.AttributeMap.TryGetValue(SpanAttributeConstants.SpanKindKey, out var value))
            {
                return value.StringValue?.Value == SpanAttributeConstants.ClientSpanKind ? Span.Types.SpanKind.Client : Span.Types.SpanKind.Server;
            }

            if (span.Kind == Span.Types.SpanKind.Unspecified)
            {
                if (span.SameProcessAsParentSpan.HasValue && !span.SameProcessAsParentSpan.Value)
                {
                    return Span.Types.SpanKind.Server;
                }

                return Span.Types.SpanKind.Client;
            }

            return span.Kind;
        }

        private static void TrackRequestFromSpan(this TelemetryClient telemetryClient, Span span, Node peerInfo, string ikey)
        {
            RequestTelemetry request = new RequestTelemetry();

            InitializeOperationTelemetry(request, span, peerInfo);
            SetTracestate(span.Tracestate, request);

            request.ResponseCode = span.Status?.Code.ToString();

            string host = null, method = null, path = null, route = null, url = null;
            int port = -1;
            bool isHttp = false;

            if (span.Attributes?.AttributeMap != null)
            {
                foreach (var attribute in span.Attributes.AttributeMap)
                {
                    if (attribute.Value == null)
                        continue;
                    
                    switch (attribute.Key)
                    {
                        case SpanAttributeConstants.HttpUrlKey:
                            url = attribute.Value.StringValue?.Value;
                            isHttp = true;
                            break;
                        case SpanAttributeConstants.HttpStatusCodeKey:
                            request.ResponseCode = attribute.Value.StringValue != null ? 
                                    attribute.Value.StringValue.Value :
                                    attribute.Value.IntValue.ToString();
                            isHttp = true;
                            break;
                        case SpanAttributeConstants.HttpUserAgentKey:
                            request.Context.User.UserAgent = attribute.Value.StringValue?.Value;
                            isHttp = true;
                            break;
                        case SpanAttributeConstants.HttpRouteKey:
                            route = attribute.Value.StringValue?.Value;
                            isHttp = true;
                            break;
                        case SpanAttributeConstants.HttpPathKey:
                            path = attribute.Value.StringValue?.Value;
                            isHttp = true;
                            break;
                        case SpanAttributeConstants.HttpMethodKey:
                            method = attribute.Value.StringValue?.Value;
                            isHttp = true;
                            break;
                        case SpanAttributeConstants.HttpHostKey:
                            host = attribute.Value.StringValue?.Value;
                            isHttp = true;
                            break;
                        case SpanAttributeConstants.HttpPortKey:
                            port = (int) attribute.Value.IntValue;
                            isHttp = true;
                            break;
                        case SpanAttributeConstants.ErrorKey:
                            if (attribute.Value.BoolValue)
                            {
                                request.Success = false;
                                if (string.IsNullOrEmpty(request.ResponseCode))
                                {
                                    request.ResponseCode = "-1";
                                }
                            }
                            break;
                        default:
                            SetCustomProperty(request.Properties, attribute);

                            break;
                    }
                }

                if (isHttp)
                {
                    if (url != null && Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var requestUrl))
                    {
                        if (requestUrl.IsAbsoluteUri)
                        {
                            request.Url = requestUrl;
                            request.Name = GetHttpTelemetryName(method, requestUrl.AbsolutePath, route);
                        }
                        else
                        {
                            request.Name = GetHttpTelemetryName(method, requestUrl.OriginalString, route);
                        }
                    }
                    else
                    {
                        request.Url = GetUrl(host, port, path);
                        request.Name = GetHttpTelemetryName(method, path, route);
                    }
                }
            }

            request.Context.InstrumentationKey = ikey;
            telemetryClient.TrackRequest(request);
        }

        private static void TrackDependencyFromSpan(this TelemetryClient telemetryClient, Span span, Node peerInfo, string ikey)
        {
            string host = GetHost(span.Attributes?.AttributeMap);
            if (IsApplicationInsightsUrl(host))
            {
                return;
            }

            DependencyTelemetry dependency = new DependencyTelemetry();

            // https://github.com/Microsoft/ApplicationInsights-dotnet/issues/876
            dependency.Success = null;

            InitializeOperationTelemetry(dependency, span, peerInfo);
            SetTracestate(span.Tracestate, dependency);

            dependency.ResultCode = span.Status?.Code.ToString();

            if (span.Attributes?.AttributeMap != null)
            {
                string method = null, path = null, url = null;
                int port = -1;

                bool isHttp = false;
                foreach (var attribute in span.Attributes.AttributeMap)
                {
                    if (attribute.Value == null)
                        continue;

                    switch (attribute.Key)
                    {
                        case SpanAttributeConstants.HttpUrlKey:
                            url = attribute.Value.StringValue?.Value;
                            break;
                        case SpanAttributeConstants.HttpStatusCodeKey:
                            dependency.ResultCode = attribute.Value.StringValue != null ?
                                    attribute.Value.StringValue.Value : 
                                    attribute.Value.IntValue.ToString(); 
                            isHttp = true;
                            break;
                        case SpanAttributeConstants.HttpPathKey:
                            path = attribute.Value.StringValue.Value;
                            isHttp = true;
                            break;
                        case SpanAttributeConstants.HttpMethodKey:
                            method = attribute.Value.StringValue.Value;
                            isHttp = true;
                            break;
                        case SpanAttributeConstants.HttpHostKey:
                            break;
                        case SpanAttributeConstants.HttpPortKey:
                            port = (int) attribute.Value.IntValue;
                            break;
                        case SpanAttributeConstants.ErrorKey:
                            if (attribute.Value != null && attribute.Value.BoolValue)
                            {
                                dependency.Success = false;
                            }

                            break;
                        default:
                            SetCustomProperty(dependency.Properties, attribute);
                            break;
                    }
                }

                dependency.Target = host;
                if (isHttp)
                {
                    dependency.Type = "Http";

                    if (url != null)
                    {
                        dependency.Data = url;
                        if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
                        {
                            dependency.Name = GetHttpTelemetryName(
                                method, 
                                uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString, 
                                null);
                        }
                    }
                    else
                    {
                        dependency.Data = GetUrl(host, port, path)?.ToString();
                        dependency.Name = GetHttpTelemetryName(method, path, null);
                    }
                }
            }

            dependency.Context.InstrumentationKey = ikey;
            telemetryClient.TrackDependency(dependency);
        }

        private static bool IsApplicationInsightsUrl(string host)
        {
            return host != null && (host.StartsWith("dc.services.visualstudio.com")
                   || host.StartsWith("rt.services.visualstudio.com"));
        }

        private static void TrackTraceFromTimeEvent(this TelemetryClient telemetryClient, Span.Types.TimeEvent evnt, Span span, Node peerInfo, string ikey)
        {
            Span.Types.TimeEvent.Types.Annotation annotation = evnt.Annotation;
            if (annotation != null)
            {
                telemetryClient.TrackTrace(span, evnt, peerInfo, annotation.Description.Value, ikey,
                    annotation.Attributes?.AttributeMap);
            }

            Span.Types.TimeEvent.Types.MessageEvent message = evnt.MessageEvent;
            if (message != null)
            {
                telemetryClient.TrackTrace(span, evnt, peerInfo,
                    $"MessageEvent. messageId: '{message.Id}', type: '{message.Type}', compressed size: '{message.CompressedSize}', uncompressed size: '{message.UncompressedSize}'", ikey);
            }
        }

        private static void TrackTrace(this TelemetryClient telemetryClient, 
            Span span, 
            Span.Types.TimeEvent evnt,
            Node peerInfo,
            string message,
            string ikey,
            IDictionary<string, AttributeValue> attributes = null)
        {
            TraceTelemetry trace = new TraceTelemetry(message);

            SetParentOperationContext(span, trace.Context.Operation);
            trace.Timestamp = evnt.Time?.ToDateTime() ?? DateTime.UtcNow;
            if (attributes != null)
            {
                foreach (var attribute in attributes)
                {
                    SetCustomProperty(trace.Properties, attribute);
                }
            }

            trace.Context.InstrumentationKey = ikey;

            SetPeerInfo(trace, peerInfo);
            telemetryClient.TrackTrace(trace);
        }

        private static void InitializeOperationTelemetry(OperationTelemetry telemetry, Span span, Node peerInfo)
        {
            telemetry.Name = span.Name?.Value;

            var now = DateTime.UtcNow;
            telemetry.Timestamp = span.StartTime?.ToDateTime() ?? now;
            var endTime = span.EndTime?.ToDateTime() ?? now;

            SetOperationContext(span, telemetry);
            telemetry.Duration = endTime - telemetry.Timestamp;

            if (span.Status != null)
            {
                telemetry.Success = span.Status.Code == 0;
                if (!string.IsNullOrEmpty(span.Status.Message))
                {
                    telemetry.Properties[StatusDescriptionPropertyName] = span.Status.Message;
                }
            }

            SetLinks(span.Links, telemetry.Properties);
            SetPeerInfo(telemetry, peerInfo);
        }

        private static void SetOperationContext(Span span, OperationTelemetry telemetry)
        {
            string traceId = BytesStringToHexString(span.TraceId);
            telemetry.Context.Operation.Id = BytesStringToHexString(span.TraceId);
            if (span.ParentSpanId != null && !span.ParentSpanId.IsEmpty && span.ParentSpanId.Any(b => b != 0))
            {
                telemetry.Context.Operation.ParentId = FormatId(traceId, BytesStringToHexString(span.ParentSpanId));
            }

            telemetry.Id = FormatId(traceId, BytesStringToHexString(span.SpanId));
        }

        private static void SetParentOperationContext(Span span, OperationContext context)
        {
            context.Id = BytesStringToHexString(span.TraceId);
            context.ParentId = FormatId(context.Id, BytesStringToHexString(span.SpanId));
        }

        private static string FormatId(string traceId, string spanId)
        {
            return string.Concat('|', traceId, '.', spanId, '.');
        }

        private static Uri GetUrl(string host, int port, string path)
        {
            if (string.IsNullOrEmpty(host))
            {
                return null;
            }

            string slash = string.Empty;
            if (!string.IsNullOrEmpty(path) && !path.StartsWith("/"))
            {
                slash = "/";
            }

            string scheme = port == 80 ? "http" : "https";
            if (port < 0 || port == 80 || port == 443)
            {
                return new Uri($"{scheme}://{host}{slash}{path}");
            }

            return new Uri($"{scheme}://{host}:{port}{slash}{path}");
        }

        private static string GetHttpTelemetryName(string method, string path, string route)
        {
            if (method == null && path == null && route == null)
            {
                return null;
            }

            if (path == null && route == null)
            {
                return method;
            }

            if (method == null)
            {
                return route ?? path;
            }

            return method + " " + (route ?? path);
        }

        private static void SetLinks(Span.Types.Links spanLinks, IDictionary<string, string> telemetryProperties)
        {
            if (spanLinks?.Link == null)
            {
                return;
            }

            // for now, we just put links to telemetry properties
            // link0_spanId = ...
            // link0_traceId = ...
            // link0_type = child | parent | other
            // link0_<attributeKey> = <attributeValue>
            // this is not convenient for querying data
            // We'll consider adding Links to operation telemetry schema

            int num = 0;
            foreach (var link in spanLinks.Link)
            {
                string prefix = $"{LinkPropertyName}{num++}_";
                telemetryProperties[prefix + LinkSpanIdPropertyName] = BytesStringToHexString(link.SpanId);
                telemetryProperties[prefix + LinkTraceIdPropertyName] = BytesStringToHexString(link.TraceId);
                telemetryProperties[prefix + LinkTypePropertyName] = link.Type.ToString();
                if (link.Attributes?.AttributeMap != null)
                {
                    foreach (var attribute in link.Attributes.AttributeMap)
                    {
                        SetCustomProperty(telemetryProperties, attribute, prefix);
                    }
                }
            }
        }

        private static string GetHost(IDictionary<string, AttributeValue> attributes)
        {
            if (attributes != null)
            {
                if (attributes.TryGetValue(SpanAttributeConstants.HttpUrlKey, out var urlAttribute))
                {
                    if (urlAttribute != null &&
                        Uri.TryCreate(urlAttribute.StringValue.Value, UriKind.Absolute, out var uri))
                    {
                        return uri.Host;
                    }
                }

                if (attributes.TryGetValue(SpanAttributeConstants.HttpHostKey, out var hostAttribute))
                {
                    return hostAttribute.StringValue?.Value;
                }
            }

            return null;
        }

        private static void SetCustomProperty(IDictionary<string ,string> telemetryProperties, KeyValuePair<string, AttributeValue> attribute, string prefix = null)
        {
            Debug.Assert(telemetryProperties != null);
            Debug.Assert(attribute.Value != null);

            if (telemetryProperties.ContainsKey(attribute.Key))
            {
                return;
            }

            switch (attribute.Value.ValueCase)
            {
                case AttributeValue.ValueOneofCase.StringValue:
                    telemetryProperties[prefix + attribute.Key] = attribute.Value.StringValue?.Value;
                    break;
                case AttributeValue.ValueOneofCase.BoolValue:
                    telemetryProperties[prefix + attribute.Key] = attribute.Value.BoolValue.ToString();
                    break;
                case AttributeValue.ValueOneofCase.IntValue:
                    telemetryProperties[prefix + attribute.Key] = attribute.Value.IntValue.ToString();
                    break;
            }
        }

        /// <summary>
        /// Converts protobuf ByteString to hex-encoded low string
        /// </summary>
        /// <returns>Hex string</returns>
        private static string BytesStringToHexString(ByteString bytes)
        {
            if (bytes == null)
            {
                return null;
            }

            // See https://stackoverflow.com/questions/311165/how-do-you-convert-a-byte-array-to-a-hexadecimal-string-and-vice-versa/24343727#24343727
            var result = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                var val = Lookup32[bytes[i]];
                result[2 * i] = (char)val;
                result[(2 * i) + 1] = (char)(val >> 16);
            }

            return new string(result);
        }

        private static uint[] CreateLookup32()
        {
            // See https://stackoverflow.com/questions/311165/how-do-you-convert-a-byte-array-to-a-hexadecimal-string-and-vice-versa/24343727#24343727
            var result = new uint[256];
            for (int i = 0; i < 256; i++)
            {
                string s = i.ToString("x2", CultureInfo.InvariantCulture);
                result[i] = s[0] + ((uint)s[1] << 16);
            }

            return result;
        }

        private static void SetPeerInfo(ITelemetry telemetry, Node peerInfo)
        {
            string libLanguage = null;
            string ocLibVersion = null;
            if (peerInfo != null)
            {
                if (peerInfo.ServiceInfo != null)
                {
                    telemetry.Context.Cloud.RoleName = peerInfo.ServiceInfo.Name;
                }

                if (peerInfo.Identifier != null)
                {
                    telemetry.Context.Cloud.RoleInstance = string.Concat(peerInfo.Identifier.HostName, '.', peerInfo.Identifier.Pid.ToString());
                }

                if (peerInfo.LibraryInfo != null)
                {
                    libLanguage = FriendlyLanguageNames[peerInfo.LibraryInfo.Language];
                    ocLibVersion = peerInfo.LibraryInfo.CoreLibraryVersion;
                }
            }

            if (string.IsNullOrEmpty(libLanguage))
            {
                libLanguage = FriendlyLanguageNames[LibraryInfo.Types.Language.Unspecified];
            }

            if (string.IsNullOrEmpty(ocLibVersion))
            {
                ocLibVersion = "0.0.0";
            }

            telemetry.Context.GetInternalContext().SdkVersion =
                string.Concat("lf_", libLanguage, "-oc:", ocLibVersion);
        }

        internal static string GetAssemblyVersionString()
        {
            // Since dependencySource is no longer set, sdk version is prepended with information which can identify whether RDD was collected by profiler/framework
            // For directly using TrackDependency(), version will be simply what is set by core
            Type converterType = typeof(OpenCensusTelemetryConverter);

            object[] assemblyCustomAttributes = converterType.Assembly.GetCustomAttributes(false);
            string versionStr = assemblyCustomAttributes
                .OfType<AssemblyFileVersionAttribute>()
                .First()
                .Version;

            Version version = new Version(versionStr);

            string postfix = version.Revision.ToString(CultureInfo.InvariantCulture);
            return version.ToString(3) + "-" + postfix;
        }

        private static void SetTracestate(Span.Types.Tracestate tracestate, ISupportProperties telemetry)
        {
            if (tracestate?.Entries != null)
            {
                foreach (var entry in tracestate.Entries)
                {
                    if (!telemetry.Properties.ContainsKey(entry.Key))
                    {
                        telemetry.Properties[entry.Key] = entry.Value;
                    }
                }
            }
        }

        private static Dictionary<LibraryInfo.Types.Language, string> CacheLanguageNames()
        {
            var values = (LibraryInfo.Types.Language[])Enum.GetValues(typeof(LibraryInfo.Types.Language));
            return values.ToDictionary(v => v, v => v.ToString().ToLower());
        }
    }
}