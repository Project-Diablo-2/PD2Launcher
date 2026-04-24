using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using PD2Shared.Helpers;
using PD2Shared.Models;
using PD2Shared.Storage;

namespace SteamPD2
{
    class Program
    {
        private static readonly string installPath = AppContext.BaseDirectory;
        private static readonly string logPath = Path.Combine(installPath, "SteamPD2.log");

        static async Task Main(string[] args)
        {
            try
            {
                await Run(args);
            }
            catch (Exception ex)
            {
                Log($"FATAL ERROR: {ex}");
            }
        }

        static async Task Run(string[] args)
        {
            Log("============================================================");
            Log("-=-=-= SteamPD2 Bootstrap Starting =-=-=-");
            Log($"Base directory: {AppContext.BaseDirectory}");
            Log($"Current directory: {Directory.GetCurrentDirectory()}");

            WineDxvkHelpers.EnsureDxvkConfigForLauncher(installPath);
            Log($"dxvk.conf exists = {File.Exists(Path.Combine(installPath, "dxvk.conf"))}");

            var localStorage = new LocalStorage();
            var fileUpdateHelpers = new FileUpdateHelpers(new HttpClient());
            var filterHelpers = new FilterHelpers(new HttpClient(), localStorage);
            var launchGameHelpers = new LaunchGameHelpers();

            var launcherArgs = localStorage.LoadSection<LauncherArgs>(StorageKey.LauncherArgs);
            if (launcherArgs?.disableAutoUpdate == true)
            {
                Log("disableAutoUpdate is enabled. Skipping all update checks.");
                launchGameHelpers.LaunchGame(localStorage);
                return;
            }

            var fileUpdateModel = localStorage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel);
            if (fileUpdateModel != null &&
                fileUpdateModel.Client?.TrimEnd('/') ==
                "https://storage.googleapis.com/storage/v1/b/pd2-client-files/o")
            {
                fileUpdateModel.Client = "https://pd2-client-files.projectdiablo2.com/";
                localStorage.Update(StorageKey.FileUpdateModel, fileUpdateModel);
            }

            if (fileUpdateModel == null)
            {
                Log("FileUpdateModel missing. Exiting.");
                return;
            }

            Log($"Client path: {fileUpdateModel.Client}");
            Log($"Launcher path: {fileUpdateModel.Launcher}");
            Log($"Environment: {fileUpdateModel.FilePath}");

            List<CloudFileItem> cloudFiles;
            try
            {
                cloudFiles = await fileUpdateHelpers.GetCloudFileMetadataAsync(fileUpdateModel.Launcher);
            }
            catch (Exception ex)
            {
                Log("Unable to reach launcher update server. Proceeding in offline mode.");
                Log($"Error: {ex.Message}");

                try
                {
                    launchGameHelpers.LaunchGame(localStorage);
                }
                catch (Exception launchEx)
                {
                    Log($"Game launch failed: {launchEx.Message}");
                }

                return;
            }

            if (cloudFiles == null || cloudFiles.Count == 0)
            {
                Log("Launcher metadata came back empty. Skipping launcher updates.");
            }
            else
            {
                await HandleLauncherUpdates(fileUpdateHelpers, cloudFiles);
            }

            try
            {
                Log("Checking game files...");
                await fileUpdateHelpers.UpdateFilesCheck(
                    localStorage,
                    new Progress<double>(v =>
                    {
                        Console.Write($"\rGame update: {(int)(v * 100)}%   ");
                    }),
                    () => Log("Game files updated."));

                Log("Syncing files from environment to root...");
                await fileUpdateHelpers.SyncFilesFromEnvToRoot(localStorage);

                Log("Checking filters...");
                var selectedFilter = localStorage.LoadSection<SelectedAuthorAndFilter>(
                    StorageKey.SelectedAuthorAndFilter);
                if (selectedFilter?.selectedFilter != null)
                {
                    await filterHelpers.CheckAndUpdateFilterAsync(selectedFilter);
                }

                Log($"Game.exe exists before launch = {File.Exists(Path.Combine(installPath, "Game.exe"))}");
                Log("Launching game...");
                launchGameHelpers.LaunchGame(localStorage);
            }
            catch (Exception ex)
            {
                Log($"Exception occurred: {ex.Message}");
            }
        }

