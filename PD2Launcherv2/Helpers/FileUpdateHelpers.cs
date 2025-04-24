﻿using Force.Crc32;
using PD2Launcherv2.Interfaces;
using PD2Launcherv2.Models;
using ProjectDiablo2Launcherv2;
using ProjectDiablo2Launcherv2.Models;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows;

namespace PD2Launcherv2.Helpers
{
    public class FileUpdateHelpers
    {
        private readonly HttpClient _httpClient;
        private readonly List<string> _excludedFiles = Constants.excludedFiles;

        public FileUpdateHelpers(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromMinutes(3);
        }

        public void StartUpdateProcess()
        {
            string launcherDirectory = Directory.GetCurrentDirectory();
            string updateUtilityPath = Path.Combine(launcherDirectory, "UpdateUtility.exe");
            string mainLauncherPath = Path.Combine(launcherDirectory, "PD2Launcher.exe");
            string tempLauncherPath = Path.Combine(launcherDirectory, "TempLauncher.exe");
            Debug.WriteLine(File.Exists(updateUtilityPath));
            Debug.WriteLine($"\n\n\nupdateUtilityPath: {updateUtilityPath}");
            Debug.WriteLine($"mainLauncherPath: {mainLauncherPath}");
            Debug.WriteLine($"mainLauncherPath: {mainLauncherPath}");
            Debug.WriteLine($"tempLauncherPath: {tempLauncherPath}\n\n\n");

            if (File.Exists(updateUtilityPath) && File.Exists(tempLauncherPath))
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = updateUtilityPath,
                    Arguments = $"\"{mainLauncherPath}\" \"{tempLauncherPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                Process.Start(startInfo);
            }
            else
            {
                Debug.WriteLine("Update Utility or TempLauncher.exe not found.");
            }
        }

