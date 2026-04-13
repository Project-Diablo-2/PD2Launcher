using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using PD2Shared.Helpers;
using PD2Shared.Models;
using PD2Shared.Storage;

namespace SteamPD2
{
    class Program
    {
        private static readonly string logPath =
            Path.Combine(AppContext.BaseDirectory, "SteamPD2.log");

        private const bool DisableLauncherUpdatesForTestBuild = false;

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
            Log($"Base directory: {AppContext.BaseDirectory}");
            Log($"Current directory: {Directory.GetCurrentDirectory()}");

            WineDxvkHelpers.EnsureDxvkConfigForLauncher(AppContext.BaseDirectory);

            var localStorage = new LocalStorage();
            var fileUpdateHelpers = new FileUpdateHelpers(new HttpClient());
            var filterHelpers = new FilterHelpers(new HttpClient(), localStorage);
            var launchGameHelpers = new LaunchGameHelpers();

            var launcherArgs = localStorage.LoadSection<LauncherArgs>(
                StorageKey.LauncherArgs);

            if (launcherArgs?.disableAutoUpdate == true)
            {
                Log("disableAutoUpdate is enabled. Skipping all update checks.");
                launchGameHelpers.LaunchGame(localStorage);
                return;
            }

            var fileUpdateModel = localStorage.LoadSection<FileUpdateModel>(
                StorageKey.FileUpdateModel);

            if (fileUpdateModel != null &&
                fileUpdateModel.Client.TrimEnd('/') ==
                "https://storage.googleapis.com/storage/v1/b/pd2-client-files/o")
            {
                fileUpdateModel.Client =
                    "https://pd2-client-files.projectdiablo2.com/";
                localStorage.Update(StorageKey.FileUpdateModel, fileUpdateModel);
            }

            Log($"Client path: {fileUpdateModel?.Client}");
            Log($"Launcher path: {fileUpdateModel?.Launcher}");

            if (fileUpdateModel == null)
            {
                Log("FileUpdateModel missing. Exiting.");
                return;
            }

            if (DisableLauncherUpdatesForTestBuild)
            {
                Log("TEST BUILD: launcher updates are disabled in SteamPD2.");
            }
            else
            {
                Log("Launcher update logic is enabled.");
                Log("This test build is not expected to use this path.");
            }

            Log("Checking game files...");

            try
            {
                await fileUpdateHelpers.UpdateFilesCheck(
                    localStorage,
                    new Progress<double>(v =>
                    {
                        Console.Write($"\rGame update: {(int)(v * 100)}%   ");
                    }),
                    () => Log("Game files updated."));

                await fileUpdateHelpers.SyncFilesFromEnvToRoot(localStorage);

                Log("Checking filters...");
                var selectedFilter = localStorage.LoadSection<SelectedAuthorAndFilter>(
                    StorageKey.SelectedAuthorAndFilter);

                if (selectedFilter?.selectedFilter != null)
                {
                    await filterHelpers.CheckAndUpdateFilterAsync(selectedFilter);
                }

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
            try
            {
                File.AppendAllText(
                    logPath,
                    $"[{DateTime.Now}] {msg}{Environment.NewLine}");
            }
            catch
            {
                // Don't crash if logging fails
            }
        }
    }
}