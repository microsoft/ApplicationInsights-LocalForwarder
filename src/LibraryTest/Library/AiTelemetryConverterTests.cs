namespace Microsoft.LocalForwarder.LibraryTest.Library
{
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.LocalForwarder.Library;
    using Microsoft.LocalForwarder.Library.Inputs.Contracts;
    using System;
    using System.Reflection;
    using System.Linq;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AiTelemetryConverterTests
    {
        [TestMethod]
        public void AiTelemetryConverterTests_ConvertsEvent()
        {
            // ARRANGE
            var timestamp = DateTimeOffset.UtcNow;

            Telemetry telemetry = new Telemetry()
            {
                Ver = 5,
                DataTypeName = "Event",
                DateTime = timestamp.ToString("o"),
                SamplingRate = new Google.Protobuf.WellKnownTypes.DoubleValue() { Value = 25 },
                SequenceNumber = "50",
                InstrumentationKey = "ikey",
                Event = new Event() { Ver = 6, Name = "Event1" }
            };

            telemetry.Tags.Add(new ContextTagKeys().SessionId, "sessionId");

            telemetry.Event.Properties.Add("prop1", "propValue1");
            telemetry.Event.Measurements.Add("measurement1", 105);

            // ACT
            EventTelemetry result = AiTelemetryConverter.ConvertEventToSdkApi(telemetry);

            // ASSERT
            Assert.AreEqual("Event1", result.Name);
            Assert.AreEqual("prop1", result.Properties.Single().Key);
            Assert.AreEqual("propValue1", result.Properties.Single().Value);
            Assert.AreEqual("measurement1", result.Metrics.Single().Key);
            Assert.AreEqual(105.0, result.Metrics.Single().Value);

            // common fields
            Assert.AreEqual("50", result.Sequence);
            Assert.AreEqual(timestamp, result.Timestamp);
            Assert.AreEqual("ikey", result.Context.InstrumentationKey);
            Assert.AreEqual("sessionId", result.Context.Session.Id);

            // sampling fields
            Assert.AreEqual(25, (result as ISupportSampling).SamplingPercentage);
        }

        [TestMethod]
        public void AiTelemetryConverterTests_ConvertsTrace()
        {
            // ARRANGE
            var timestamp = DateTimeOffset.UtcNow;

            Telemetry telemetry = new Telemetry()
            {
                Ver = 5,
                DataTypeName = "Trace",
                DateTime = timestamp.ToString("o"),
                SamplingRate = new Google.Protobuf.WellKnownTypes.DoubleValue() { Value = 25 },
                SequenceNumber = "50",
                InstrumentationKey = "ikey",
                Message = new Message() { Ver = 6, Message_ = "Message1", SeverityLevel = LocalForwarder.Library.Inputs.Contracts.SeverityLevel.Warning }
            };

            telemetry.Tags.Add(new ContextTagKeys().SessionId, "sessionId");

            telemetry.Message.Properties.Add("prop1", "propValue1");
            
            // ACT
            TraceTelemetry result = AiTelemetryConverter.ConvertTraceToSdkApi(telemetry);

            // ASSERT
            Assert.AreEqual("Message1", result.Message);
            Assert.AreEqual(ApplicationInsights.DataContracts.SeverityLevel.Warning, result.SeverityLevel);
            Assert.AreEqual("prop1", result.Properties.Single().Key);
            Assert.AreEqual("propValue1", result.Properties.Single().Value);
            
            // common fields
            Assert.AreEqual("50", result.Sequence);
            Assert.AreEqual(timestamp, result.Timestamp);
            Assert.AreEqual("ikey", result.Context.InstrumentationKey);
            Assert.AreEqual("sessionId", result.Context.Session.Id);

            // sampling fields
            Assert.AreEqual(25, (result as ISupportSampling).SamplingPercentage);
        }

        [TestMethod]
        public void AiTelemetryConverterTests_ConvertsMeasurementMetric()
        {
            // ARRANGE
            var timestamp = DateTimeOffset.UtcNow;

            Telemetry telemetry = new Telemetry()
            {
                Ver = 5,
                DataTypeName = "Metric",
                DateTime = timestamp.ToString("o"),
                SamplingRate = new Google.Protobuf.WellKnownTypes.DoubleValue() { Value = 25 },
                SequenceNumber = "50",
                InstrumentationKey = "ikey",
                Metric = new Metric() { Ver = 6 }
            };

            telemetry.Tags.Add(new ContextTagKeys().SessionId, "sessionId");

            telemetry.Metric.Metrics.Add(new DataPoint() { Ns = "ns1", Name = "Metric1", Kind = DataPointType.Measurement, Value = 11, Count = new Google.Protobuf.WellKnownTypes.Int32Value() { Value = 1 } });
            
            telemetry.Metric.Properties.Add("prop1", "propValue1");

            // ACT
            MetricTelemetry result = AiTelemetryConverter.ConvertMetricToSdkApi(telemetry);

            // ASSERT
            Assert.AreEqual("Metric1", result.Name);
            Assert.AreEqual("prop1", result.Properties.Single().Key);
            Assert.AreEqual("propValue1", result.Properties.Single().Value);

            Assert.AreEqual("ns1", result.MetricNamespace);
            Assert.AreEqual("Metric1", result.Name);
            Assert.AreEqual(11.0, result.Sum);
            Assert.AreEqual(1, result.Count);
            Assert.IsNull(result.Min);
            Assert.IsNull(result.Max);
            Assert.IsNull(result.StandardDeviation);

            // common fields
            Assert.AreEqual("50", result.Sequence);
            Assert.AreEqual(timestamp, result.Timestamp);
            Assert.AreEqual("ikey", result.Context.InstrumentationKey);
            Assert.AreEqual("sessionId", result.Context.Session.Id);
        }

        [TestMethod]
        public void AiTelemetryConverterTests_ConvertsAggregateMetric()
        {
            // ARRANGE
            var timestamp = DateTimeOffset.UtcNow;

            Telemetry telemetry = new Telemetry()
            {
                Ver = 5,
                DataTypeName = "Metric",
                DateTime = timestamp.ToString("o"),
                SamplingRate = new Google.Protobuf.WellKnownTypes.DoubleValue() { Value = 25 },
                SequenceNumber = "50",
                InstrumentationKey = "ikey",
                Metric = new Metric() { Ver = 6 }
            };

            telemetry.Tags.Add(new ContextTagKeys().SessionId, "sessionId");

            telemetry.Metric.Metrics.Add(new DataPoint()
            {
                Ns = "ns1",
                Name = "Metric1",
                Kind = DataPointType.Aggregation,
                Value = 11,
                Count = new Google.Protobuf.WellKnownTypes.Int32Value() { Value = 2 },
                Min = new Google.Protobuf.WellKnownTypes.DoubleValue() { Value = 10 },
                Max = new Google.Protobuf.WellKnownTypes.DoubleValue() { Value = 12 },
                StdDev = new Google.Protobuf.WellKnownTypes.DoubleValue() { Value = 0.2 }
            });

            telemetry.Metric.Properties.Add("prop1", "propValue1");

            // ACT
            MetricTelemetry result = AiTelemetryConverter.ConvertMetricToSdkApi(telemetry);

            // ASSERT
            Assert.AreEqual("Metric1", result.Name);
            Assert.AreEqual("prop1", result.Properties.Single().Key);
            Assert.AreEqual("propValue1", result.Properties.Single().Value);

            Assert.AreEqual("ns1", result.MetricNamespace);
            Assert.AreEqual("Metric1", result.Name);
            Assert.AreEqual(11.0, result.Sum);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(10.0, result.Min);
            Assert.AreEqual(12.0, result.Max);
            Assert.AreEqual(0.2, result.StandardDeviation);

            // common fields
            Assert.AreEqual("50", result.Sequence);
            Assert.AreEqual(timestamp, result.Timestamp);
            Assert.AreEqual("ikey", result.Context.InstrumentationKey);
            Assert.AreEqual("sessionId", result.Context.Session.Id);
        }

        [TestMethod]
        public void AiTelemetryConverterTests_ConvertsException()
        {
            // ARRANGE
            var timestamp = DateTimeOffset.UtcNow;

            Telemetry telemetry = new Telemetry()
            {
                Ver = 5,
                DataTypeName = "Exception",
                DateTime = timestamp.ToString("o"),
                SamplingRate = new Google.Protobuf.WellKnownTypes.DoubleValue() { Value = 25 },
                SequenceNumber = "50",
                InstrumentationKey = "ikey",
                Exception = new LocalForwarder.Library.Inputs.Contracts.Exception()
                {
                    Ver = 6,
                    SeverityLevel = LocalForwarder.Library.Inputs.Contracts.SeverityLevel.Warning,
                    ProblemId = "Problem1",
                }
            };

            telemetry.Tags.Add(new ContextTagKeys().SessionId, "sessionId");

            telemetry.Exception.Exceptions.Add(new ExceptionDetails() { Id = 12, OuterId = 13, TypeName = "TerribleException1", Message = "Oh wow look what happened 1", HasFullStack = new Google.Protobuf.WellKnownTypes.BoolValue() { Value = true }, Stack = "Terrible stack 1" });
            telemetry.Exception.Exceptions[0].ParsedStack.Add(new LocalForwarder.Library.Inputs.Contracts.StackFrame() { Level = 4, Method = "Method1", Assembly = "Assm1", FileName = "File1", Line = 145 });
            telemetry.Exception.Exceptions[0].ParsedStack.Add(new LocalForwarder.Library.Inputs.Contracts.StackFrame() { Level = 5, Method = "Method2", Assembly = "Assm2", FileName = "File2", Line = 146 });

            telemetry.Exception.Exceptions.Add(new ExceptionDetails() { Id = 120, OuterId = 130, TypeName = "TerribleException2", Message = "Oh wow look what happened 2", HasFullStack = new Google.Protobuf.WellKnownTypes.BoolValue() { Value = false }, Stack = "Terrible stack 2" });
            telemetry.Exception.Exceptions[1].ParsedStack.Add(new LocalForwarder.Library.Inputs.Contracts.StackFrame() { Level = 40, Method = "Method10", Assembly = "Assm10", FileName = "File10", Line = 1450 });
            telemetry.Exception.Exceptions[1].ParsedStack.Add(new LocalForwarder.Library.Inputs.Contracts.StackFrame() { Level = 50, Method = "Method20", Assembly = "Assm20", FileName = "File20", Line = 1460 });

            telemetry.Exception.Properties.Add("prop1", "propValue1");
            telemetry.Exception.Measurements.Add("measurement1", 111.1);

            // ACT
            ExceptionTelemetry result = AiTelemetryConverter.ConvertExceptionToSdkApi(telemetry);

            // ASSERT
            Assert.AreEqual(ApplicationInsights.DataContracts.SeverityLevel.Warning, result.SeverityLevel);
            Assert.AreEqual("Problem1", result.ProblemId);

            Assert.AreEqual(2, result.ExceptionDetailsInfoList.Count);
            Assert.AreEqual("TerribleException1", result.ExceptionDetailsInfoList[0].TypeName);
            Assert.AreEqual("Oh wow look what happened 1", result.ExceptionDetailsInfoList[0].Message);

            object internalExceptionDetails = typeof(ExceptionDetailsInfo).GetProperty("ExceptionDetails", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(result.ExceptionDetailsInfoList[0]);
            Type type = internalExceptionDetails.GetType();
            Assert.AreEqual(12, type.GetProperty("id").GetValue(internalExceptionDetails));
            Assert.AreEqual(13, type.GetProperty("outerId").GetValue(internalExceptionDetails));
            Assert.AreEqual("TerribleException1", type.GetProperty("typeName").GetValue(internalExceptionDetails));
            Assert.AreEqual("Oh wow look what happened 1", type.GetProperty("message").GetValue(internalExceptionDetails));
            Assert.AreEqual(true, type.GetProperty("hasFullStack").GetValue(internalExceptionDetails));
            Assert.AreEqual("Terrible stack 1", type.GetProperty("stack").GetValue(internalExceptionDetails));

            object parsedStack = type.GetProperty("parsedStack").GetValue(internalExceptionDetails);
            Type parsedStackType = parsedStack.GetType();
            Assert.AreEqual(2, parsedStackType.GetProperty("Count").GetValue(parsedStack));

            object stackFrame = parsedStackType.InvokeMember("Item", BindingFlags.GetProperty, null, parsedStack, new[] { (object)0 });
            Type stackFrameType = stackFrame.GetType();
            Assert.AreEqual(4, stackFrameType.GetProperty("level").GetValue(stackFrame));
            Assert.AreEqual("Method1", stackFrameType.GetProperty("method").GetValue(stackFrame));
            Assert.AreEqual("Assm1", stackFrameType.GetProperty("assembly").GetValue(stackFrame));
            Assert.AreEqual("File1", stackFrameType.GetProperty("fileName").GetValue(stackFrame));
            Assert.AreEqual(145, stackFrameType.GetProperty("line").GetValue(stackFrame));
            stackFrame = parsedStackType.InvokeMember("Item", BindingFlags.GetProperty, null, parsedStack, new[] { (object)1 });
            Assert.AreEqual(5, stackFrameType.GetProperty("level").GetValue(stackFrame));
            Assert.AreEqual("Method2", stackFrameType.GetProperty("method").GetValue(stackFrame));
            Assert.AreEqual("Assm2", stackFrameType.GetProperty("assembly").GetValue(stackFrame));
            Assert.AreEqual("File2", stackFrameType.GetProperty("fileName").GetValue(stackFrame));
            Assert.AreEqual(146, stackFrameType.GetProperty("line").GetValue(stackFrame));

            Assert.AreEqual("TerribleException2", result.ExceptionDetailsInfoList[1].TypeName);
            Assert.AreEqual("Oh wow look what happened 2", result.ExceptionDetailsInfoList[1].Message);

            internalExceptionDetails = typeof(ExceptionDetailsInfo).GetProperty("ExceptionDetails", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(result.ExceptionDetailsInfoList[1]);
            type = internalExceptionDetails.GetType();
            Assert.AreEqual(120, type.GetProperty("id").GetValue(internalExceptionDetails));
            Assert.AreEqual(130, type.GetProperty("outerId").GetValue(internalExceptionDetails));
            Assert.AreEqual("TerribleException2", type.GetProperty("typeName").GetValue(internalExceptionDetails));
            Assert.AreEqual("Oh wow look what happened 2", type.GetProperty("message").GetValue(internalExceptionDetails));
            Assert.AreEqual(false, type.GetProperty("hasFullStack").GetValue(internalExceptionDetails));
            Assert.AreEqual("Terrible stack 2", type.GetProperty("stack").GetValue(internalExceptionDetails));

            parsedStack = type.GetProperty("parsedStack").GetValue(internalExceptionDetails);
            parsedStackType = parsedStack.GetType();
            Assert.AreEqual(2, parsedStackType.GetProperty("Count").GetValue(parsedStack));

            stackFrame = parsedStackType.InvokeMember("Item", BindingFlags.GetProperty, null, parsedStack, new[] { (object)0 });
            stackFrameType = stackFrame.GetType();
            Assert.AreEqual(40, stackFrameType.GetProperty("level").GetValue(stackFrame));
            Assert.AreEqual("Method10", stackFrameType.GetProperty("method").GetValue(stackFrame));
            Assert.AreEqual("Assm10", stackFrameType.GetProperty("assembly").GetValue(stackFrame));
            Assert.AreEqual("File10", stackFrameType.GetProperty("fileName").GetValue(stackFrame));
            Assert.AreEqual(1450, stackFrameType.GetProperty("line").GetValue(stackFrame));
            stackFrame = parsedStackType.InvokeMember("Item", BindingFlags.GetProperty, null, parsedStack, new[] { (object)1 });
            Assert.AreEqual(50, stackFrameType.GetProperty("level").GetValue(stackFrame));
            Assert.AreEqual("Method20", stackFrameType.GetProperty("method").GetValue(stackFrame));
            Assert.AreEqual("Assm20", stackFrameType.GetProperty("assembly").GetValue(stackFrame));
            Assert.AreEqual("File20", stackFrameType.GetProperty("fileName").GetValue(stackFrame));
            Assert.AreEqual(1460, stackFrameType.GetProperty("line").GetValue(stackFrame));

            Assert.AreEqual("prop1", result.Properties.Single().Key);
            Assert.AreEqual("propValue1", result.Properties.Single().Value);
            Assert.AreEqual("measurement1", result.Metrics.Single().Key);
            Assert.AreEqual(111.1, result.Metrics.Single().Value);

            // common fields
            Assert.AreEqual("50", result.Sequence);
            Assert.AreEqual(timestamp, result.Timestamp);
            Assert.AreEqual("ikey", result.Context.InstrumentationKey);
            Assert.AreEqual("sessionId", result.Context.Session.Id);

            // sampling fields
            Assert.AreEqual(25, (result as ISupportSampling).SamplingPercentage);
        }

        [TestMethod]
        public void AiTelemetryConverterTests_ConvertsDependency()
        {
            // ARRANGE
            var timestamp = DateTimeOffset.UtcNow;

            Telemetry telemetry = new Telemetry()
            {
                Ver = 5,
                DataTypeName = "Dependency",
                DateTime = timestamp.ToString("o"),
                SamplingRate = new Google.Protobuf.WellKnownTypes.DoubleValue() { Value = 25 },
                SequenceNumber = "50",
                InstrumentationKey = "ikey",
                Dependency = new Dependency() { Ver = 6, Name = "Dependency1", Id = "Dep1", ResultCode = "ResultCode1", Duration = new Google.Protobuf.WellKnownTypes.Duration() { Seconds = 123 }, Success = new Google.Protobuf.WellKnownTypes.BoolValue() { Value = true }, Data = "Data", Type = "Type", Target = "Target" }
            };

            telemetry.Tags.Add(new ContextTagKeys().SessionId, "sessionId");

            telemetry.Dependency.Properties.Add("prop1", "propValue1");
            telemetry.Dependency.Measurements.Add("measurement1", 105);

            // ACT
            DependencyTelemetry result = AiTelemetryConverter.ConvertDependencyToSdkApi(telemetry);

            // ASSERT
            Assert.AreEqual("Dependency1", result.Name);
            Assert.AreEqual("Dep1", result.Id);
            Assert.AreEqual("ResultCode1", result.ResultCode);
            Assert.AreEqual(TimeSpan.FromSeconds(123), result.Duration);
            Assert.AreEqual(true, result.Success);
            Assert.AreEqual("Data", result.Data);
            Assert.AreEqual("Type", result.Type);
            Assert.AreEqual("Target", result.Target);

            Assert.AreEqual("prop1", result.Properties.Single().Key);
            Assert.AreEqual("propValue1", result.Properties.Single().Value);
            Assert.AreEqual("measurement1", result.Metrics.Single().Key);
            Assert.AreEqual(105.0, result.Metrics.Single().Value);

            // common fields
            Assert.AreEqual("50", result.Sequence);
            Assert.AreEqual(timestamp, result.Timestamp);
            Assert.AreEqual("ikey", result.Context.InstrumentationKey);
            Assert.AreEqual("sessionId", result.Context.Session.Id);

            // sampling fields
            Assert.AreEqual(25, (result as ISupportSampling).SamplingPercentage);
        }

        [TestMethod]
        public void AiTelemetryConverterTests_ConvertsAvailability()
        {
            // ARRANGE
            var timestamp = DateTimeOffset.UtcNow;

            Telemetry telemetry = new Telemetry()
            {
                Ver = 5,
                DataTypeName = "Event",
                DateTime = timestamp.ToString("o"),
                SamplingRate = new Google.Protobuf.WellKnownTypes.DoubleValue() { Value = 25 },
                SequenceNumber = "50",
                InstrumentationKey = "ikey",
                Availability = new Availability() { Ver = 6, Id = "Avail1", Name = "Availability1", Duration = new Google.Protobuf.WellKnownTypes.Duration() { Seconds = 123 }, Success = true, RunLocation = "RunLocation", Message = "Message" }
            };

            telemetry.Tags.Add(new ContextTagKeys().SessionId, "sessionId");

            telemetry.Availability.Properties.Add("prop1", "propValue1");
            telemetry.Availability.Measurements.Add("measurement1", 105);

            // ACT
            AvailabilityTelemetry result = AiTelemetryConverter.ConvertAvailabilityToSdkApi(telemetry);

            // ASSERT
            Assert.AreEqual("Avail1", result.Id);
            Assert.AreEqual("Availability1", result.Name);
            Assert.AreEqual(TimeSpan.FromSeconds(123), result.Duration);
            Assert.AreEqual(true, result.Success);
            Assert.AreEqual("RunLocation", result.RunLocation);
            Assert.AreEqual("Message", result.Message);
            
            Assert.AreEqual("prop1", result.Properties.Single().Key);
            Assert.AreEqual("propValue1", result.Properties.Single().Value);
            Assert.AreEqual("measurement1", result.Metrics.Single().Key);
            Assert.AreEqual(105.0, result.Metrics.Single().Value);

            // common fields
            Assert.AreEqual("50", result.Sequence);
            Assert.AreEqual(timestamp, result.Timestamp);
            Assert.AreEqual("ikey", result.Context.InstrumentationKey);
            Assert.AreEqual("sessionId", result.Context.Session.Id);
        }

        [TestMethod]
        public void AiTelemetryConverterTests_ConvertsPageView()
        {
            // ARRANGE
            var timestamp = DateTimeOffset.UtcNow;

            Telemetry telemetry = new Telemetry()
            {
                Ver = 5,
                DataTypeName = "PageView",
                DateTime = timestamp.ToString("o"),
                SamplingRate = new Google.Protobuf.WellKnownTypes.DoubleValue() { Value = 25 },
                SequenceNumber = "50",
                InstrumentationKey = "ikey",
                PageView = new PageView() { Url = "http://microsoft.com/", Duration = new Google.Protobuf.WellKnownTypes.Duration() { Seconds = 123 }, Id = "PageView1", ReferrerUri = "http://none.com", Event = new Event() { Ver = 6, Name = "Event1" } }
            };

            telemetry.Tags.Add(new ContextTagKeys().SessionId, "sessionId");

            telemetry.PageView.Event.Properties.Add("prop1", "propValue1");
            telemetry.PageView.Event.Measurements.Add("measurement1", 105);

            // ACT
            PageViewTelemetry result = AiTelemetryConverter.ConvertPageViewToSdkApi(telemetry);

            // ASSERT
            Assert.AreEqual("PageView1", result.Id);
            Assert.AreEqual("http://microsoft.com/", result.Url.OriginalString);
            Assert.AreEqual(TimeSpan.FromSeconds(123), result.Duration);
            Assert.AreEqual("Event1", result.Name);

            Assert.AreEqual("prop1", result.Properties.Single().Key);
            Assert.AreEqual("propValue1", result.Properties.Single().Value);
            Assert.AreEqual("measurement1", result.Metrics.Single().Key);
            Assert.AreEqual(105.0, result.Metrics.Single().Value);

            // common fields
            Assert.AreEqual("50", result.Sequence);
            Assert.AreEqual(timestamp, result.Timestamp);
            Assert.AreEqual("ikey", result.Context.InstrumentationKey);
            Assert.AreEqual("sessionId", result.Context.Session.Id);

            // sampling fields
            Assert.AreEqual(25, (result as ISupportSampling).SamplingPercentage);
        }

        [TestMethod]
        public void AiTelemetryConverterTests_ConvertsRequest()
        {
            // ARRANGE
            var timestamp = DateTimeOffset.UtcNow;

            Telemetry telemetry = new Telemetry()
            {
                Ver = 5,
                DataTypeName = "Request",
                DateTime = timestamp.ToString("o"),
                SamplingRate = new Google.Protobuf.WellKnownTypes.DoubleValue() { Value = 25 },
                SequenceNumber = "50",
                InstrumentationKey = "ikey",
                Request = new Request() { Ver = 6, Id = "Req1", Name = "Request1", Duration = new Google.Protobuf.WellKnownTypes.Duration() { Seconds = 123 }, Success = new Google.Protobuf.WellKnownTypes.BoolValue() { Value = true }, Source = "Source", Url = "http://microsoft.com/" }
            };

            telemetry.Tags.Add(new ContextTagKeys().SessionId, "sessionId");

            telemetry.Request.Properties.Add("prop1", "propValue1");
            telemetry.Request.Measurements.Add("measurement1", 105);

            // ACT
            RequestTelemetry result = AiTelemetryConverter.ConvertRequestToSdkApi(telemetry);

            // ASSERT
            Assert.AreEqual("Req1", result.Id);
            Assert.AreEqual("Request1", result.Name);
            Assert.AreEqual(TimeSpan.FromSeconds(123), result.Duration);
            Assert.AreEqual(true, result.Success);
            Assert.AreEqual("Source", result.Source);
            Assert.AreEqual("http://microsoft.com/", result.Url.OriginalString);

            Assert.AreEqual("prop1", result.Properties.Single().Key);
            Assert.AreEqual("propValue1", result.Properties.Single().Value);
            Assert.AreEqual("measurement1", result.Metrics.Single().Key);
            Assert.AreEqual(105.0, result.Metrics.Single().Value);

            // common fields
            Assert.AreEqual("50", result.Sequence);
            Assert.AreEqual(timestamp, result.Timestamp);
            Assert.AreEqual("ikey", result.Context.InstrumentationKey);
            Assert.AreEqual("sessionId", result.Context.Session.Id);

            // sampling fields
            Assert.AreEqual(25, (result as ISupportSampling).SamplingPercentage);
        }

        [TestMethod]
        public void AiTelemetryConverterTests_ThrowsOnEmptyMetric()
        {
            // ARRANGE
            var timestamp = DateTimeOffset.UtcNow;

            Telemetry telemetry = new Telemetry()
            {
                Ver = 5,
                DataTypeName = "Metric",
                DateTime = timestamp.ToString("o"),
                SamplingRate = new Google.Protobuf.WellKnownTypes.DoubleValue() { Value = 25 },
                SequenceNumber = "50",
                InstrumentationKey = "ikey",
                Metric = new Metric() { Ver = 6 }
            };

            // empty telemetry.Metric.Metrics
            //telemetry.Metric.Metrics.Add(new DataPoint() { Ns = "ns1", Name = "Metric1", Kind = DataPointType.Measurement, Value = 11, Count = new Google.Protobuf.WellKnownTypes.Int32Value() { Value = 1 } });


            // ACT
            try
            {
                MetricTelemetry result = AiTelemetryConverter.ConvertMetricToSdkApi(telemetry);
            }
            catch(ArgumentException e)
            {
                // ASSERT
                Assert.AreEqual("Metrics list can't be empty", e.Message);
                return;
            }

            Assert.Fail("Expected an exception");
        }

        [TestMethod]
        public void AiTelemetryConverterTests_FillsOutMissingStatsForAggregateMetric()
        {
            // ARRANGE
            var timestamp = DateTimeOffset.UtcNow;

            Telemetry telemetry = new Telemetry()
            {
                Ver = 5,
                DataTypeName = "Metric",
                DateTime = timestamp.ToString("o"),
                SamplingRate = new Google.Protobuf.WellKnownTypes.DoubleValue() { Value = 25 },
                SequenceNumber = "50",
                InstrumentationKey = "ikey",
                Metric = new Metric() { Ver = 6 }
            };

            telemetry.Tags.Add(new ContextTagKeys().SessionId, "sessionId");

            telemetry.Metric.Metrics.Add(new DataPoint()
            {
                Ns = "ns1",
                Name = "Metric1",
                Kind = DataPointType.Aggregation,
                Value = 11,
                Count = null,
                Min = null,
                Max = null,
                StdDev = null
            });

            telemetry.Metric.Properties.Add("prop1", "propValue1");

            // ACT
            MetricTelemetry result = AiTelemetryConverter.ConvertMetricToSdkApi(telemetry);

            // ASSERT
            Assert.AreEqual("Metric1", result.Name);
            Assert.AreEqual("prop1", result.Properties.Single().Key);
            Assert.AreEqual("propValue1", result.Properties.Single().Value);

            Assert.AreEqual("ns1", result.MetricNamespace);
            Assert.AreEqual("Metric1", result.Name);
            Assert.AreEqual(11.0, result.Sum);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(11.0, result.Min);
            Assert.AreEqual(11.0, result.Max);
            Assert.AreEqual(0.0, result.StandardDeviation);

            // common fields
            Assert.AreEqual("50", result.Sequence);
            Assert.AreEqual(timestamp, result.Timestamp);
            Assert.AreEqual("ikey", result.Context.InstrumentationKey);
            Assert.AreEqual("sessionId", result.Context.Session.Id);
        }
    }
}