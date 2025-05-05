using System;
using System.Net.Http;
using System.Threading.Tasks;
using PD2Shared.Helpers;
using PD2Shared.Storage;
using PD2Shared.Interfaces;
using PD2Shared.Models;

namespace SteamPD2
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("SteamPD2 Bootstrapper Starting.......");

            // Init basic dependencies
            var storage = new LocalStorage(); // Your DI may need adjustment
            storage.InitializeIfNotExists(StorageKey.FileUpdateModel, new FileUpdateModel());

            var updateHelpers = new FileUpdateHelpers(new HttpClient());

            var fileUpdateModel = storage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel);
            if (fileUpdateModel == null)
            {
                Console.WriteLine("File update model not found.");
                return;
            }

            Console.WriteLine("Checking core launcher files (Big 4)...");
            var cloudItems = await updateHelpers.GetCloudFileMetadataAsync(fileUpdateModel.Launcher);
            var bigFour = new[] { "PD2Launcher.exe", "PD2Shared.dll", "SteamPD2.exe", "UpdateUtility.exe" };
            bool needsUpdate = false;

            foreach (var file in bigFour)
            {
                var item = cloudItems.Find(i => i.Name == file);
                if (item == null) continue;

                var localPath = Path.Combine(Directory.GetCurrentDirectory(), file);
                if (!File.Exists(localPath) || !updateHelpers.CompareCRC(localPath, item.Crc32c))
                {
                    Console.WriteLine($"Update required: {file}");
                    needsUpdate = true;
                    break;
                }
            }

            if (needsUpdate)
            {
                Console.WriteLine("Downloading updated files...");

                foreach (var file in bigFour)
                {
                    var item = cloudItems.Find(i => i.Name == file);
                    if (item == null) continue;

                    var tempName = file == "UpdateUtility.exe" ? file : "Temp" + file;
                    var tempPath = Path.Combine(Directory.GetCurrentDirectory(), tempName);
                    bool downloaded = await updateHelpers.PrepareLauncherUpdateAsync(item.MediaLink, tempPath, new Progress<double>(p =>
                    {
                        Console.WriteLine($"Downloading {file}: {(int)(p * 100)}%");
                    }));

                    if (!downloaded)
                    {
                        Console.WriteLine($"Failed to download {file}, aborting update.");
                        return;
                    }
                }

                Console.WriteLine("Update ready. Launching UpdateUtility.exe...");
                updateHelpers.StartUpdateProcess();
                await Task.Delay(250);
                return;
            }

            Console.WriteLine("Files up to date. Continuing to game launch...");

            var filterHelpers = new FilterHelpers(new HttpClient(), storage);
            var selectedFilter = storage.LoadSection<SelectedAuthorAndFilter>(StorageKey.SelectedAuthorAndFilter);

            if (selectedFilter?.selectedFilter != null)
            {
                Console.WriteLine("Checking for filter updates...");
                await filterHelpers.CheckAndUpdateFilterAsync(selectedFilter);
            }

            Console.WriteLine("Syncing game files...");
            await updateHelpers.SyncFilesFromEnvToRoot(storage);

            Console.WriteLine("Launching game...");
            var launcher = new LaunchGameHelpers();
            launcher.LaunchGame(storage);
        }
    }
}