using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using PD2Shared.Helpers;
using PD2Shared.Interfaces;
using PD2Shared.Models;
using PD2Shared.Storage;

namespace SteamPD2
{
    class Program
    {
        static readonly string logPath = Path.Combine(AppContext.BaseDirectory, "SteamPD2.log");

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
            Log("-=-=-= SteamPD2 Bootstrap Starting =-=-=-");

            var localStorage = new LocalStorage();
            var fileUpdateHelpers = new FileUpdateHelpers(new HttpClient());
            var filterHelpers = new FilterHelpers(new HttpClient(), localStorage);
            var launchGameHelpers = new LaunchGameHelpers();

            var fileUpdateModel = localStorage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel);
            Log($"Cloud path: {fileUpdateModel?.Launcher}");
            if (fileUpdateModel == null)
            {
                Log("FileUpdateModel missing. Exiting.");
                return;
            }

            List<CloudFileItem> cloudFiles = new();
            try
            {
                cloudFiles = await fileUpdateHelpers.GetCloudFileMetadataAsync(fileUpdateModel.Launcher);
            }
            catch (Exception ex)
            {
                Log("Unable to reach update server. Proceeding in offline mode.");
                Log($"Error: {ex.Message}");

                // Skip all update logic and go straight to launch
                try
                {
                    Log("Launching game (offline mode)...");
                    launchGameHelpers.LaunchGame(localStorage);
                }
                catch (Exception launchEx)
                {
                    Log($"Game launch failed: {launchEx.Message}");
                }

                return;
            }
            try
            {
                cloudFiles = await fileUpdateHelpers.GetCloudFileMetadataAsync(fileUpdateModel.Launcher);
            }
            catch (Exception ex)
            {
                Log("Unable to reach update server. Proceeding in offline mode.");
                Log($"Error: {ex.Message}");

                // Skip all update logic and go straight to launch
                try
                {
                    Log("Launching game (offline mode)...");
                    launchGameHelpers.LaunchGame(localStorage);
                }
                catch (Exception launchEx)
                {
                    Log($"Game launch failed: {launchEx.Message}");
                }

                return;
            }
            var installPath = Directory.GetCurrentDirectory();
            var bigFour = new[] { "PD2Launcher.exe", "PD2Shared.dll", "SteamPD2.exe", "UpdateUtility.exe" };
            Log("Checking non-Big4 launcher files...");
            foreach (var cloudItem in cloudFiles)
            {
                if (bigFour.Contains(cloudItem.Name)) continue;
                if (fileUpdateHelpers.IsFileExcluded(cloudItem.Name)) continue;

                var localPath = Path.Combine(installPath, cloudItem.Name);
                if (!File.Exists(localPath) || !fileUpdateHelpers.CompareCRC(localPath, cloudItem.Crc32c))
                {
                    Log($"Updating {cloudItem.Name}...");
                    bool downloaded = await fileUpdateHelpers.PrepareLauncherUpdateAsync(cloudItem.MediaLink, localPath, null);
                    if (!downloaded)
                    {
                        Log($"Failed to download {cloudItem.Name}. Exiting.");
                        return;
                    }
                }
            }
            bool needsBig4Update = bigFour.Any(name =>
            {
                var cloudItem = cloudFiles.FirstOrDefault(i => i.Name == name);
                var localPath = Path.Combine(installPath, name);
                return cloudItem != null && (!File.Exists(localPath) || !fileUpdateHelpers.CompareCRC(localPath, cloudItem.Crc32c));
            });

            if (needsBig4Update)
            {
                Log("Launcher update detected. Downloading...");
                foreach (var fileName in bigFour)
                {
                    Log($"Queueing update: {fileName}");
                    var cloudItem = cloudFiles.FirstOrDefault(i => i.Name == fileName);
                    if (cloudItem == null) continue;

                    var progress = new Progress<double>(v =>
                    {
                        Console.Write($"\rDownloading {fileName}: {(int)(v * 100)}%   ");
                    });
                    var targetName = fileName == "UpdateUtility.exe" ? fileName : "Temp" + fileName;
                    var path = Path.Combine(installPath, targetName);
                    bool downloaded = await fileUpdateHelpers.PrepareLauncherUpdateAsync(cloudItem.MediaLink, path, progress);
                    if (!downloaded)
                    {
                        Log($"Failed to download {fileName}. Exiting.");
                        return;
                    }
                }

                Log("Launching updater utility...");
                fileUpdateHelpers.StartUpdateProcessWithSteam(installPath);
                return;
            }

            Log("Launcher files up to date. Checking game files...");

            try
            {
                await fileUpdateHelpers.UpdateFilesCheck(localStorage, new Progress<double>(v =>
                {
                    Console.Write($"\rGame update: {(int)(v * 100)}%   ");
                }), () => Log("Game files updated."));

                await fileUpdateHelpers.SyncFilesFromEnvToRoot(localStorage);

                Log("Checking filters...");
                var selectedFilter = localStorage.LoadSection<SelectedAuthorAndFilter>(StorageKey.SelectedAuthorAndFilter);
                if (selectedFilter?.selectedFilter != null)
                    await filterHelpers.CheckAndUpdateFilterAsync(selectedFilter);

                Log("Launching game...");
                launchGameHelpers.LaunchGame(localStorage);
            }
            catch (Exception ex)
            {
                Log($"Exception occurred: {ex.Message}");
            }
        }

        static void Log(string msg)
        {
            //Console.WriteLine(msg);
            try
            {
                File.AppendAllText(logPath, $"[{DateTime.Now}] {msg}\n");
            }
            catch { /* Don't crash if logging failss */ }
        }
    }
}