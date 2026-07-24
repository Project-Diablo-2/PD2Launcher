using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace UpdateUtility
{
    internal static class Program
    {
        private const string ManifestUrl =
            "https://pd2-launcher.projectdiablo2.com/launcher_manifest.json";

        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromMinutes(30)
        };

        private static readonly uint[] Crc32CTable = CreateCrc32CTable();

        private static async Task<int> Main(string[] args)
        {
            Log("-=-=-= Update Utility Started =-=-=-");
            Log($"Received {args.Length} arguments.");

            for (int index = 0; index < args.Length; index++)
            {
                Log($"Argument {index}: {args[index]}");
            }

            UpdatePlan plan = ResolvePlan(args);
            LogPlan(plan);

            bool launcherWasRunning =
                IsMatchingProcessRunning("PD2Launcher", plan.Pd2Launcher);

            bool steamWasRunning =
                IsMatchingProcessRunning("SteamPD2", plan.SteamPd2);

            Dictionary<string, ManifestItem>? manifestItems =
                await TryGetManifestItemsAsync();

            bool launcherTempReady = await EnsureTempFileAsync(
                "PD2Launcher.exe",
                plan.TempPd2Launcher,
                manifestItems);

            bool steamTempReady = await EnsureTempFileAsync(
                "SteamPD2.exe",
                plan.TempSteamPd2,
                manifestItems);

            if (!launcherTempReady && !steamTempReady)
            {
                Log(
                    "No usable launcher temp files were found or downloaded. " +
                    "Nothing will be replaced.");

                return 2;
            }

            await KillAndWaitAsync("PD2Launcher", plan.Pd2Launcher);
            await KillAndWaitAsync("SteamPD2", plan.SteamPd2);

            // Give Windows/antivirus a moment to release executable handles.
            await Task.Delay(1500);

            bool launcherUpdated = false;
            bool steamUpdated = false;

            if (launcherTempReady)
            {
                launcherUpdated = await TryReplaceWithRetriesAsync(
                    plan.TempPd2Launcher,
                    plan.Pd2Launcher,
                    "PD2Launcher");
            }
            else
            {
                Log("PD2Launcher temp file is unavailable; launcher replacement skipped.");
            }

            if (steamTempReady)
            {
                steamUpdated = await TryReplaceWithRetriesAsync(
                    plan.TempSteamPd2,
                    plan.SteamPd2,
                    "SteamPD2");
            }
            else
            {
                Log("SteamPD2 temp file is unavailable; SteamPD2 replacement skipped.");
            }

            if (launcherTempReady && !launcherUpdated)
            {
                Log(
                    "PD2Launcher replacement failed. " +
                    "The launcher will not be restarted to avoid another update loop.");

                return 3;
            }

            if (!launcherUpdated && !steamUpdated)
            {
                Log("No files were updated successfully.");
                return 4;
            }

            string executableToStart = SelectExecutableToStart(
                plan,
                launcherWasRunning,
                steamWasRunning);

            bool started = await StartExecutableAsync(executableToStart);
            return started ? 0 : 5;
        }

        private static UpdatePlan ResolvePlan(string[] args)
        {
            string updaterDirectory = Path.GetFullPath(AppContext.BaseDirectory);

            if (args.Length == 4 || args.Length == 5)
            {
                Log("Using new 4/5 argument format.");

                string pd2Launcher = NormalizePath(args[0], updaterDirectory);
                string installDirectory =
                    Path.GetDirectoryName(pd2Launcher) ?? updaterDirectory;

                return new UpdatePlan
                {
                    InstallDirectory = installDirectory,
                    Pd2Launcher = pd2Launcher,
                    TempPd2Launcher = NormalizePath(args[1], installDirectory),
                    SteamPd2 = NormalizePath(args[2], installDirectory),
                    TempSteamPd2 = NormalizePath(args[3], installDirectory),
                    ExplicitExecutableToStart = args.Length == 5
                        ? NormalizePath(args[4], installDirectory)
                        : null
                };
            }

            if (args.Length == 6 || args.Length == 7)
            {
                Log(
                    "Using legacy 6/7 argument format. " +
                    "PD2Shared arguments will be ignored.");

                string pd2Launcher = NormalizePath(args[0], updaterDirectory);
                string installDirectory =
                    Path.GetDirectoryName(pd2Launcher) ?? updaterDirectory;

                return new UpdatePlan
                {
                    InstallDirectory = installDirectory,
                    Pd2Launcher = pd2Launcher,
                    TempPd2Launcher = NormalizePath(args[1], installDirectory),
                    SteamPd2 = NormalizePath(args[4], installDirectory),
                    TempSteamPd2 = NormalizePath(args[5], installDirectory),
                    ExplicitExecutableToStart = args.Length == 7
                        ? NormalizePath(args[6], installDirectory)
                        : null
                };
            }

            // Never abandon already-downloaded temp files only because the caller
            // used an unexpected argument count. Infer the standard filenames from
            // the updater's own directory instead.
            Log(
                $"Unexpected argument count: {args.Length}. " +
                "Falling back to self-contained repair mode.");

            string fallbackDirectory = updaterDirectory;
            string? explicitExecutable = null;

            // A manually supplied directory is accepted as a convenience.
            if (args.Length == 1)
            {
                string candidate = args[0].Trim().Trim('"');

                if (Directory.Exists(candidate))
                {
                    fallbackDirectory = Path.GetFullPath(candidate);
                }
                else if (candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    explicitExecutable = NormalizePath(candidate, updaterDirectory);
                    fallbackDirectory =
                        Path.GetDirectoryName(explicitExecutable) ?? updaterDirectory;
                }
            }

            return new UpdatePlan
            {
                InstallDirectory = fallbackDirectory,
                Pd2Launcher = Path.Combine(fallbackDirectory, "PD2Launcher.exe"),
                TempPd2Launcher = Path.Combine(
                    fallbackDirectory,
                    "TempPD2Launcher.exe"),
                SteamPd2 = Path.Combine(fallbackDirectory, "SteamPD2.exe"),
                TempSteamPd2 = Path.Combine(
                    fallbackDirectory,
                    "TempSteamPD2.exe"),
                ExplicitExecutableToStart = explicitExecutable
            };
        }

        private static void LogPlan(UpdatePlan plan)
        {
            Log($"Install directory: {plan.InstallDirectory}");
            Log($"PD2Launcher: {plan.Pd2Launcher}");
            Log($"TempPD2Launcher: {plan.TempPd2Launcher}");
            Log($"SteamPD2: {plan.SteamPd2}");
            Log($"TempSteamPD2: {plan.TempSteamPd2}");
            Log(
                "Explicit executable to start: " +
                (plan.ExplicitExecutableToStart ?? "<automatic>"));
        }

        private static string NormalizePath(string path, string baseDirectory)
        {
            string cleanPath = path.Trim().Trim('"');

            return Path.GetFullPath(
                Path.IsPathRooted(cleanPath)
                    ? cleanPath
                    : Path.Combine(baseDirectory, cleanPath));
        }

        private static async Task<Dictionary<string, ManifestItem>?>
            TryGetManifestItemsAsync()
        {
            try
            {
                Log($"Downloading launcher manifest: {ManifestUrl}");

                using HttpResponseMessage response =
                    await HttpClient.GetAsync(ManifestUrl);

                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();

                ManifestResponse? manifest = JsonSerializer.Deserialize<ManifestResponse>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                Dictionary<string, ManifestItem> items =
                    manifest?.Items?
                        .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                        .ToDictionary(
                            item => item.Name,
                            item => item,
                            StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, ManifestItem>(
                        StringComparer.OrdinalIgnoreCase);

                Log($"Manifest returned {items.Count} items.");
                return items;
            }
            catch (Exception ex)
            {
                Log(
                    "Manifest download failed. Existing valid temp files " +
                    $"can still be installed. Error: {ex.Message}");

                return null;
            }
        }

        private static async Task<bool> EnsureTempFileAsync(
            string itemName,
            string tempPath,
            Dictionary<string, ManifestItem>? manifestItems)
        {
            ManifestItem? manifestItem = null;
            manifestItems?.TryGetValue(itemName, out manifestItem);

            if (File.Exists(tempPath))
            {
                if (ValidateUpdateFile(tempPath, manifestItem, out string validationMessage))
                {
                    Log($"Using existing {itemName} temp file: {validationMessage}");
                    return true;
                }

                Log(
                    $"Existing temp file is invalid and will be replaced: " +
                    $"{tempPath}. {validationMessage}");

                TryDelete(tempPath);
            }

            if (manifestItem == null)
            {
                Log(
                    $"Cannot download {itemName}: it was not found in the manifest.");

                return false;
            }

            return await DownloadUpdateFileAsync(manifestItem, tempPath);
        }

        private static async Task<bool> DownloadUpdateFileAsync(
            ManifestItem item,
            string tempPath)
        {
            string downloadUrl = ResolveDownloadUrl(item);
            string partialPath = tempPath + ".download";

            TryDelete(partialPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(tempPath) ?? AppContext.BaseDirectory);

            const int maxAttempts = 3;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    Log(
                        $"Downloading {item.Name} " +
                        $"(attempt {attempt}/{maxAttempts}) from {downloadUrl}");

                    using HttpResponseMessage response = await HttpClient.GetAsync(
                        downloadUrl,
                        HttpCompletionOption.ResponseHeadersRead);

                    response.EnsureSuccessStatusCode();

                    await using Stream source =
                        await response.Content.ReadAsStreamAsync();

                    await using (var destination = new FileStream(
                        partialPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        useAsync: true))
                    {
                        await source.CopyToAsync(destination);
                        await destination.FlushAsync();
                    }

                    if (!ValidateUpdateFile(
                        partialPath,
                        item,
                        out string validationMessage))
                    {
                        throw new IOException(
                            $"Downloaded file validation failed: {validationMessage}");
                    }

                    File.Move(partialPath, tempPath, overwrite: true);
                    Log($"Downloaded and validated {item.Name}: {validationMessage}");
                    return true;
                }
                catch (Exception ex)
                {
                    Log(
                        $"Download attempt {attempt}/{maxAttempts} failed for " +
                        $"{item.Name}: {ex.Message}");

                    TryDelete(partialPath);

                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(1500);
                    }
                }
            }

            Log($"Failed to download {item.Name} after {maxAttempts} attempts.");
            return false;
        }

        private static string ResolveDownloadUrl(ManifestItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.MediaLink))
            {
                if (Uri.TryCreate(item.MediaLink, UriKind.Absolute, out Uri? absolute))
                {
                    return absolute.ToString();
                }

                return new Uri(new Uri(ManifestUrl), item.MediaLink).ToString();
            }

            return new Uri(new Uri(ManifestUrl), Uri.EscapeDataString(item.Name))
                .ToString();
        }

        private static bool ValidateUpdateFile(
            string path,
            ManifestItem? manifestItem,
            out string message)
        {
            try
            {
                var fileInfo = new FileInfo(path);

                if (!fileInfo.Exists)
                {
                    message = "file does not exist";
                    return false;
                }

                if (fileInfo.Length < 32768)
                {
                    message = $"file is unexpectedly small ({fileInfo.Length} bytes)";
                    return false;
                }

                using (var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                {
                    if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z')
                    {
                        message = "file does not have a valid MZ executable header";
                        return false;
                    }
                }

                if (manifestItem != null && manifestItem.Size > 0 &&
                    fileInfo.Length != manifestItem.Size)
                {
                    message =
                        $"size mismatch: expected {manifestItem.Size}, " +
                        $"received {fileInfo.Length}";

                    return false;
                }

                if (manifestItem != null &&
                    !string.IsNullOrWhiteSpace(manifestItem.Crc32c))
                {
                    string actualCrc = CalculateCrc32CBase64(path);

                    if (!string.Equals(
                        actualCrc,
                        manifestItem.Crc32c,
                        StringComparison.Ordinal))
                    {
                        message =
                            $"CRC32C mismatch: expected {manifestItem.Crc32c}, " +
                            $"received {actualCrc}";

                        return false;
                    }

                    message =
                        $"{fileInfo.Length} bytes, CRC32C {actualCrc}";

                    return true;
                }

                message = $"{fileInfo.Length} bytes with valid MZ header";
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private static bool IsMatchingProcessRunning(
            string processName,
            string expectedExecutablePath)
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (ProcessMatchesPath(process, expectedExecutablePath))
                    {
                        return true;
                    }
                }
                catch
                {
                    // The process may have exited between enumeration and inspection.
                }
                finally
                {
                    process.Dispose();
                }
            }

            return false;
        }

        private static async Task KillAndWaitAsync(
            string processName,
            string expectedExecutablePath)
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (!ProcessMatchesPath(process, expectedExecutablePath))
                    {
                        Log(
                            $"Skipping {processName} ({process.Id}) because it is " +
                            "running from a different installation directory.");

                        continue;
                    }

                    Log($"Closing {process.ProcessName} ({process.Id}).");
                    process.Kill(entireProcessTree: true);

                    Task waitTask = process.WaitForExitAsync();
                    Task completed = await Task.WhenAny(
                        waitTask,
                        Task.Delay(TimeSpan.FromSeconds(20)));

                    if (completed != waitTask)
                    {
                        Log(
                            $"Timed out waiting for {processName} ({process.Id}) " +
                            "to exit.");
                    }
                    else
                    {
                        Log($"{processName} ({process.Id}) exited.");
                    }
                }
                catch (Exception ex)
                {
                    Log(
                        $"Failed to close {processName} ({process.Id}): " +
                        ex.Message);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private static bool ProcessMatchesPath(
            Process process,
            string expectedExecutablePath)
        {
            string? actualPath = process.MainModule?.FileName;

            if (string.IsNullOrWhiteSpace(actualPath))
            {
                return false;
            }

            return string.Equals(
                Path.GetFullPath(actualPath),
                Path.GetFullPath(expectedExecutablePath),
                StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<bool> TryReplaceWithRetriesAsync(
            string tempPath,
            string finalPath,
            string label)
        {
            if (!File.Exists(tempPath))
            {
                Log($"{label} temp file missing: {tempPath}");
                return false;
            }

            string backupPath = finalPath + ".update-backup";
            const int maxAttempts = 15;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                bool originalMovedToBackup = false;

                try
                {
                    TryDelete(backupPath);

                    if (File.Exists(finalPath))
                    {
                        File.Move(finalPath, backupPath, overwrite: true);
                        originalMovedToBackup = true;
                    }

                    File.Move(tempPath, finalPath, overwrite: true);

                    if (!ValidateUpdateFile(finalPath, null, out string validationMessage))
                    {
                        throw new IOException(
                            $"Installed executable failed validation: {validationMessage}");
                    }

                    TryDelete(backupPath);
                    Log($"{label} updated successfully: {validationMessage}");
                    return true;
                }
                catch (Exception ex)
                {
                    Log(
                        $"[{label}] replacement attempt {attempt}/{maxAttempts} " +
                        $"failed: {ex.Message}");

                    try
                    {
                        if (originalMovedToBackup && File.Exists(backupPath))
                        {
                            TryDelete(finalPath);
                            File.Move(backupPath, finalPath, overwrite: true);
                            Log($"[{label}] restored the previous executable.");
                        }
                    }
                    catch (Exception restoreException)
                    {
                        Log(
                            $"[{label}] failed to restore previous executable: " +
                            restoreException.Message);
                    }

                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(1000);
                    }
                }
            }

            Log($"Failed to update {label} after {maxAttempts} attempts.");
            return false;
        }

        private static string SelectExecutableToStart(
            UpdatePlan plan,
            bool launcherWasRunning,
            bool steamWasRunning)
        {
            if (!string.IsNullOrWhiteSpace(plan.ExplicitExecutableToStart))
            {
                return plan.ExplicitExecutableToStart;
            }

            if (steamWasRunning && File.Exists(plan.SteamPd2))
            {
                return plan.SteamPd2;
            }

            // Default to the launcher for automatic updates, manual repair runs,
            // and cases where the launcher exited before the updater inspected it.
            return plan.Pd2Launcher;
        }

        private static async Task<bool> StartExecutableAsync(string path)
        {
            if (!File.Exists(path))
            {
                Log($"Executable to restart was not found: {path}");
                return false;
            }

            try
            {
                Log($"Starting: {path}");

                Process? process = Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    WorkingDirectory =
                        Path.GetDirectoryName(path) ?? AppContext.BaseDirectory,
                    UseShellExecute = true
                });

                if (process == null)
                {
                    Log("Process.Start returned null.");
                    return false;
                }

                int processId = process.Id;
                await Task.Delay(1000);

                if (process.HasExited)
                {
                    Log(
                        $"Started process {processId}, but it exited immediately " +
                        $"with code {process.ExitCode}.");

                    process.Dispose();
                    return false;
                }

                Log($"Started successfully with process ID {processId}.");
                process.Dispose();
                return true;
            }
            catch (Exception ex)
            {
                Log($"Failed to start executable: {ex.Message}");
                return false;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Log($"Could not delete {path}: {ex.Message}");
            }
        }

        private static string CalculateCrc32CBase64(string path)
        {
            uint crc = 0xFFFFFFFF;
            byte[] buffer = new byte[81920];

            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            int bytesRead;

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int index = 0; index < bytesRead; index++)
                {
                    crc = Crc32CTable[(int)((crc ^ buffer[index]) & 0xFF)] ^
                          (crc >> 8);
                }
            }

            crc = ~crc;

            Span<byte> bigEndian = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(bigEndian, crc);
            return Convert.ToBase64String(bigEndian);
        }

        private static uint[] CreateCrc32CTable()
        {
            const uint polynomial = 0x82F63B78;
            var table = new uint[256];

            for (uint value = 0; value < table.Length; value++)
            {
                uint entry = value;

                for (int bit = 0; bit < 8; bit++)
                {
                    entry = (entry & 1) != 0
                        ? (entry >> 1) ^ polynomial
                        : entry >> 1;
                }

                table[(int)value] = entry;
            }

            return table;
        }

        private static void Log(string message)
        {
            string line =
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}";

            try
            {
                string logPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "update.log");

                File.AppendAllText(
                    logPath,
                    line + Environment.NewLine);
            }
            catch
            {
                // Logging must never prevent an update.
            }

            try
            {
                Console.WriteLine(line);
            }
            catch
            {
                // The published executable may not have an attached console.
            }
        }

        private sealed class UpdatePlan
        {
            public string InstallDirectory { get; init; } = string.Empty;
            public string Pd2Launcher { get; init; } = string.Empty;
            public string TempPd2Launcher { get; init; } = string.Empty;
            public string SteamPd2 { get; init; } = string.Empty;
            public string TempSteamPd2 { get; init; } = string.Empty;
            public string? ExplicitExecutableToStart { get; init; }
        }

        private sealed class ManifestResponse
        {
            public List<ManifestItem> Items { get; set; } = new();
        }

        private sealed class ManifestItem
        {
            public string Name { get; set; } = string.Empty;
            public string? MediaLink { get; set; }
            public long Size { get; set; }
            public string? Crc32c { get; set; }
        }
    }
}

