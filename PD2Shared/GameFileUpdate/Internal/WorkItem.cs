namespace PD2Shared.GameFileUpdate.Internal
{
    internal class WorkItem
    {
        public WorkItem(ManifestEntry manifestEntry, string url, string downloadPath, string installPath, PartialDownload? partialDownload)
        {
            ManifestEntry = manifestEntry;
            Url = url;
            DownloadPath = downloadPath;
            InstallPath = installPath;
            PartialDownload = partialDownload;
        }

        public ManifestEntry ManifestEntry { get; }
        public string Url { get; }
        public string DownloadPath { get; }
        public string InstallPath { get; }

        public bool InstallFileValidated { get; set; } = false;
        public bool DownloadFileValidated { get; set; } = false;
        public PartialDownload? PartialDownload { get; set; } = null;
        public DownloadResult? DownloadResult { get; set; } = null;
    }
}
