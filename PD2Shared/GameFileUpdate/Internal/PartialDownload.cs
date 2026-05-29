using Org.BouncyCastle.Crypto.Digests;

namespace PD2Shared.GameFileUpdate.Internal
{
    internal class PartialDownload
    {
        public PartialDownload(long partialSize, byte[] partialMd5Bytes, byte[] referenceMd5Bytes, MD5Digest md5Digest)
        {
            this.PartialSize = partialSize;
            this.PartialMd5Bytes = partialMd5Bytes;
            this.ReferenceMd5Bytes = referenceMd5Bytes;
            this.Md5Digest = md5Digest;
        }

        public long PartialSize { get; }
        public byte[] PartialMd5Bytes { get; }
        public byte[] ReferenceMd5Bytes { get; }
        public MD5Digest Md5Digest { get; }
    }
}