        public async Task<List<CloudFileItem>> GetCloudFileMetadataAsync(string cloudFileBucket)
        {
            int maxRetries = 3;
            int delayBetweenRetries = 2000; //2 sec's

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await _httpClient.GetAsync(cloudFileBucket);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var json = JsonSerializer.Deserialize<CloudFilesResponse>(content, options);

                    return json?.Items.Select(i => new CloudFileItem
                    {
                        Name = i.Name,
                        MediaLink = i.MediaLink,
                        Crc32c = i.Crc32c,
                    }).ToList() ?? new List<CloudFileItem>();
                }
                catch (HttpRequestException ex) when (attempt < maxRetries)
                {
                    Debug.WriteLine($"[Retry {attempt}/{maxRetries}] HTTP request failed: {ex.Message}. Retrying...");
                    await Task.Delay(delayBetweenRetries);
                }
            }

            Debug.WriteLine("Failed to retrieve cloud metadata after retries.");
            return new List<CloudFileItem>();
        }

        private async Task<bool> DownloadFileAsync(string fileUrl, string destinationPath, IProgress<double> progress)
        {
            // add retries for s10 Time out bugs from people having shitty internet
            int maxRetries = 3;
            int delayBetweenRetries = 2000; // 2 seconds

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var response = await _httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                    var buffer = new byte[8192];
                    int bytesRead;
                    double totalRead = 0;
                    long totalBytes = response.Content.Headers.ContentLength ?? -1;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            progress.Report(totalRead / totalBytes);
                        }
                    }

                    return true; // Success
                }
                catch (HttpRequestException ex) when (attempt < maxRetries)
                {
                    Debug.WriteLine($"[Retry {attempt}/{maxRetries}] Failed to download {fileUrl}: {ex.Message}. Retrying...");
                    await Task.Delay(delayBetweenRetries);
                }
            }

            Debug.WriteLine($"Failed to download {fileUrl} after retries.");
            return false;
        }

        /**
        * Compare the CRC of two local files.
        */
        public bool CompareLocalFilesCRC(string sourceFilePath, string destinationFilePath)
        {
            // First, check if both files exist to avoid FileNotFoundException
            if (!File.Exists(sourceFilePath) || !File.Exists(destinationFilePath))
            {
                return false; // One or both files do not exist, so they can't be compared
            }

            uint crcSource = Crc32CFromFile(sourceFilePath);
            uint crcDestination = Crc32CFromFile(destinationFilePath);

            return crcSource == crcDestination;
        }

        public uint Crc32CFromFile(string filePath)
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var memoryStream = new MemoryStream();
            fileStream.CopyTo(memoryStream);
            byte[] fileBytes = memoryStream.ToArray();

            return Crc32CAlgorithm.Compute(fileBytes);
        }

        /**
         * Method for checking if a file needs to be updated
         */
        public bool CompareCRC(string filePath, string crcHash)
        {
            uint localCrc = Crc32CFromFile(filePath);
            byte[] remoteCrcBytes = Convert.FromBase64String(crcHash);
            uint remoteCrc = BitConverter.ToUInt32(remoteCrcBytes.Reverse().ToArray());
            return localCrc == remoteCrc;
        }

        private async Task TryDownloadFileAsync(string mediaLink, string localFilePath, int retries, IProgress<double> progress)
        {
            try
            {
                var response = await _httpClient.GetAsync(mediaLink);
                if (response.IsSuccessStatusCode)
                {
                    using (var fs = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"Failed to download file: {mediaLink}, due to network issues: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to download file: {mediaLink}, due to error: {ex.Message}");
            }
        }

        public async Task UpdateFilesCheck(ILocalStorage _localStorage, IProgress<double> progress, Action onDownloadComplete)
        {
            Debug.WriteLine("\nstart UpdateFilesCheck");
            var fileUpdateModel = _localStorage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel);
            if (fileUpdateModel == null)
            {
                Debug.WriteLine("FileUpdateModel is not set or directory does not exist.");
                return;
            }

            var installPath = Directory.GetCurrentDirectory();
            var fullUpdatePath = Path.Combine(installPath, fileUpdateModel.FilePath);
            Directory.CreateDirectory(fullUpdatePath);

            try
            {
                var cloudFileItems = await GetCloudFileMetadataAsync(fileUpdateModel.Client);
                int processedFiles = 0;

                foreach (var cloudFile in cloudFileItems)
                {
                    if (cloudFile.Name.EndsWith("/")) continue;

                    var normalizedPath = cloudFile.Name.Replace("/", Path.DirectorySeparatorChar.ToString());
                    var localFilePath = Path.Combine(fullUpdatePath, normalizedPath);

                    var directoryPath = Path.GetDirectoryName(localFilePath);
                    if (directoryPath != null) Directory.CreateDirectory(directoryPath);

                    bool shouldExclude = IsFileExcluded(cloudFile.Name);
                    bool localFileExists = File.Exists(localFilePath);

                    if (shouldExclude && !localFileExists)
                    {
                        await TryDownloadFileAsync(cloudFile.MediaLink, localFilePath, 3, progress);
                    }
                    else if (!shouldExclude && (!localFileExists || !CompareCRC(localFilePath, cloudFile.Crc32c)))
                    {
                        // For non-excluded files, download if they don't exist or CRC check fails.
                        await TryDownloadFileAsync(cloudFile.MediaLink, localFilePath, 3, progress);
                    }

                    // For copying logic, ensure it's consistent with the conditions above.
                    if ((shouldExclude && !localFileExists) || (!shouldExclude && (!localFileExists || !CompareCRC(localFilePath, cloudFile.Crc32c))))
                    {
                        var destinationFilePath = Path.Combine(installPath, normalizedPath);
                        var destinationDirectory = Path.GetDirectoryName(destinationFilePath);
                        if (destinationDirectory != null && !Directory.Exists(destinationDirectory))
                        {
                            Directory.CreateDirectory(destinationDirectory);
                        }
                        File.Copy(localFilePath, destinationFilePath, true);
                    }

                    processedFiles++;
                    progress?.Report((double)processedFiles / cloudFileItems.Count);
                }

                onDownloadComplete?.Invoke();
            }
            catch (HttpRequestException ex)
            {
                // Handle network errors specifically
                Debug.WriteLine($"Network error occurred while updating files: {ex.Message}. Switching to offline mode.");
                onDownloadComplete?.Invoke();
            }
            catch (Exception ex)
            {
                // Handle other general exceptions
                Debug.WriteLine($"An error occurred while updating files: {ex.Message}");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ShowErrorMessage($"An error occurred while updating files: {ex.Message}\nPlease verify your game is closed and try again.");
                });
            }
            finally
            {
                Debug.WriteLine("end UpdateFilesCheck \n");
            }
        }

        public async Task SyncFilesFromEnvToRoot(ILocalStorage localStorage)
        {
            FileUpdateModel fileUpdate = localStorage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel);
            if (fileUpdate == null)
            {
                Debug.WriteLine("FileUpdateModel is not set.");
                return;
            }

            var installPath = Directory.GetCurrentDirectory();
            var envPath = Path.Combine(installPath, fileUpdate.FilePath);

            if (!Directory.Exists(envPath))
            {
                Debug.WriteLine($"{envPath} directory does not exist, skipping sync.");
                return;
            }

            var filesInEnv = Directory.EnumerateFiles(envPath, "*.*", SearchOption.AllDirectories);

            foreach (var sourceFilePath in filesInEnv)
            {
                var relativePath = Path.GetRelativePath(envPath, sourceFilePath).Replace(Path.DirectorySeparatorChar, '/');
                var destinationFilePath = Path.Combine(installPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

                var isExcluded = IsFileExcluded(relativePath);
                var destinationExists = File.Exists(destinationFilePath);

                // For excluded files, only copy if they do not exist in the destination.
                if (isExcluded && destinationExists)
                {
                    Debug.WriteLine($"Excluded file exists, skipping: {sourceFilePath}");
                    continue;
                }

                // Ensure the directory for the destination file exists.
                var destinationDirectory = Path.GetDirectoryName(destinationFilePath);
                if (destinationDirectory != null && !Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                // Copy the file if it doesn't exist at the destination or if the CRC values don't match (for non-excluded files).
                if (!destinationExists || (!isExcluded && !CompareLocalFilesCRC(sourceFilePath, destinationFilePath)))
                {
                    File.Copy(sourceFilePath, destinationFilePath, true);
                    Debug.WriteLine($"Copied or updated file from '{sourceFilePath}' to '{destinationFilePath}'.");
                }
            }
        }

        private void ShowErrorMessage(string message)
        {

            MessageBox.Show(message, "Error Updating Files", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public async Task UpdateLauncherCheck(ILocalStorage _localStorage, IProgress<double> progress, Action onDownloadComplete)
        {
            Debug.WriteLine("\n\n\n\nstart UpdateLauncherCheck");
            var fileUpdateModel = _localStorage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel);
            var installPath = Directory.GetCurrentDirectory();

            if (fileUpdateModel != null)
            {
                var cloudFileItems = await GetCloudFileMetadataAsync(fileUpdateModel.Launcher);

                foreach (var cloudFile in cloudFileItems)
                {
                    var localFilePath = Path.Combine(installPath, cloudFile.Name);

                    // Handle launcher update specifically
                    if (cloudFile.Name == "PD2Launcher.exe")
                    {
                        var launcherNeedsUpdate = !File.Exists(localFilePath) || !CompareCRC(localFilePath, cloudFile.Crc32c);
                        if (launcherNeedsUpdate)
                        {
                            await HandleLauncherUpdate(cloudFile.MediaLink, progress, onDownloadComplete);
                            // Exit the loop and method after handling the launcher update
                            return;
                        }
                    }
                    else
                    {
                        // Directly download or update other files
                        if (!File.Exists(localFilePath) || !CompareCRC(localFilePath, cloudFile.Crc32c))
                        {
                            await DownloadFileAsync(cloudFile.MediaLink, localFilePath, progress);
                        }
                    }
                }
            }
            else
            {
                Debug.WriteLine("FileUpdateModel is not set or directory does not exist.");
            }
            onDownloadComplete?.Invoke();
            Debug.WriteLine("end UpdateLauncherCheck \n");
        }

        //Include s10 bug fix for: "new version" popup can get stuck behind the launcher itself
        private async Task HandleLauncherUpdate(string launcherMediaLink, IProgress<double> progress, Action onDownloadComplete)
        {
            var installPath = Directory.GetCurrentDirectory();
            var tempDownloadPath = Path.Combine(installPath, "TempLauncher.exe");

            Application.Current.Dispatcher.Invoke(() =>
            {
                Window? ownerWindow = Application.Current.MainWindow;
                MessageBox.Show(ownerWindow, "A launcher update has been identified, select OK to start update.", "Update Ready", MessageBoxButton.OK, MessageBoxImage.Information);
            });

            await DownloadFileAsync(launcherMediaLink, tempDownloadPath, progress);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Window? ownerWindow = Application.Current.MainWindow;
                MessageBox.Show(ownerWindow, "The launcher will now close to apply an update. Please wait for the launcher to reopen.", "Update in Progress", MessageBoxButton.OK, MessageBoxImage.Information);
                StartUpdateProcess();
                Application.Current.MainWindow?.Close();
            });
            await Task.Delay(1500);

            onDownloadComplete?.Invoke();
        }

        private bool IsFileExcluded(string fileName)
        {
            return _excludedFiles.Any(excluded =>
                excluded.EndsWith("/*") && fileName.StartsWith(excluded.TrimEnd('*', '/')) ||
                excluded.Equals(fileName, StringComparison.OrdinalIgnoreCase) ||
                (excluded.Contains("*") && new Regex("^" + Regex.Escape(excluded).Replace("\\*", ".*") + "$").IsMatch(fileName)));
        }

    }

    public class CloudFileItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("mediaLink")]
        public string MediaLink { get; set; }

        [JsonPropertyName("crc32c")]
        public string Crc32c { get; set; }
    }

    public class CloudFilesResponse
    {
        [JsonPropertyName("items")]
        public List<CloudFileItem> Items { get; set; }
    }
}