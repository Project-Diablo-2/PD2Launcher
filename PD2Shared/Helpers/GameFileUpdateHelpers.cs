using PD2Shared.Models;
using System.Diagnostics;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using PD2Shared.Storage;
using static PD2Shared.Constants;
using PD2Shared.Interfaces;

namespace PD2Shared.Helpers
{
    public class GameFileUpdateHelpers
    {
        private readonly HttpClient _httpClient;

        public GameFileUpdateHelpers(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromMinutes(3);
        }

        public async Task UpdateFromShaMetadataAsync(ILocalStorage localStorage, IProgress<double> progress, Action onComplete)
        {
            FileUpdateModel fileUpdateModel = localStorage.LoadSection<FileUpdateModel>(PD2Shared.Models.StorageKey.FileUpdateModel);
            string installPath = Directory.GetCurrentDirectory();
            string fullUpdatePath = Path.Combine(installPath, fileUpdateModel.FilePath);
            Directory.CreateDirectory(fullUpdatePath);

            string metadataUrl = $"{fileUpdateModel.Client.TrimEnd('/')}/metadata.json";
            string localMetaPath = Path.Combine(installPath, "local_metadata.json");
            string? localMetadataContent = File.Exists(localMetaPath) ? await File.ReadAllTextAsync(localMetaPath) : null;

            try
            {
                Debug.WriteLine($"\n================ metadata URL: {metadataUrl}\n");
                var response = await _httpClient.GetAsync(metadataUrl);
                response.EnsureSuccessStatusCode();
                string remoteMetadataContent = await response.Content.ReadAsStringAsync();

                if (localMetadataContent != null && localMetadataContent == remoteMetadataContent)
                {
                    Debug.WriteLine("Metadata unchanged. Skipping SHA1 update.");
                    return;
                }

                var parsed = JObject.Parse(remoteMetadataContent);
                var checksums = parsed["checksum"]?.ToObject<List<string>>() ?? new();

                var shaFiles = checksums.Select(entry =>
                {
                    var parts = entry.Split("  ", 2);
                    if (parts.Length != 2) return null;
                    return new ShaHashFileItem
                    {
                        Name = parts[1].Trim(),
                        MediaLink = metadataUrl.Replace("metadata.json", "") + parts[1].Trim(),
                        Hash = parts[0].Trim()
                    };
                }).Where(x => x != null).ToList();

                int processed = 0;

                foreach (var file in shaFiles!)
                {
                    Debug.WriteLine($"{file.Name}");
                    if (file.Name.EndsWith("/")) continue;

                    string localFile = Path.Combine(fullUpdatePath, file.Name.Replace("/", Path.DirectorySeparatorChar.ToString()));
                    string destination = Path.Combine(installPath, file.Name.Replace("/", Path.DirectorySeparatorChar.ToString()));

                    Directory.CreateDirectory(Path.GetDirectoryName(localFile)!);
                    bool fileExists = File.Exists(localFile);

                    if (!fileExists || !CompareSha1(localFile, file.Hash))
                    {
                        await DownloadFileAsync(file.MediaLink, localFile);
                        File.Copy(localFile, destination, true);
                    }

                    processed++;
                    progress?.Report((double)processed / shaFiles.Count);
                }

                await File.WriteAllTextAsync(localMetaPath, remoteMetadataContent);
                onComplete?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SHA Update] Failed: {ex.Message}");
                onComplete?.Invoke();
            }
        }

        public bool CompareSha1(string filePath, string expectedHash)
        {
            using var sha1 = SHA1.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = sha1.ComputeHash(stream);
            var localHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            return localHash == expectedHash.ToLowerInvariant();
        }

        private async Task DownloadFileAsync(string url, string destination)
        {
            using var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fs = new FileStream(destination, FileMode.Create, FileAccess.Write);
            await stream.CopyToAsync(fs);
        }
    }

    public class ShaHashFileItem
    {
        public string Name { get; set; }
        public string MediaLink { get; set; }
        public string Hash { get; set; }
    }
}