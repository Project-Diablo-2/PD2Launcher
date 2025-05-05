
using PD2Shared.Interfaces;
using PD2Shared.Models;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace PD2Shared.Helpers
{
    public class LaunchGameHelpers : ILaunchGameHelpers
    {
        public void LaunchGame(ILocalStorage localStorage)
        {
            var fileUpdateModel = localStorage.LoadSection<FileUpdateModel>(PD2Shared.Models.StorageKey.FileUpdateModel);

            string diabloIIExePath = Path.Combine(Directory.GetCurrentDirectory(), "Game.exe");
            if (!File.Exists(diabloIIExePath))
            {
                Debug.WriteLine("Game.exe not found.");
                return;
            }

            LauncherArgs launcherArgs = localStorage.LoadSection<LauncherArgs>(PD2Shared.Models.StorageKey.LauncherArgs);

            string args = ConstructLaunchArguments(launcherArgs);

            // Launch the game with the specified arguments.
            var startInfo = new ProcessStartInfo
            {
                FileName = diabloIIExePath,
                Arguments = args,
                WorkingDirectory = Path.GetDirectoryName(diabloIIExePath)
            };

            Process.Start(startInfo);
        }

        private string ConstructLaunchArguments(LauncherArgs launcherArgs)
        {
            List<string> argsList = new List<string>();

            // Graphics mode
            if (launcherArgs.graphics)
            {
                argsList.Add("-ddraw");
            }
            else
            {
                // Default to -3dfx if not specified or any other value
                argsList.Add("-3dfx");
            }

            // Skip to Battle.net
            if (launcherArgs.skiptobnet)
            {
                argsList.Add("-skiptobnet");
            }

            // Sound in background
            if (launcherArgs.sndbkg)
            {
                argsList.Add("-sndbkg");
            }

            string args = string.Join(" ", argsList);
            Debug.WriteLine("Passing Args: " + args);
            return args;
        }
    }
}