namespace Microsoft.LocalForwarder.Library
{
    using System;
    using System.Reflection;
    using System.Xml.Linq;

    internal class Configuration
    {
        private readonly XElement localForwarderConfiguration;

        public bool? ApplicationInsightsInput_Enabled
        {
            get
            {
                try
                {
                    string value = this.localForwarderConfiguration.Element("Inputs").Element("ApplicationInsightsInput").Attribute("Enabled").Value;

                    if (bool.TryParse(value, out bool result))
                    {
                        return result;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant($"Could not find or convert the data field {MethodBase.GetCurrentMethod().Name} in configuration. {this.localForwarderConfiguration.Value}"), e);
                }
            }
        }

        public string ApplicationInsightsInput_Host {
            get
            {
                try
                {
                    return this.localForwarderConfiguration.Element("Inputs").Element("ApplicationInsightsInput").Element("Host").Value;
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant($"Could not find or convert the data field {MethodBase.GetCurrentMethod().Name} in configuration. {this.localForwarderConfiguration.Value}"), e);
                }
            }
        }

        public int? ApplicationInsightsInput_Port
        {
            get
            {
                try
                {
                    string value = this.localForwarderConfiguration.Element("Inputs").Element("ApplicationInsightsInput").Element("Port").Value;

                    if (int.TryParse(value, out var result))
                    {
                        return result;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant($"Could not find or convert the data field {MethodBase.GetCurrentMethod().Name} in configuration. {this.localForwarderConfiguration.Value}"), e);
                }
            }
        }

        public bool? OpenCensusInput_Enabled
        {
            get
            {
                try
                {
                    string value = this.localForwarderConfiguration.Element("Inputs").Element("OpenCensusInput").Attribute("Enabled").Value;

                    if (bool.TryParse(value, out var result))
                    {
                        return result;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant($"Could not find or convert the data field {MethodBase.GetCurrentMethod().Name} in configuration. {this.localForwarderConfiguration.Value}"), e);
                }
            }
        }

        public string OpenCensusInput_Host
        {
            get
            {
                try
                {
                    return this.localForwarderConfiguration.Element("Inputs").Element("OpenCensusInput").Element("Host").Value;
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant($"Could not find or convert the data field {MethodBase.GetCurrentMethod().Name} in configuration. {this.localForwarderConfiguration.Value}"), e);
                }
            }
        }

        public int? OpenCensusInput_Port
        {
            get
            {
                try
                {
                    string value = this.localForwarderConfiguration.Element("Inputs").Element("OpenCensusInput").Element("Port").Value;

                    if (int.TryParse(value, out var result))
                    {
                        return result;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant($"Could not find or convert the data field {MethodBase.GetCurrentMethod().Name} in configuration. {this.localForwarderConfiguration.Value}"), e);
                }
            }
        }

        public string OpenCensusToApplicationInsights_InstrumentationKey
        {
            get
            {
                try
                {
                    return this.localForwarderConfiguration.Element("OpenCensusToApplicationInsights").Element("InstrumentationKey").Value;
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant($"Could not find or convert the data field {MethodBase.GetCurrentMethod().Name} in configuration. {this.localForwarderConfiguration.Value}"), e);
                }
            }
        }

        public string ApplicationInsights_LiveMetricsStreamInstrumentationKey
        {
            get
            {
                try
                {
                    return this.localForwarderConfiguration.Element("ApplicationInsights").Element("LiveMetricsStreamInstrumentationKey").Value;
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant($"Could not find or convert the data field {MethodBase.GetCurrentMethod().Name} in configuration. {this.localForwarderConfiguration.Value}"), e);
                }
            }
        }

        public string ApplicationInsights_LiveMetricsStreamAuthenticationApiKey
        {
            get
            {
                try
                {
                    return this.localForwarderConfiguration.Element("ApplicationInsights").Element("LiveMetricsStreamAuthenticationApiKey").Value;
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant($"Could not find or convert the data field {MethodBase.GetCurrentMethod().Name} in configuration. {this.localForwarderConfiguration.Value}"), e);
                }
            }
        }

        public bool? ApplicationInsights_AdaptiveSampling_Enabled
        {
            get
            {
                try
                {
                    string value = this.localForwarderConfiguration.Element("ApplicationInsights").Element("AdaptiveSampling").Attribute("Enabled").Value;

                    if (bool.TryParse(value, out var result))
                    {
                        return result;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant($"Could not find or convert the data field {MethodBase.GetCurrentMethod().Name} in configuration. {this.localForwarderConfiguration.Value}"), e);
                }
            }
        }

        public int? ApplicationInsights_AdaptiveSampling_MaxEventsPerSecond
        {
            get
            {
                try
                {
                    string value = this.localForwarderConfiguration.Element("ApplicationInsights").Element("AdaptiveSampling").Element("MaxEventsPerSecond").Value;

                    if (int.TryParse(value, out var result))
                    {
                        return result;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant($"Could not find or convert the data field {MethodBase.GetCurrentMethod().Name} in configuration. {this.localForwarderConfiguration.Value}"), e);
                }
            }
        }

        public int? ApplicationInsights_AdaptiveSampling_MaxOtherItemsPerSecond
        {
            get
            {
                try
                {
                    string value = this.localForwarderConfiguration.Element("ApplicationInsights").Element("AdaptiveSampling").Element("MaxOtherItemsPerSecond").Value;

                    if (int.TryParse(value, out var result))
                    {
                        return result;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception e)
                {
                    throw new ArgumentException(
                        FormattableString.Invariant($"Could not find or convert the data field {MethodBase.GetCurrentMethod().Name} in configuration. {this.localForwarderConfiguration.Value}"), e);
                }
            }
        }

        public Configuration(string configuration)
        {
            try
            {
                configuration = Environment.ExpandEnvironmentVariables(configuration);
            }
            catch(Exception e)
            {
                throw new ArgumentException(FormattableString.Invariant($"Error expanding environment variables contained within the configuration"), e);
            }

            try
            {
                this.localForwarderConfiguration = XElement.Parse(configuration);
            }
            catch (Exception e)
            {
                throw new ArgumentException(FormattableString.Invariant($"Error parsing configuration"), e);
            }
        }
    }
}
