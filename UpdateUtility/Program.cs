using System.Diagnostics;

namespace UpdateUtility
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Log("-=-=-= Update Utility Started =-=-=-");

            if (args.Length < 6 || args.Length > 7)
            {
                Log("Expected 6 or 7 args: PD2Launcher TempPD2Launcher PD2Shared TempPD2Shared SteamPD2 TempSteamPD2 [ExeToStart]");
                return;
            }

            var pd2Launcher = args[0];
            var tempLauncher = args[1];
            var sharedDll = args[2];
            var tempShared = args[3];
            var steamLauncher = args[4];
            var tempSteam = args[5];
            var exeToStart = args.Length == 7 ? args[6] : pd2Launcher;

            await KillAndWait("PD2Launcher");
            await KillAndWait("SteamPD2");

            await Task.Delay(2500);

            TryReplace(tempLauncher, pd2Launcher, "PD2Launcher");
            TryReplace(tempShared, sharedDll, "PD2Shared.dll");
            TryReplace(tempSteam, steamLauncher, "SteamPD2");

            Log($"Starting: {Path.GetFileName(exeToStart)}");
            StartExecutable(exeToStart);
        }

        static async Task KillAndWait(string processName)
        {
            var procs = Process.GetProcessesByName(processName);
            foreach (var p in procs)
            {
                try
                {
                    Log($"Killing {p.ProcessName} ({p.Id})");
                    p.Kill();
                }
                catch (Exception ex)
                {
                    Log($"Failed to kill {p.ProcessName}: {ex.Message}");
                }
            }

            while (Process.GetProcessesByName(processName).Length > 0)
            {
                Log($"Waiting for {processName} to exit..");
                await Task.Delay(1000);
            }
        }

        static void TryReplace(string tempPath, string finalPath, string label)
        {
            if (!File.Exists(tempPath))
            {
                Log($"{label} temp file missing. Skipping.");
                return;
            }

            const int maxRetries = 10;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (File.Exists(finalPath))
                        File.Delete(finalPath);

                    MoveFileWithCmd(tempPath, finalPath);
                    Log($"{label} updated successfully.");
                    return;
                }
                catch (Exception ex)
                {
                    Log($"[{label}] Retry {attempt}/{maxRetries} failed: {ex.Message}");
                    Thread.Sleep(1000);
                }
            }

            Log($"Failed to update {label} after {maxRetries} attempts.");
        }

        static void MoveFileWithCmd(string src, string dst)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c move /Y \"{src}\" \"{dst}\"",
                UseShellExecute = false,
                CreateNoWindow = false
            };

            var proc = Process.Start(psi);
            proc.WaitForExit();

            if (!File.Exists(dst))
                throw new IOException($"Move failed via cmd for {Path.GetFileName(dst)}.");
        }

        static void StartExecutable(string path)
        {
            if (!File.Exists(path))
            {
                Log($"Executable not found: {Path.GetFileName(path)}");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                Log($"Started: {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                Log($"Error starting executable: {ex.Message}");
            }
        }

        static void Log(string msg)
        {
            //Console.WriteLine(msg);
            File.AppendAllText("update.log", $"{DateTime.Now}: {msg}\n");
        }
    }
}