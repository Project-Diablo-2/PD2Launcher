namespace PD2Shared.GameFileUpdate
{
    public enum UpdateMode
    {
        Normal,   // (I1) FilesToRestore  = [All files] -> [InstallFiles that failed Validation]
                  // (D1) FilesToDownload = [FilesToRestore] -> [DownloadFiles that failed Validation] + [DownloadFiles with PartialDownloads]

        Restore,  // (I2) FilesToRestore  = [All files] -> [InstallFiles that failed Validation] + [InstallFiles loosely validated]
                  // (D1) FilesToDownload = [FilesToRestore] -> [DownloadFiles that failed Validation] + [DownloadFiles with PartialDownloads]

        Download, // (I3) FilesToRestore  = [None]
                  // (D2) FilesToDownload = [All files] -> [DownloadFiles that failed Validation] + [DownloadFiles with PartialDownloads]

        Reset     // (I4) FilesToRestore  = [All files]
                  // (D3) FilesToDownload = [All files] (...but Validate PartialDownloads beforehand)
                  // ...additionally force-query all files for good measure
    }

    // Sneaking in an extension class for convenience
    public static class UpdateModeEx
    {
        public static bool IsNormal(this UpdateMode updateMode)
        {
            return updateMode == UpdateMode.Normal;
        }

        public static bool IsRestore(this UpdateMode updateMode)
        {
            return updateMode == UpdateMode.Restore;
        }

        public static bool IsDownload(this UpdateMode updateMode)
        {
            return updateMode == UpdateMode.Download;
        }

        public static bool IsReset(this UpdateMode updateMode)
        {
            return updateMode == UpdateMode.Reset;
        }
    }
}
