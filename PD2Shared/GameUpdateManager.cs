using PD2Shared.Interfaces;
using PD2Shared.Models;

namespace PD2Shared
{
    public static class GameUpdateManager
    {
        public static async Task RunUpdateAndLaunchAsync(
            ILocalStorage storage,
            IFileUpdateHelpers fileUpdater,
            ILaunchGameHelpers gameLauncher,
            IFilterHelpers filterHelpers,
            IProgress<double>? progress = null,
            Action? onDownloadComplete = null)
        {
            Console.WriteLine("[PD2Shared] Update and Launch started...");

            try
            {
 
                var selectedFilter = storage.LoadSection<SelectedAuthorAndFilter>(StorageKey.SelectedAuthorAndFilter);
                if (selectedFilter != null)
                {
                    try
                    {
                        await filterHelpers.CheckAndUpdateFilterAsync(selectedFilter);
                        Console.WriteLine("[PD2Shared] Filter check complete.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PD2Shared] Filter update failed: {ex.Message}");
                    }
                }

                // Check update config
                var launcherArgs = storage.LoadSection<LauncherArgs>(StorageKey.LauncherArgs);
                if (launcherArgs == null || !launcherArgs.disableAutoUpdate)
                {
                    try
                    {
                        await fileUpdater.UpdateFilesCheck(storage, progress ?? new Progress<double>(), onDownloadComplete ?? (() => { }));
                        await fileUpdater.SyncFilesFromEnvToRoot(storage);
                        Console.WriteLine("[PD2Shared] Files updated and synced.");
                    }
                    catch (HttpRequestException ex)
                    {
                        Console.WriteLine($"[PD2Shared] Network error: {ex.Message}. Running in offline mode.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PD2Shared] Update failed: {ex.Message}");
                    }
                }

                // Launch the game
                gameLauncher.LaunchGame(storage);
                Console.WriteLine("[PD2Shared] Game launched.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PD2Shared] Fatal error during update or launch: {ex.Message}");
            }
        }
    }
}