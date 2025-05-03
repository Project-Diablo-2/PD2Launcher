using PD2Shared;
using PD2Shared.Helpers;
using PD2Shared.Storage;

namespace SteamPD2
{
    public static class SteamBootstrap
    {
        public static async Task RunAsync()
        {
            Console.WriteLine("[SteamPD2] Starting Steam Bootstrap process...");

            var httpClient = new HttpClient();

            var storage = new LocalStorage();
            var fileUpdater = new FileUpdateHelpers(httpClient);
            var gameLauncher = new LaunchGameHelpers();
            var filterHelper = new FilterHelpers(httpClient, storage);

            await GameUpdateManager.RunUpdateAndLaunchAsync(
                storage,
                fileUpdater,
                gameLauncher,
                filterHelper
            );

            Console.WriteLine("[SteamPD2] Bootstrap process complete.");
        }
    }
}