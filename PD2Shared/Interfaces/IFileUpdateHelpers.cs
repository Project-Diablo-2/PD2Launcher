
namespace PD2Shared.Interfaces
{
    public interface IFileUpdateHelpers
    {
        Task UpdateFilesCheck(ILocalStorage storage, IProgress<double> progress, Action onDownloadComplete);
        Task SyncFilesFromEnvToRoot(ILocalStorage storage);
    }
}
