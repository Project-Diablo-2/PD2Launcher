namespace PD2Shared.GameFileUpdate
{
    // Base class for all exceptions
    public abstract class GameFileUpdateException : Exception
    {
        public GameFileUpdateException(Exception? innerException = null, string? message = null) : base(message, innerException) { }
    }

    public class LoadManifestException : GameFileUpdateException
    {
        public LoadManifestException(string message, Exception? innerException = null) : base(innerException, message) { }
    }

    public class SaveManifestException : GameFileUpdateException
    {
        public SaveManifestException(string message, Exception? innerException = null) : base(innerException, message) { }
    }

    public class LoadMetadataException : GameFileUpdateException
    {
        public LoadMetadataException(string message, Exception? innerException = null) : base(innerException, message) { }
    }

    // Base class for fatal exceptions
    public abstract class FatalGameFileUpdateException : GameFileUpdateException
    {
        public FatalGameFileUpdateException(Exception? innerException = null, string? message = null) : base(innerException, message) { }
    }

    // Offline (fresh metadata not retrieved, either due to an error or being forced to work offline) and the available manifest (if any) contains no data to work with
    public class OfflineInvalidManifest : FatalGameFileUpdateException
    {
        public OfflineInvalidManifest(Exception? innerException = null, string? message = null) : base(innerException, message) { }
    }

    // Retrieved metadata appears to be invalid (rare)
    public class InvalidMetadataRetrieved : FatalGameFileUpdateException
    {
        public InvalidMetadataRetrieved(Exception? innerException = null, string? message = null) : base(innerException, message) { }
    }

    // Offline (fresh metadata not retrieved, either due to an error or being forced to work offline),
    // validation failed based on the available manifest and files need to be re-downloaded, which is impossible.
    public class OfflineNeedsDownload : FatalGameFileUpdateException
    {
        public OfflineNeedsDownload(Exception? innerException = null, string? message = null) : base(innerException, message) { }
    }

    // Base download failure exception
    public class DownloadException : GameFileUpdateException
    {
        public DownloadException(Exception? innerException = null, string? message = null) : base(innerException, message) { }
    }

    // Download failed due to MD5 mismatch (rare)
    public class DownloadMd5MismatchException : DownloadException
    {
        public DownloadMd5MismatchException(Exception? innerException = null, string? message = null) : base(innerException, message) { }
    }
}
