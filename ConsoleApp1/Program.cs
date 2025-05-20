using PD2Shared.Helpers;
using System.Text.Json;

Console.WriteLine("=== PD2 File Sync Tool ===");

string[] environments = {
    "https://pd2-client-files.projectdiablo2.com/metadata.json", // Live
    "https://pd2-alpha-client-files.projectdiablo2.com/metadata.json" // Alpha
};

foreach (var metadataUrl in environments)
{
    Console.WriteLine($"\n--- Checking: {metadataUrl} ---");

    string baseUrl = metadataUrl.Replace("metadata.json", "");
    string localFolder = AppContext.BaseDirectory;

    try
    {
        var metadata = await AWSFileHandler.LoadRemoteJsonChecksumAsync(metadataUrl);

        foreach (var kvp in metadata)
        {
            string fileName = kvp.Key;
            string expectedHash = kvp.Value;
            string localPath = Path.Combine(localFolder, fileName);
            string remoteFileUrl = baseUrl + fileName;

            bool needsUpdate = false;
            try
            {
                needsUpdate = AWSFileHandler.IsFileOutdated(localPath, expectedHash);
            }
            catch (Exception hashEx)
            {
                Console.WriteLine($"[ERROR] Failed to hash {fileName}: {hashEx.Message}");
                needsUpdate = true; // Fail-safe: try to redownload
            }

            if (needsUpdate)
            {
                Console.WriteLine($"[UPDATE] {fileName} is missing or outdated. Downloading...");

                using var client = new HttpClient();
                try
                {
                    var fileBytes = await client.GetByteArrayAsync(remoteFileUrl);
                    Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                    await File.WriteAllBytesAsync(localPath, fileBytes);
                    Console.WriteLine($"[DONE] Downloaded {fileName}");
                }
                catch (Exception dlEx)
                {
                    Console.WriteLine($"[FAIL] Could not download {fileName}: {dlEx.Message}");
                }
            }
            else
            {
                Console.WriteLine($"[SKIP] {fileName} is up to date.");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Failed to load metadata from {metadataUrl}: {ex.Message}");
    }
}

Console.WriteLine("\n✅ Sync process complete. Press any key to exit.");
Console.ReadKey();