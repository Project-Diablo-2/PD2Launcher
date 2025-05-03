using System;
using System.Net.Http;
using System.Threading.Tasks;
using PD2Shared.Helpers;
using PD2Shared.Storage;
using PD2Shared.Interfaces;

namespace SteamPD2
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("SteamPD2 bootstrapper starting...");

            ILocalStorage localStorage = new LocalStorage();
            var fileUpdateHelpers = new FileUpdateHelpers(new HttpClient());
            var launchGameHelpers = new LaunchGameHelpers();

            try
            {
                await fileUpdateHelpers.UpdateFilesCheck(localStorage, new Progress<double>(p =>
                {
                    Console.WriteLine($"Progress: {p:P0}");
                }),
                () =>
                {
                    Console.WriteLine("Files updated.");
                });

                Console.WriteLine("Launching game...");
                launchGameHelpers.LaunchGame(localStorage);

                Console.WriteLine("Launcher finished execution.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
            }
        }
    }
}
