using PD2Shared.Interfaces;
using PD2Shared.Models;
using PD2Shared.Logging;
using static PD2Shared.Logging.LoggingStatic;
using PD2Shared.Utils;
using System.Diagnostics;

namespace PD2Shared.Helpers
{
    public class LaunchGameHelpers : ILaunchGameHelpers
    {
        public Process LaunchGame(ILocalStorage localStorage, EventHandler? exitedEventHandler = null)
        {
            LauncherArgs launcherArgs = localStorage.LoadSection<LauncherArgs>(StorageKey.LauncherArgs);

            Process process = new()
            {
                EnableRaisingEvents = exitedEventHandler != null,
                StartInfo = new()
                {
                    WorkingDirectory = Env.GetCwd(),
                    FileName = Path.Combine(Env.GetCwd(), "Game.exe"),
                    Arguments = ConstructLaunchArguments(launcherArgs),
                }
            };

            if (exitedEventHandler != null)
            {
                process.Exited += exitedEventHandler;
            }

            L.CallerInformation($"Launching: '\"{process.StartInfo.FileName}\" {process.StartInfo.Arguments}'...");

            process.Start();
            return process;
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