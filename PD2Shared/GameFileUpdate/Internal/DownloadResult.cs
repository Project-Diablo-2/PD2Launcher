using Org.BouncyCastle.Crypto.Digests;

namespace PD2Shared.GameFileUpdate.Internal
{
    internal class DownloadResult
    {
        public DownloadResult(Exception? exception = null)
        {
            this.Exception = exception;
        }

        public DownloadResult(Exception? exception, long partialSize, byte[] partialMd5Bytes, byte[] referenceMd5Bytes, MD5Digest md5Digest)
        {
            if (partialSize <= 0)
            {
                throw new ArgumentException($"Valid '{nameof(partialSize)}' for {nameof(PartialDownload)} must be >0", nameof(partialSize));
            }

            this.Exception = exception;

            this.PartialDownload = new PartialDownload(partialSize, partialMd5Bytes, referenceMd5Bytes, md5Digest);

        }

        public bool IsSuccess { get => Exception == null; }

        public Exception? Exception { get; }
        public PartialDownload? PartialDownload { get; }
    }
}
