using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UpdateUtility
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Log("-=-=-= Update Utility Started =-=-=-");
            Log($"Received {args.Length} arguments.");

            string pd2Launcher;
            string tempLauncher;
            string steamLauncher;
            string tempSteam;
            string exeToStart;

            if (args.Length == 4 || args.Length == 5)
            {
                // New format:
                // 0: PD2Launcher.exe
                // 1: TempPD2Launcher.exe
                // 2: SteamPD2.exe
                // 3: TempSteamPD2.exe
                // 4: Optional executable to restart

                Log("Using new 4/5 argument format.");

                pd2Launcher = args[0];
                tempLauncher = args[1];
                steamLauncher = args[2];
                tempSteam = args[3];

                exeToStart =
                    args.Length == 5
                        ? args[4]
                        : pd2Launcher;
            }
            else if (args.Length == 6 || args.Length == 7)
            {
                // Legacy format:
                // 0: PD2Launcher.exe
                // 1: TempPD2Launcher.exe
                // 2: PD2Shared.dll
                // 3: TempPD2Shared.dll
                // 4: SteamPD2.exe
                // 5: TempSteamPD2.exe
                // 6: Optional executable to restart

                Log(
                    "Using legacy 6/7 argument format. " +
                    "PD2Shared arguments will be ignored.");

                pd2Launcher = args[0];
                tempLauncher = args[1];

                // args[2] and args[3] intentionally ignored.

                steamLauncher = args[4];
                tempSteam = args[5];

                exeToStart =
                    args.Length == 7
                        ? args[6]
                        : pd2Launcher;
            }
            else
            {
                Log(
                    $"Unexpected argument count: {args.Length}. " +
                    "Expected 4, 5, 6, or 7 arguments.");

                for (int i = 0; i < args.Length; i++)
                {
                    Log($"Argument {i}: {args[i]}");
                }

                return;
            }

            Log($"PD2Launcher: {pd2Launcher}");
            Log($"TempPD2Launcher: {tempLauncher}");
            Log($"SteamPD2: {steamLauncher}");
            Log($"TempSteamPD2: {tempSteam}");
            Log($"Executable to start: {exeToStart}");

            await KillAndWait("PD2Launcher");
            await KillAndWait("SteamPD2");

            // Give Windows time to release executable file handles.
            await Task.Delay(2500);

            bool launcherUpdated = TryReplace(
                tempLauncher,
                pd2Launcher,
                "PD2Launcher");

            bool steamUpdated = TryReplace(
                tempSteam,
                steamLauncher,
                "SteamPD2");

            if (!launcherUpdated)
            {
                Log(
                    "PD2Launcher could not be updated. " +
                    "The launcher will not be restarted.");

                return;
            }

            if (!steamUpdated)
            {
                Log(
                    "SteamPD2 could not be updated. " +
                    "Continuing because PD2Launcher updated successfully.");
            }

            Log($"Starting: {Path.GetFileName(exeToStart)}");
            StartExecutable(exeToStart);
        }

        static async Task KillAndWait(string processName)
        {
            Process[] processes =
                Process.GetProcessesByName(processName);

            foreach (Process process in processes)
            {
                try
                {
                    Log(
                        $"Killing {process.ProcessName} " +
                        $"({process.Id})");

                    process.Kill();
                }
                catch (Exception ex)
                {
                    Log(
                        $"Failed to kill {process.ProcessName}: " +
                        $"{ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }

            while (Process.GetProcessesByName(processName).Length > 0)
            {
                Log($"Waiting for {processName} to exit...");
                await Task.Delay(1000);
            }
        }

        static bool TryReplace(
            string tempPath,
            string finalPath,
            string label)
        {
            if (!File.Exists(tempPath))
            {
                Log($"{label} temp file missing: {tempPath}");
                return false;
            }

            const int maxRetries = 10;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (File.Exists(finalPath))
                    {
                        File.Delete(finalPath);
                    }

                    MoveFileWithCmd(tempPath, finalPath);

                    Log($"{label} updated successfully.");
                    return true;
                }
                catch (Exception ex)
                {
                    Log(
                        $"[{label}] Retry {attempt}/{maxRetries} failed: " +
                        $"{ex.Message}");

                    Thread.Sleep(1000);
                }
            }

            Log(
                $"Failed to update {label} after " +
                $"{maxRetries} attempts.");

            return false;
        }

        static void MoveFileWithCmd(
            string source,
            string destination)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments =
                    $"/c move /Y \"{source}\" \"{destination}\"",

                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo);

            if (process == null)
            {
                throw new InvalidOperationException(
                    "Could not start cmd.exe to move the update file.");
            }

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new IOException(
                    $"Move command exited with code " +
                    $"{process.ExitCode} for " +
                    $"{Path.GetFileName(destination)}.");
            }

            if (!File.Exists(destination))
            {
                throw new IOException(
                    $"Move failed for " +
                    $"{Path.GetFileName(destination)}.");
            }
        }

        static void StartExecutable(string path)
        {
            if (!File.Exists(path))
            {
                Log($"Executable not found: {path}");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    WorkingDirectory =
                        Path.GetDirectoryName(path) ??
                        AppContext.BaseDirectory,

                    UseShellExecute = true
                });

                Log($"Started: {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                Log(
                    $"Error starting executable: " +
                    $"{ex.Message}");
            }
        }

        static void Log(string message)
        {
            try
            {
                string logPath =
                    Path.Combine(
                        AppContext.BaseDirectory,
                        "update.log");

                File.AppendAllText(
                    logPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: " +
                    $"{message}{Environment.NewLine}");
            }
            catch
            {
                // blank
            }
        }
    }
}