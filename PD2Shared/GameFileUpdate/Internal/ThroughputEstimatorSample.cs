namespace PD2Shared.GameFileUpdate.Internal
{
    internal class ThroughputEstimatorSample
    {
        public long Bytes { get; }
        public long TimePointMilliseconds { get; }

        public ThroughputEstimatorSample(long bytes, long timePointMilliseconds)
        {
            Bytes = bytes;
            TimePointMilliseconds = timePointMilliseconds;
        }
    }
}
