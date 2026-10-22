namespace PD2Shared.GameFileUpdate
{
    public partial class ProgressValues
    {
        private class Data : IData
        {
            public double? Total { get; set; } = null;
            public bool TotalSet { get; set; } = false;

            public FileCountProgress? FileCount { get; set; } = null;
            public bool FileCountSet { get; set; } = false;

            public BytesProgress? Bytes { get; set; } = null;
            public bool BytesSet { get; set; } = false;

            public BytesPerSecProgress? BytesPerSec { get; set; } = null;
            public bool BytesPerSecSet { get; set; } = false;
        }
    }
}
