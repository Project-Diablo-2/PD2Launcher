namespace PD2Shared.GameFileUpdate.Internal
{
    internal class PartialDownload
    {
        public PartialDownload(
            long partialSize,
            Xxh3Hash partialXxh3Hash,
            NonFinalizingXxh3 xxh3Digest,
            Md5Hash referenceMd5Hash,
            NonFinalizingDigest digest,
            Hash expectedHash)
        {
            this.PartialSize = partialSize;
            this.PartialXxh3Hash = partialXxh3Hash;
            this.Xxh3Digest = xxh3Digest;
            this.ReferenceMd5Hash = referenceMd5Hash;
            this.Digest = digest;
            this.ExpectedHash = expectedHash;
        }

        public long PartialSize { get; }
        public Xxh3Hash PartialXxh3Hash { get; }
        public NonFinalizingXxh3 Xxh3Digest { get; }
        public Md5Hash ReferenceMd5Hash { get; }
        public NonFinalizingDigest Digest { get; }
        public Hash ExpectedHash { get; }
    }
}
