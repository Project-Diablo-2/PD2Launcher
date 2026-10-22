namespace PD2Shared.GameFileUpdate.Internal
{
    internal class DownloadResult
    {
        public DownloadResult(Exception exception)
        {
            this.Exception = exception;
        }

        public DownloadResult(Xxh3Hash xxh3Hash)
        {
            this.Xxh3Hash = xxh3Hash;
        }

        public DownloadResult(
            Exception? exception,
            long partialSize,
            Xxh3Hash partialXxh3Hash,
            NonFinalizingXxh3 xxh3Digest,
            Md5Hash referenceMd5Hash,
            NonFinalizingDigest digest,
            Hash expectedHash)
        {
            if (partialSize <= 0)
            {
                throw new ArgumentException($"Valid '{nameof(partialSize)}' for {nameof(PartialDownload)} must be >0", nameof(partialSize));
            }

            this.Exception = exception;

            this.PartialDownload = new PartialDownload(partialSize, partialXxh3Hash, xxh3Digest, referenceMd5Hash, digest, expectedHash);

        }

        public bool IsSuccess { get => Exception == null; }

        public Exception? Exception { get; } = null;
        public Xxh3Hash? Xxh3Hash { get; } = null;
        public PartialDownload? PartialDownload { get; } = null;
    }
}