        private static async Task HandleLauncherUpdates(
            FileUpdateHelpers fileUpdateHelpers,
            List<CloudFileItem> cloudFiles)
        {
            var bigFour = new[]
            {
                "PD2Launcher.exe",
                "PD2Shared.dll",
                "SteamPD2.exe",
                "UpdateUtility.exe"
            };

            var optionalLauncherFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "RemovePD2WindowsSettings.ps1",
                "SetPD2WindowsSettings.ps1"
            };

            Log("Checking non-Big4 launcher files...");
            foreach (var cloudItem in cloudFiles)
            {
                var normalizedName = GetCloudFileName(cloudItem.Name);

                if (bigFour.Contains(normalizedName, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (fileUpdateHelpers.IsFileExcluded(normalizedName))
                {
                    continue;
                }

                var localPath = Path.Combine(installPath, normalizedName);
                bool needsUpdate = !File.Exists(localPath) ||
                                   !fileUpdateHelpers.CompareCRC(localPath, cloudItem.Crc32c);

                if (!needsUpdate)
                {
                    continue;
                }

                Log($"Updating launcher helper file: {normalizedName}");
                bool downloaded = await fileUpdateHelpers.PrepareLauncherUpdateAsync(
                    cloudItem.MediaLink,
                    localPath,
                    null);

                if (!downloaded)
                {
                    if (optionalLauncherFiles.Contains(normalizedName))
                    {
                        Log($"Optional launcher helper failed to download: {normalizedName}. Continuing.");
                        continue;
                    }

                    Log($"Failed to download required launcher helper: {normalizedName}. Exiting.");
                    return;
                }
            }

            bool needsBig4Update = bigFour.Any(name =>
            {
                var cloudItem = cloudFiles.FirstOrDefault(i =>
                    string.Equals(GetCloudFileName(i.Name), name, StringComparison.OrdinalIgnoreCase));

                var localPath = Path.Combine(installPath, name);
                return cloudItem != null &&
                       (!File.Exists(localPath) ||
                        !fileUpdateHelpers.CompareCRC(localPath, cloudItem.Crc32c));
            });

            if (!needsBig4Update)
            {
                Log("Launcher files are up to date.");
                return;
            }

            Log("Launcher update detected. Downloading...");
            foreach (var fileName in bigFour)
            {
                var cloudItem = cloudFiles.FirstOrDefault(i =>
                    string.Equals(GetCloudFileName(i.Name), fileName, StringComparison.OrdinalIgnoreCase));

                if (cloudItem == null)
                {
                    Log($"Cloud metadata missing for {fileName}. Skipping.");
                    continue;
                }

                var progress = new Progress<double>(v =>
                {
                    Console.Write($"\rDownloading {fileName}: {(int)(v * 100)}%   ");
                });

                var targetName = fileName == "UpdateUtility.exe"
                    ? fileName
                    : "Temp" + fileName;

                var path = Path.Combine(installPath, targetName);
                Log($"Downloading Big4 file: {fileName} -> {path}");

                bool downloaded = await fileUpdateHelpers.PrepareLauncherUpdateAsync(
                    cloudItem.MediaLink,
                    path,
                    progress);

                if (!downloaded)
                {
                    Log($"Failed to download {fileName}. Exiting.");
                    return;
                }
            }

            Log("Launching updater utility...");
            fileUpdateHelpers.StartUpdateProcessWithSteam(installPath);
            Environment.Exit(0);
        }

        private static string GetCloudFileName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return string.Empty;
            }

            var normalized = rawName.Replace('\\', '/');
            var lastSegment = normalized.Split('/').LastOrDefault() ?? normalized;
            return Uri.UnescapeDataString(lastSegment);
        }

        private static void Log(string msg)
        {
            try
            {
                File.AppendAllText(logPath, $"[{DateTime.Now}] {msg}{Environment.NewLine}");
            }
            catch
            {
                // Don't crash if logging fails
            }
        }
    }
}
