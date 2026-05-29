namespace PD2Shared.GameFileUpdate.Internal
{
    internal class Context
    {
        public ManifestEntry[] manifestEntries = Array.Empty<ManifestEntry>();
        public Dictionary<string, PartialDownload> partialDownloads = new();
    }
}
