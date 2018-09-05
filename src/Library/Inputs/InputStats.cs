namespace Microsoft.LocalForwarder.Library.Inputs
{
    /// <summary>
    /// Statistics regarding the current state of an Input.
    /// </summary>
    class InputStats
    {
        public int ConnectionCount = 0;

        public long BatchesReceived = 0;

        public long BatchesFailed = 0;

        public long ConfigsReceived = 0;

        public long ConfigsFailed = 0;

        public override string ToString()
        {
            return $"ConnectionCount: {this.ConnectionCount}, BatchesReceived: {this.BatchesReceived}, BatchesFailed: {this.BatchesFailed}, ConfigsReceived: {this.ConfigsReceived}, ConfigsFailed: {this.ConfigsFailed}";
        }
    }
}
