namespace PD2Shared.GameFileUpdate.Internal
{
    internal enum ValidationKind
    {
        DownloadFiles,
        InstallFiles,
    }

    // Sneaking in an extension class for convenience
    internal static class ValidationKindEx
    {
        public static bool IsDownloadFiles(this ValidationKind validationKind)
        {
            return validationKind == ValidationKind.DownloadFiles;
        }

        public static bool IsInstallFiles(this ValidationKind validationKind)
        {
            return validationKind == ValidationKind.InstallFiles;
        }
    }
}
