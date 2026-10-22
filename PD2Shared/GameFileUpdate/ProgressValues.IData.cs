namespace PD2Shared.GameFileUpdate
{
    public partial class ProgressValues
    {
        public interface IData
        {
            // Main progress indicator (0..1)
            public double? Total { get; }
            public bool TotalSet { get; }

            // <current>/<total> counter indicator
            public FileCountProgress? FileCount { get; }
            public bool FileCountSet { get; }

            // "<current>(/<total>) MiB" indicator
            public BytesProgress? Bytes { get; }
            public bool BytesSet { get; }

            // "<bytesPerSec> MiB/s" indicator
            public BytesPerSecProgress? BytesPerSec { get; }
            public bool BytesPerSecSet { get; }
        }
    }
}
