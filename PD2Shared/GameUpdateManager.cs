using PD2Shared.Interfaces;
using PD2Shared.Models;
using System.Diagnostics;

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
            Console.WriteLine("[PD2Shared] Update and Launch started...............");

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

                var launcherArgs = storage.LoadSection<LauncherArgs>(StorageKey.LauncherArgs);
                if (launcherArgs != null && launcherArgs.disableAutoUpdate)
                {
                    gameLauncher.LaunchGame(storage);
                    return;
                }

                var installPath = Directory.GetCurrentDirectory();
                var big4 = new (string name, string tempName)[]
                {
                ("PD2Launcher.exe", "TempPD2Launcher.exe"),
                ("SteamPD2.exe", "TempSteamPD2.exe"),
                ("PD2Shared.dll", "TempPD2Shared.dll"),
                ("UpdateUtility.exe", "UpdateUtility.exe")
                };

                var updateModel = storage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel);
                var cloudItems = await fileUpdater.GetCloudFileMetadataAsync(updateModel.Launcher);

                bool big4NeedsUpdate = false;
                foreach (var (name, tempName) in big4)
                {
                    var cloudItem = cloudItems.FirstOrDefault(c => c.Name == name);
                    if (cloudItem != null)
                    {
                        var localPath = Path.Combine(installPath, name);
                        var tempPath = Path.Combine(installPath, tempName);

                        if (!File.Exists(localPath) || !fileUpdater.CompareCRC(localPath, cloudItem.Crc32c))
                        {
                            Console.WriteLine($"[PD2Shared] {name} requires update.");
                            big4NeedsUpdate = true;

                            if (name == "UpdateUtility.exe")
                            {
                                // Direct overwrite — no temp
                                await fileUpdater.DownloadFileAsync(cloudItem.MediaLink, localPath, progress ?? new Progress<double>());
                            }
                            else
                            {
                                await fileUpdater.DownloadFileAsync(cloudItem.MediaLink, tempPath, progress ?? new Progress<double>());
                            }
                        }
                    }
                }

                if (big4NeedsUpdate)
                {
                    // Determine which EXE to restart
                    string finalExeToStart = File.Exists(Path.Combine(installPath, "SteamPD2.exe"))
                        ? "SteamPD2.exe"
                        : "PD2Launcher.exe";

                    string updateUtilPath = Path.Combine(installPath, "UpdateUtility.exe");
                    string args = $"\"{installPath}\\PD2Launcher.exe\" \"{installPath}\\TempPD2Launcher.exe\" " +
                                  $"\"{installPath}\\PD2Shared.dll\" \"{installPath}\\TempPD2Shared.dll\" " +
                                  $"\"{installPath}\\SteamPD2.exe\" \"{installPath}\\TempSteamPD2.exe\" " +
                                  $"\"{finalExeToStart}\"";

                    Console.WriteLine("[PD2Shared] Big 4 updated. Launching UpdateUtility and exiting.");

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = updateUtilPath,
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    });

                    Environment.Exit(0); // or return;
                    return;
                }

                // If no Big 4 update needed, do normal game file updates
                await fileUpdater.UpdateFilesCheck(storage, progress ?? new Progress<double>(), onDownloadComplete ?? (() => { }));
                await fileUpdater.SyncFilesFromEnvToRoot(storage);
                Console.WriteLine("[PD2Shared] Game files updated.");

                gameLauncher.LaunchGame(storage);
                Console.WriteLine("[PD2Shared] Game launched");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PD2Shared] Fatal error during update or launch: {ex.Message}");
            }
        }
    }
}