
using PD2Shared.Helpers;

namespace PD2Shared.Interfaces
{
    public interface IFileUpdateHelpers
    {
        Task UpdateFilesCheck(ILocalStorage storage, IProgress<double> progress, Action onDownloadComplete);
        Task SyncFilesFromEnvToRoot(ILocalStorage storage);
        Task<List<CloudFileItem>> GetCloudFileMetadataAsync(string cloudFileBucket);
        bool CompareCRC(string filePath, string crcHash);
        Task<bool> DownloadFileAsync(string fileUrl, string destinationPath, IProgress<double> progress);
    }
}
