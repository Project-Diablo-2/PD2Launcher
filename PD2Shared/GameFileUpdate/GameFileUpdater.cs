using Newtonsoft.Json.Linq;
using Serilog.Events;
using System.Buffers;
using System.Collections.Immutable;
using System.Data;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using PD2Shared.Extensions;
using PD2Shared.GameFileUpdate.Internal;
using PD2Shared.Logging;
using static PD2Shared.Logging.LoggingStatic;
using PD2Shared.Models;
using PD2Shared.Utils;

namespace PD2Shared.GameFileUpdate
{
    using PV = ProgressValues;

    public partial class GameFileUpdater
    {
        // The default buffer size of FileStream() (https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.-ctor#system-io-filestream-ctor(system-string-system-io-filemode-system-io-fileaccess-system-io-fileshare-system-int32))
        private const int DefaultStreamBufferSize = 4096;
        // The default buffer size of Stream.CopyToAsync() (https://learn.microsoft.com/en-us/dotnet/api/system.io.stream.copytoasync#system-io-stream-copytoasync(system-io-stream-system-int32))
        private const int DefaultLargeStreamBufferSize = 81920;
        // A reasonably small buffer of just one page
        private const int DefaultNetworkStreamBufferSize = 4096;

        private readonly Dictionary<FileUpdateModel, Context> _fileUpdateModelToContext = new(new FileUpdateModelEqualityComparer());

        private static int CalculateFileBufferSize(long fileSize)
        {
            return (int)Math.Clamp(fileSize, (long)DefaultStreamBufferSize, (long)DefaultLargeStreamBufferSize);
        }

        private static int CalculateFileBufferSize(string path)
        {
            // Both: FileInfo constructor and its properties can throw
            return CalculateFileBufferSize(new FileInfo(path).Length);
        }

        private static FileStream OpenReadFileStream(string path, int bufferSize)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        }

        private static FileStream OpenCreateFileStream(string path, int bufferSize)
        {
            Env.EnsureDirectoryExists(path);

            return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        }

        private static FileStream OpenWriteFileStream(string path, int bufferSize, long offset)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan)
            {
                // While this can throw, Validation should have made sure that the file is of sufficient size
                Position = offset
            };
        }

        private static void LogManifestStats(ManifestEntry[] manifestEntries)
        {
            L.CallerDebug($"{manifestEntries.Count(e => e.Size != null)}/{manifestEntries.Length} sizes; {manifestEntries.Count(e => e.Xxh3Hash != null)}/{manifestEntries.Length} XXH3s");
        }

        private static async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken ct, IProgress<Tuple<long, long, long>>? progress = null)
        {
            int bufferSize = CalculateFileBufferSize(sourcePath);

            using (var inStream = OpenReadFileStream(sourcePath, bufferSize))
            {
                using (var outStream = OpenCreateFileStream(destinationPath, bufferSize))
                {
                    if (progress == null)
                    {
                        await inStream.CopyToAsync(outStream, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

                        try
                        {
                            int bytesRead;
                            long totalBytesCopied = 0;

                            while ((bytesRead = await inStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                            {
                                await outStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);

                                totalBytesCopied += bytesRead;
                                progress.Report(Tuple.Create<long, long, long>(bytesRead, totalBytesCopied, inStream.Length));
                            }
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }
                    }
                }
            }
        }

        private static async Task<Hash[]> ComputeHashesAsync(Digest[] digests, string path, long? sizeLimit, CancellationToken ct, IProgress<Tuple<long, long, long>>? progress = null)
        {
            int bufferSize = CalculateFileBufferSize(path);

            using var inStream = OpenReadFileStream(path, bufferSize);

            if (progress == null && digests.Length == 1)
            {
                return new Hash[] { await digests.First().HashStream(inStream, ct).ConfigureAwait(false) };
            }
            else
            {
                var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

                try
                {
                    int bytesRead;
                    long totalBytesRead = 0;

                    var sizeToRead = sizeLimit != null ? (int)Math.Min(buffer.Length, sizeLimit.Value - totalBytesRead) : buffer.Length;

                    while ((bytesRead = await inStream.ReadAsync(buffer, 0, sizeToRead, ct).ConfigureAwait(false)) > 0)
                    {
                        foreach (var d in digests)
                        {
                            d.Update(buffer, 0, bytesRead);
                        }

                        totalBytesRead += bytesRead;
                        progress?.Report(Tuple.Create<long, long, long>(bytesRead, totalBytesRead, sizeLimit != null ? sizeLimit.Value : inStream.Length));

                        if (sizeLimit != null && totalBytesRead >= sizeLimit.Value)
                        {
                            break;
                        }
                    }

                    return digests.Select(d => d.GetHash()).ToArray();
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        private static ManifestEntry[] LoadManifest(string path)
        {
            // Expected input:
            //
            // {
            //   "manifest": {
            //     "entries": {
            //       "BH-LICENSE.md": {
            //         "md5": "990edf479f989d2f07dd0d95dadfdc95",
            //         "size": 35181,
            //         "xxh3": "1940485fe884a490"
            //       },
            //       "BH.dll": {
            //         "md5": "ecdf6624097328a390926b0dcddc2d79",
            //         "size": 1423360,
            //         "xxh3": "2753ec14b38fd8fa"
            //       },
            //       "binkw32.dll": {
            //         "md5": "f0c8199c01b623d97d6597f38e5b52a0",
            //         "size": null
            //       },
            //       [...]
            //     },
            //     "count": 47
            //   }
            // }

            L.CallerInformation($"Loading '{path}'...");

            FileStream inStream;
            try
            {
                inStream = OpenReadFileStream(path, 0);
            }
            catch (Exception ex)
            {
                throw new LoadManifestException("Failed to open manifest.", ex);
            }

            JsonNode? rootNode;

            try
            {
                using (inStream)
                {
                    rootNode = JsonNode.Parse(inStream, new JsonNodeOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch (JsonException ex)
            {
                throw new LoadManifestException("Failed to parse manifest.", ex);
            }

            if (rootNode == null)
            {
                throw new LoadManifestException("Manifest JSON payload is null.");
            }

            const string rootFieldName = "manifest";

            var manifestNode = rootNode![rootFieldName] ?? throw new LoadManifestException($"Manifest JSON root field '{rootFieldName}' is absent or null.");

            SerializableManifest? serializableManifest;

            try
            {
                serializableManifest = JsonSerializer.Deserialize<SerializableManifest>(manifestNode, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    // AllowDuplicateProperties = false // Only available since .NET 10 (https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsondocumentoptions.allowduplicateproperties)
                });
            }
            catch (JsonException ex)
            {
                throw new LoadManifestException("Failed to deserialize manifest entries.", ex);
            }

            if (serializableManifest == null || serializableManifest.Entries == null)
            {
                throw new LoadManifestException("Manifest entries are null.");
            }

            if (serializableManifest.Entries.Count != serializableManifest.Count)
            {
                throw new LoadManifestException(message: $"Actual JSON entry count ({serializableManifest.Entries.Count}) does not match the manifest ({serializableManifest.Count}).");
            }

            {
                var uniquePaths = new HashSet<string>(serializableManifest.Entries.Count, StringComparer.OrdinalIgnoreCase);

                return serializableManifest.Entries
                    // De-duplicate entries
                    .Where(kvp =>
                    {
                        if (!uniquePaths.Add(kvp.Key))
                        {
                            throw new LoadManifestException($"Manifest entry with duplicate path encountered: '{kvp.Key}'");
                        }

                        return true;
                    })
                    .Select(kvp =>
                    {
                        try
                        {
                            return new ManifestEntry(kvp.Key, kvp.Value);
                        }
                        catch (Exception ex)
                        {
                            // Let Serilog output JSON-formatted SerializableManifest.Entry here
                            L.CallerError($"Failed to construct {nameof(ManifestEntry)} for '{kvp.Key}' using {kvp.Value}: {{@SerializableManifest.Entry}}", ExplicitArray(kvp.Value));
                            throw new LoadManifestException($"Failed to construct {nameof(ManifestEntry)} for '{kvp.Key}' with given {kvp.Value}.", ex);
                        }
                    })
                    .ToArray();
            }
        }

        private static async Task<bool> SaveManifest(string path, ManifestEntry[] manifestEntries, CancellationToken ct)
        {
            if (!manifestEntries.Any(e => e.Dirty))
            {
                L.CallerDebug("No dirty manifest entries found. Skipping saving the manifest.");
                return false;
            }

            L.CallerInformation($"Saving manifest to: '{path}'...");

            FileStream outStream;
            try
            {
                outStream = OpenCreateFileStream(path, 0);
            }
            catch (Exception ex)
            {
                throw new SaveManifestException($"Failed to create manifest file: '{path}'.", ex);
            }

            using (outStream)
            {
                // Refer to "Expected input" detailed in deserialization logic

                await JsonSerializer.SerializeAsync(outStream, new
                {
                    manifest = new SerializableManifest(manifestEntries)
                }, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                },
                ct).ConfigureAwait(false);
            }

            // Clear Dirty flags on all entries
            foreach (var e in manifestEntries)
            {
                e.Dirty = false;
            }

            LogManifestStats(manifestEntries);

            return true;
        }

        private static async Task<Tuple<bool?, Exception?>> TrySaveManifest(string path, ManifestEntry[] manifestEntries, CancellationToken ct)
        {
            bool? res = null;

            try
            {
                res = await SaveManifest(path, manifestEntries, ct).ConfigureAwait(false);
            }
            catch( Exception ex)
            {
                return Tuple.Create(res, (Exception?)ex);
            }

            return Tuple.Create(res, (Exception?)null);
        }

        private static async Task<ManifestEntry[]> DownloadMetadata(HttpClient httpClient, string url, CancellationToken ct)
        {
            L.CallerInformation($"Downloading metadata from: '{url}'...");

            using var response = await httpClient.GetAsync(url, ct).ConfigureAwait(false);
            response.ThrowIfUnsuccessful();

            JObject rootNode;

            try
            {
                rootNode = JObject.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            }
            catch (Newtonsoft.Json.JsonReaderException ex)
            {
                throw new LoadMetadataException("Failed to parse metadata.", ex);
            }

            if (rootNode == null)
            {
                throw new LoadMetadataException("Metadata JSON payload is null.");
            }

            const string rootFieldName = "checksum";

            var checksumNode = rootNode[rootFieldName] ?? throw new LoadMetadataException($"Metadata JSON root field '{rootFieldName}' is absent or null.");

            List<string>? stringEntries;

            try
            {
                stringEntries = checksumNode.ToObject<List<string>>();
            }
            catch (Newtonsoft.Json.JsonReaderException ex)
            {
                throw new LoadMetadataException("Failed to deserialize metadata entries.", ex);
            }

            if (stringEntries == null)
            {
                throw new LoadMetadataException("Metadata entries are null.");
            }

            var uniquePaths = new HashSet<string>(stringEntries.Count, StringComparer.OrdinalIgnoreCase);

            return stringEntries
                .Select(entry =>
                {
                    var parts = entry.Split("  ", 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length != 2)
                    {
                        throw new LoadMetadataException($"Invalid metadata entry: '{entry}'");
                    }

                    var path = parts[1];
                    var md5 = parts[0];

                    // Exclude directories?
                    if (path.EndsWith('/'))
                    {
                        throw new LoadMetadataException($"Metadata entry with directory-like path encountered: '{entry}'");
                    }

                    if (!uniquePaths.Add(path))
                    {
                        throw new LoadMetadataException($"Metadata entry with duplicate path encountered: '{entry}'");
                    }

                    try
                    {
                        return new ManifestEntry(path, md5);
                    }
                    catch (Exception ex)
                    {
                        throw new LoadMetadataException($"Failed to construct {nameof(ManifestEntry)} with metadata entry: '{entry}'", ex);
                    }
                })
                .ToArray();
        }

        private static async Task<Tuple<bool, long?, Xxh3Hash?>> ValidateFileAsync(
            string path,
            long? size,
            Hash expectedHash,
            CancellationToken ct,
            bool looseValidation = false,
            bool sizeIsMinimumSize = false,
            IProgress<Tuple<long, long, long>>? progress = null)
        {
            if (sizeIsMinimumSize && size == null)
            {
                throw new ArgumentException($"'{nameof(size)}' cannot be null when '{nameof(sizeIsMinimumSize)}' is true", nameof(size));
            }

            if (!await Env.FileExistsAsync(path).ConfigureAwait(false))
            {
                L.CallerWarning($"{path}: missing");

                return Tuple.Create(false, (long?)null, (Xxh3Hash?)null);
            }

            (var actualSize, var ex) = await Env.TryGetFileSizeAsync(path).ConfigureAwait(false);
            if (ex != null)
            {
                L.CallerError(ex, $"{nameof(Env.TryGetFileSizeAsync)}() for '{path}' failed.");

                return Tuple.Create(false, (long?)null, (Xxh3Hash?)null);
            }

            Debug.Assert(actualSize != null);

            if (sizeIsMinimumSize)
            {
                Debug.Assert(size != null);

                if (size.Value != actualSize.Value)
                {
                    if (size.Value > actualSize.Value)
                    {
                        // Partial download cannot be smaller than declared in PartialDownload
                        L.CallerWarning($"{path}: partial size mismatch: {size.Value} > {actualSize.Value} (actual)");

                        return Tuple.Create(false, actualSize, (Xxh3Hash ?)null);
                    }
                    else
                    {
                        L.CallerDebug($"{path}: acceptable partial size mismatch: {size.Value} <= {actualSize.Value} (actual)");
                    }
                }
            }
            else
            {
                if (looseValidation)
                {
                    if (actualSize.Value > 0)
                    {
                        // If the file exists and has non-zero size -- that's good enough
                        L.CallerDebug($"{path}: loosely validated: {actualSize.Value} (actual)");

                        return Tuple.Create(true, actualSize, (Xxh3Hash ?)null);
                    }
                    else
                    {
                        L.CallerWarning($"{path}: failed loose validation (empty file)");

                        return Tuple.Create(false, actualSize, (Xxh3Hash?)null);
                    }
                }
                else
                {
                    if (size != null && size.Value != actualSize.Value)
                    {
                        L.CallerWarning($"{path}: size mismatch: {size.Value} != {actualSize.Value} (actual)");

                        return Tuple.Create(false, actualSize, (Xxh3Hash?)null);
                    }
                }
            }

            List<DisposableDigest> digests = new(2);

            try
            {
                // Attempt to pick either DisposableMd5 or DisposableXxh3 digests.
                //
                // DisposableMd5 is significantly faster than NonFinalizingMd5 as it's merely a wrapper around native implementation (System.Security.Cryptography).
                // Meanwhile, NonFinalizingMd5 is a wrapper around BouncyCastle, which is purely managed code.
                //
                // Since performance matters in this scenario and there's no use for the digest to be non-finalizing, go with the disposable variant.
                digests.Add(Digest.GetDisposable(expectedHash));

                L.CallerVerbose($"{path}: validating against {expectedHash.Name} using {digests.First().GetType().Name}...");

                // If not validating against XXH3, make sure to compute one as well (and add it as the last element to be returned in the end)
                if (!digests.First().IsHashType<Xxh3Hash>())
                {
                    digests.Add(new DisposableXxh3());
                }

                Hash[] hashes = await ComputeHashesAsync(digests.ToArray(), path, sizeLimit: sizeIsMinimumSize ? size : (long?)null, ct, progress).ConfigureAwait(false);

                if (hashes.First() == expectedHash)
                {
                    return Tuple.Create(true, actualSize, (Xxh3Hash?)hashes.Last());
                }
                else
                {
                    L.CallerWarning($"{path}: {digests.First().HashName} mismatch");

                    return Tuple.Create(false, actualSize, (Xxh3Hash?)null);
                }
            }
            finally
            {
                foreach (var d in digests)
                {
                    d.Dispose();
                }
            }
        }

        private static async Task ValidateFilesAsync(
            WorkItem[] filesToValidate,
            ValidationKind validationKind,
            ParallelOptions parallelOptions,
            IProgress<ProgressValues.IData>? progress)
        {
            if (!filesToValidate.Any())
            {
                // Return early not to end up with totalBytesToValidate == 0
                L.CallerWarning($"Nothing to validate for {validationKind}.");
                return;
            }

            using var loggedRoutine = new LoggedRoutine();

            var updateThrottle = new UpdateThrottle();

            var totalsLock = new object();

            int totalFilesValidated = 0;
            int totalFilesToValidate = filesToValidate.Length;
            long totalBytesValidated = 0;
            long? totalBytesToValidate = null;

            if (validationKind.IsDownloadFiles())
            {
                // Factor in PartialDownloads
                if (filesToValidate.All(d => d.PartialDownload?.PartialSize != null || d.ManifestEntry.Size != null))
                {
                    totalBytesToValidate = filesToValidate.Sum(d => d.PartialDownload?.PartialSize ?? d.ManifestEntry.Size);
                }
            }
            else
            {
                if (filesToValidate.All(d => d.ManifestEntry.Size != null || d.ManifestEntry.LooseValidation))
                {
                    totalBytesToValidate = filesToValidate
                        .Where(d => d.ManifestEntry.Size != null || d.ManifestEntry.LooseValidation)
                        .Sum(d => d.ManifestEntry.LooseValidation ? 0 : d.ManifestEntry.Size!.Value);
                }
            }

            {
                var totalBytesToValidateStr = totalBytesToValidate == null ? "?" : Formatting.FormatSizeInMiB(totalBytesToValidate.Value);

                L.CallerInformation($"Validating {totalFilesToValidate} {validationKind} ({totalBytesToValidateStr} total)...");
            }

            progress?.Report(new PV()
                .Clear()
                .SetFileCount(totalFilesValidated, totalFilesToValidate)
                .SetBytes(totalBytesValidated, totalBytesToValidate)
                .Extract()
            );

            await Parallel.ForEachAsync(filesToValidate, parallelOptions, async (f, ct) =>
            {
                bool validatingPartialDownload = validationKind.IsDownloadFiles() && f.PartialDownload != null;

                string path = validationKind.IsDownloadFiles() ? f.DownloadPath : f.InstallPath;
                long? expectedSize = validatingPartialDownload ? f.PartialDownload!.PartialSize : f.ManifestEntry.Size;
                Hash expectedHash = validatingPartialDownload ? f.PartialDownload!.PartialXxh3Hash : f.ManifestEntry.BestHash;

                var doingLooseValidation = validationKind.IsInstallFiles() && f.ManifestEntry.LooseValidation;

                (bool validationSucceeded, long? actualSize, Xxh3Hash? xxh3Hash) = await ValidateFileAsync(
                    path,
                    expectedSize,
                    expectedHash,
                    ct,
                    doingLooseValidation,
                    sizeIsMinimumSize: validatingPartialDownload,
                    new DirectProgress<Tuple<long, long, long>>(t =>
                {
                    (var fileBytesValidated, _, _) = t;

                    lock (totalsLock)
                    {
                        totalBytesValidated += fileBytesValidated;

                        updateThrottle.UpdateIfPossible(() =>
                        {
                            var pv = new PV();

                            if (totalBytesToValidate != null)
                            {
                                pv.SetTotal(totalBytesValidated, totalBytesToValidate.Value);
                            }
                            else
                            {
                                pv.SetTotal(totalFilesValidated, totalFilesToValidate);
                            }
                            pv.SetBytes(totalBytesValidated, totalBytesToValidate);
                            progress?.Report(pv.Extract());
                        });
                    }
                })).ConfigureAwait(false);

                if (!validationSucceeded)
                {
                    // Discard PartialDownload due to failed validation
                    if (validatingPartialDownload)
                    {
                        L.CallerWarning($"{path}: partial download failed validation. Discarding...");

                        f.PartialDownload = null;
                    }
                }
                else
                {
                    // Loosely validated file's actual size is irrelevant
                    if (!doingLooseValidation && !validatingPartialDownload)
                    {
                        Debug.Assert(actualSize != null);
                        Debug.Assert(xxh3Hash is not null);

                        if (f.ManifestEntry.Size != actualSize)
                        {
                            lock (totalsLock)
                            {
                                if (totalBytesToValidate != null)
                                {
                                    totalBytesToValidate += (actualSize.Value - f.ManifestEntry.Size.GetValueOrDefault(0));
                                }
                            }

                            if (f.ManifestEntry.Size != null)
                            {
                                var fromSizeStr = Formatting.FormatSizeInMiB(f.ManifestEntry.Size.Value, appendUnits: false);
                                var toSizeStr = Formatting.FormatSizeInMiB(actualSize.Value);

                                L.CallerWarning($"{path}: updating {nameof(f.ManifestEntry.Size)} in manifest: {f.ManifestEntry.Size} -> {actualSize} bytes ({fromSizeStr} -> {toSizeStr})");
                            }

                            f.ManifestEntry.Size = actualSize.Value;
                        }

                        if (f.ManifestEntry.Xxh3Hash != xxh3Hash)
                        {
                            if (f.ManifestEntry.Xxh3Hash != null)
                            {
                                L.CallerWarning($"{path}: updating {nameof(f.ManifestEntry.Xxh3Hash)} in manifest");
                            }

                            f.ManifestEntry.Xxh3Hash = xxh3Hash;
                        }
                    }
                }

                switch (validationKind)
                {
                    case ValidationKind.InstallFiles:
                        f.InstallFileValidated = validationSucceeded;
                        break;

                    case ValidationKind.DownloadFiles:
                        f.DownloadFileValidated = validationSucceeded;
                        break;
                }

                lock (totalsLock)
                {
                    ++totalFilesValidated;

                    var pv = new PV();

                    pv.SetFileCount(totalFilesValidated, totalFilesToValidate);
                    if (totalBytesToValidate != null)
                    {
                        pv.SetTotal(totalBytesValidated, totalBytesToValidate.Value);
                    }
                    else
                    {
                        pv.SetTotal(totalFilesValidated, totalFilesToValidate);
                    }
                    pv.SetBytes(totalBytesValidated, totalBytesToValidate);
                    progress?.Report(pv.Extract());
                }
            }).ConfigureAwait(false);
        }

        private static async Task<long?> QueryDownloadAsync(HttpClient httpClient, string url, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);

            // Make absolutely clear no encoding is requested (https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Accept-Encoding)
            request.Headers.AcceptEncoding.Clear();
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
            // Explicitly request a "0-" range (the entire file) (https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Range#syntax)
            request.Headers.Range = new RangeHeaderValue((long?)0, (long?)null);

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.ThrowIfUnsuccessful();

            // Ranges are explicitly supported if server responds with 206.
            // A response of 200 and "Accept-Ranges: bytes" means the server generally accepts ranges, but cannot fulfill this particular request
            // (likely due to encoding) and will send the entire file instead.
            var acceptsRanges = response.StatusCode == System.Net.HttpStatusCode.PartialContent;

            if (!acceptsRanges)
            {
                L.CallerWarning($"{url}: {response.StatusCode}; Cannot resume");
            }

            {
                var h = response.Content.Headers;

                // 206 should also contain Content-Range header
                if (acceptsRanges && h.ContentRange != null)
                {
                    var contentLengthStr = h.ContentLength == null ? "?" : Formatting.FormatSizeInMiB(h.ContentLength.Value);

                    var cr = h.ContentRange;
                    L.CallerDebug($"{url}: {response.StatusCode}; Content-Range: {cr.From?.ToString() ?? ""}-{cr.To?.ToString() ?? ""}/{cr.Length?.ToString() ?? "*"}; Content-Length: {h.ContentLength?.ToString() ?? "?"} ({contentLengthStr})");

                    return cr.Length ?? h.ContentLength;
                }
                else
                {
                    var contentLengthStr = h.ContentLength == null ? "?" : Formatting.FormatSizeInMiB(h.ContentLength.Value);

                    L.CallerDebug($"{url}: {response.StatusCode}; Content-Length: {h.ContentLength?.ToString() ?? "?"} ({contentLengthStr})");

                    return h.ContentLength;
                }
            }
        }

        private static async Task QueryDownloadsAsync(
            HttpClient httpClient,
            WorkItem[] filesToQuery,
            long initialSize,
            ParallelOptions parallelOptions,
            IProgress<ProgressValues.IData>? progress)
        {
            using var loggedScope = new LoggedScope($"Querying {filesToQuery.Length} files...");

            var totalsLock = new object();

            long totalBytesQueried = initialSize;
            int totalFilesToQuery = filesToQuery.Length;
            int totalFilesQueried = 0;

            progress?.Report(new PV()
                .Clear()
                .SetFileCount(totalFilesQueried, totalFilesToQuery)
                .SetBytes(totalBytesQueried)
                .Extract()
            );

            await Parallel.ForEachAsync(filesToQuery, parallelOptions, async (f, ct) =>
            {
                var queriedSize = await QueryDownloadAsync(httpClient, f.Url, ct).ConfigureAwait(false);

                lock (totalsLock)
                {
                    ++totalFilesQueried;

                    if (queriedSize != null)
                    {
                        totalBytesQueried += queriedSize.Value;

                        if (f.ManifestEntry.Size != queriedSize)
                        {
                            if (f.ManifestEntry.Size != null)
                            {
                                var fromSizeStr = Formatting.FormatSizeInMiB(f.ManifestEntry.Size.Value, appendUnits: false);
                                var toSizeStr = Formatting.FormatSizeInMiB(queriedSize.Value);

                                L.CallerWarning($"{f.ManifestEntry.Path}: updating Size in manifest {f.ManifestEntry.Size} -> {queriedSize} bytes ({fromSizeStr} -> {toSizeStr})");
                            }

                            f.ManifestEntry.Size = queriedSize.Value;
                        }
                    }

                    progress?.Report(new PV()
                        .SetTotal(totalFilesQueried, totalFilesToQuery)
                        .SetFileCount(totalFilesQueried, totalFilesToQuery)
                        .SetBytes(totalBytesQueried)
                        .Extract()
                    );
                }
            }).ConfigureAwait(false);
        }

        private static async Task<DownloadResult> DownloadFileAsync(
            HttpClient httpClient,
            string url,
            string destinationPath,
            Md5Hash referenceMd5Hash,
            Hash expectedHash,
            CancellationToken ct,
            PartialDownload? downloadToResume = null,
            IProgress<Tuple<long, long, long?>>? progress = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (downloadToResume != null)
            {
                request.Headers.Range = new RangeHeaderValue(downloadToResume.PartialSize, (long?)null);
            }

            Hash actualExpectedHash = null!;
            NonFinalizingDigest digest = null!;
            NonFinalizingXxh3 xxh3Digest = null!;
            long totalBytesWritten = 0;

            try
            {
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                response.ThrowIfUnsuccessful();

                // Ranges are explicitly supported if server responds with 206.
                // A response of 200 and "Accept-Ranges: bytes" means the server generally accepts ranges, but cannot fulfill this particular request
                // (likely due to encoding) and will send the entire file instead.
                bool resuming = downloadToResume != null && response.StatusCode == System.Net.HttpStatusCode.PartialContent;

                if (resuming)
                {
                    L.CallerDebug($"{url}: resuming download at {downloadToResume!.PartialSize} ({Formatting.FormatSizeInMiB(downloadToResume.PartialSize)})...");

                    actualExpectedHash = downloadToResume!.ExpectedHash;
                    digest = downloadToResume!.Digest;
                    xxh3Digest = downloadToResume!.Xxh3Digest;
                    totalBytesWritten = downloadToResume!.PartialSize;
                }
                else
                {
                    if (downloadToResume != null)
                    {
                        L.CallerWarning($"{url}: restarting download...");
                    }
                    else
                    {
                        L.CallerDebug($"{url}: downloading...");
                    }

                    actualExpectedHash = expectedHash;
                    digest = Digest.GetNonFinalizing(actualExpectedHash);
                    xxh3Digest = digest.IsHashType<Xxh3Hash>() ? (NonFinalizingXxh3)digest : new NonFinalizingXxh3();
                    totalBytesWritten = 0;
                }

                L.CallerVerbose($"{url}: validating against {actualExpectedHash.Name} using {digest.GetType().Name}...");

                using var inStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

                FileStream outStream;

                if (resuming)
                {
                    outStream = OpenWriteFileStream(destinationPath, CalculateFileBufferSize(response.Content.Headers.ContentRange?.Length ?? 0), offset: downloadToResume!.PartialSize);
                }
                else
                {
                    outStream = OpenCreateFileStream(destinationPath, CalculateFileBufferSize(response.Content.Headers.ContentLength ?? 0));
                }

                using (outStream)
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(DefaultNetworkStreamBufferSize);

                    try
                    {
                        long? totalFileSize = resuming ? response.Content.Headers.ContentRange?.Length : response.Content.Headers.ContentLength;

                        int bytesRead;

                        while ((bytesRead = await inStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                        {
                            await outStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);

                            digest.Update(buffer, 0, bytesRead);
                            if (digest != xxh3Digest)
                            {
                                xxh3Digest.Update(buffer, 0, bytesRead);
                            }
                            totalBytesWritten += bytesRead;
                            progress?.Report(Tuple.Create<long, long, long?>(bytesRead, totalBytesWritten, totalFileSize));
                        }

                        if (digest.GetHash() != actualExpectedHash)
                        {
                            L.CallerError($"{url}: {digest.HashName} mismatch");
                            throw new DownloadHashMismatchException(innerException: null, digest.HashName);
                        }

                        return new DownloadResult((Xxh3Hash)xxh3Digest.GetHash());
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                bool userRequested = ex.CancellationToken == ct;

                var stateStr = userRequested ? "canceled" : "interrupted";
                var logEventLevel = userRequested ? LogEventLevel.Warning : LogEventLevel.Error;

                if (totalBytesWritten > 0)
                {
                    L.CallerWrite(logEventLevel, $"{url}: download {stateStr} at {totalBytesWritten} ({Formatting.FormatSizeInMiB(totalBytesWritten)})");
                    return new DownloadResult(
                        ex,
                        totalBytesWritten,
                        (Xxh3Hash)xxh3Digest.GetHash(),
                        xxh3Digest,
                        referenceMd5Hash,
                        digest,
                        actualExpectedHash);
                }
                else
                {
                    // Treat a zero-sized partial download candidate as an unrecoverable failed download

                    L.CallerError($"{url}: download {stateStr}.");
                    return new DownloadResult(ex);
                }
            }
            catch (GameFileUpdateException ex)
            {
                // These should have logged their errors by now

                return new DownloadResult(ex);
            }
            catch (Exception ex)
            {
                L.CallerError(ex, $"{url}: download failed.");

                return new DownloadResult(ex);
            }
        }

        private static async Task<DownloadResult[]> DownloadFilesAsync(
            HttpClient httpClient,
            WorkItem[] filesToDownload,
            ParallelOptions parallelOptions,
            IProgress<ProgressValues.IData>? progress,
            IProgress<bool>? offlineIndicatorProgress,
            IProgress<bool>? downloadErrorIndicatorProgress)
        {
            if (!filesToDownload.Any())
            {
                // Return early not to end up with totalBytesToDownload == 0
                L.CallerWarning($"Nothing to download.");
                return Array.Empty<DownloadResult>();
            }

            using var loggedRoutine = new LoggedRoutine();

            var updateThrottle = new UpdateThrottle();

            var totalsLock = new object();

            int totalFilesDownloaded = 0;
            int totalFilesToDownload = filesToDownload.Length;
            long totalBytesDownloaded = filesToDownload
                .Where(f => f.PartialDownload != null)
                .Sum(f => f.PartialDownload!.PartialSize);

            long? totalBytesToDownload = filesToDownload.Any(f => f.ManifestEntry.Size == null) ? (long?)null :
                filesToDownload
                .Sum(f => f.ManifestEntry.Size!.Value);

            // Monotonic value for throughput estimation
            long totalBytesDownloadedEver = 0;

            {
                var totalRemainingBytesToDownloadStr = totalBytesToDownload == null ? "?" : Formatting.FormatSizeInMiB(totalBytesToDownload.Value - totalBytesDownloaded);
                var totalPartialDownloads = filesToDownload.Count(f => f.PartialDownload != null);

                L.CallerInformation($"Downloading {totalFilesToDownload} file(s) ({totalRemainingBytesToDownloadStr} total; {totalPartialDownloads} partial download(s) being resumed)...");
            }

            {
                var pv = new PV().Clear();

                pv.SetFileCount(totalFilesDownloaded, totalFilesToDownload);
                if (totalBytesToDownload != null)
                {
                    pv.SetTotal(totalBytesDownloaded, totalBytesToDownload.Value);
                }
                else
                {
                    pv.SetTotal(totalFilesDownloaded, totalFilesToDownload);
                }
                pv.SetBytes(totalBytesDownloaded, totalBytesToDownload);

                progress?.Report(pv.Extract());
            }

            List<DownloadResult> downloadResults = new(totalFilesToDownload);

            try
            {
                // Do throughput estimation asynchronously to be able to detect and present any connection stalls

                var throughputStopwatch = Stopwatch.StartNew();
                bool throughputFirstTick = true;
                long previousElapsedMilliseconds = 0;

                const int MaxLastTotalBytesDownloadedEver = 3;
                // A poor-man's circular buffer to store the last few values of totalBytesDownloadedEver
                LinkedList<long> previousTotalBytesDownloadedEver = new();
                bool connectionStalled = false;

                // Hopefully this won't end up being too spammy on slower connections
                var throughputLoggingThrottle = new UpdateThrottle(intervalMilliseconds: 1000);
                var throughputLoggingPaused = false;

                using var throughputTimer = new SimpleTimer(TimeSpan.FromMilliseconds(250), () =>
                {
                    long localTotalBytesDownloadedEver = totalBytesDownloadedEver;

                    if (throughputFirstTick)
                    {
                        // Set up initial values

                        throughputFirstTick = false;

                        previousTotalBytesDownloadedEver.AddFirst(localTotalBytesDownloadedEver);
                        previousElapsedMilliseconds = throughputStopwatch.ElapsedMilliseconds;
                    }
                    else
                    {
                        var bytesDownloaded = localTotalBytesDownloadedEver - previousTotalBytesDownloadedEver.First!.Value;
                        previousTotalBytesDownloadedEver.AddFirst(localTotalBytesDownloadedEver);

                        if (previousTotalBytesDownloadedEver.Count > MaxLastTotalBytesDownloadedEver)
                        {
                            previousTotalBytesDownloadedEver.RemoveLast();
                        }

                        var currentElapsedMilliseconds = throughputStopwatch.ElapsedMilliseconds;
                        var elapsedMilliseconds = currentElapsedMilliseconds - previousElapsedMilliseconds;
                        previousElapsedMilliseconds = currentElapsedMilliseconds;

                        bool stalled =
                            previousTotalBytesDownloadedEver.Count >= MaxLastTotalBytesDownloadedEver &&
                            previousTotalBytesDownloadedEver.Distinct().Count() == 1;

                        // Stop logging if connection has stalled -- no need to keep putting out zeros into the log
                        if (stalled && throughputLoggingPaused)
                        {
                            offlineIndicatorProgress?.Report(true);

                            if (!connectionStalled)
                            {
                                connectionStalled = true;
                                L.CallerWarning("Connection stalled.");
                            }

                            return;
                        }
                        else
                        {
                            offlineIndicatorProgress?.Report(false);

                            throughputLoggingPaused = false;

                            if (connectionStalled)
                            {
                                connectionStalled = false;
                                L.CallerWarning("Connection restored.");
                            }
                        }

                        progress?.Report(new PV()
                            .SetBytesPerSec(bytesDownloaded, elapsedMilliseconds)
                            .Extract()
                        );

                        throughputLoggingThrottle.UpdateIfPossible(() =>
                        {
                            throughputLoggingPaused = stalled;
                            L.CallerDebug($"Throughput: {Formatting.FormatThroughputInMiB(bytesDownloaded, elapsedMilliseconds)}");
                        });
                    }
                });

                // The actual download...

                await Parallel.ForEachAsync(filesToDownload.OrderByDescending(f => f.ManifestEntry.Size).ToArray(), parallelOptions, async (f, ct) =>
                {
                    bool sizeConfirmed = false;

                    long previousTotalFileBytesDownloaded = f.PartialDownload?.PartialSize ?? 0;

                    f.DownloadResult = await DownloadFileAsync(
                        httpClient,
                        f.Url,
                        f.DownloadPath,
                        f.ManifestEntry.Md5Hash,
                        f.ManifestEntry.BestHash,
                        ct,
                        f.PartialDownload,
                        new DirectProgress<Tuple<long, long, long?>>(t =>
                        {
                            (var fileBytesDownloaded, var totalFileBytesDownloaded, var totalFileSize) = t;

                            if (!sizeConfirmed)
                            {
                                sizeConfirmed = true;

                                if (totalFileSize != null)
                                {
                                    lock (totalsLock)
                                    {
                                        if (totalBytesToDownload != null)
                                        {
                                            totalBytesToDownload += (totalFileSize.Value - f.ManifestEntry.Size.GetValueOrDefault(0));
                                        }
                                    }

                                    if (f.ManifestEntry.Size != totalFileSize)
                                    {
                                        if (f.ManifestEntry.Size != null)
                                        {
                                            var fromSizeStr = Formatting.FormatSizeInMiB(f.ManifestEntry.Size.Value, appendUnits: false);
                                            var toSizeStr = Formatting.FormatSizeInMiB(totalFileSize.Value);

                                            L.CallerWarning($"{f.ManifestEntry.Path}: updating {nameof(f.ManifestEntry.Size)} in manifest: {f.ManifestEntry.Size} -> {totalFileSize} bytes ({fromSizeStr} -> {toSizeStr})");
                                        }

                                        f.ManifestEntry.Size = totalFileSize;
                                    }
                                }
                            }

                            lock (totalsLock)
                            {
                                totalBytesDownloaded += (totalFileBytesDownloaded - previousTotalFileBytesDownloaded);
                                previousTotalFileBytesDownloaded = totalFileBytesDownloaded;
                                totalBytesDownloadedEver += fileBytesDownloaded;

                                updateThrottle.UpdateIfPossible(() =>
                                {
                                    var pv = new PV();

                                    if (totalBytesToDownload != null)
                                    {
                                        pv.SetTotal(totalBytesDownloaded, totalBytesToDownload.Value);
                                    }
                                    else
                                    {
                                        pv.SetTotal(totalFilesDownloaded, totalFilesToDownload);
                                    }
                                    pv.SetBytes(totalBytesDownloaded, totalBytesToDownload);
                                    progress?.Report(pv.Extract());
                                });
                            }
                        })).ConfigureAwait(false);

                    if (!f.DownloadResult.IsSuccess)
                    {
                        downloadErrorIndicatorProgress?.Report(true);

                        lock (totalsLock)
                        {
                            downloadResults.Add(f.DownloadResult);
                        }
                    }
                    else
                    {
                        if (f.ManifestEntry.Xxh3Hash != f.DownloadResult.Xxh3Hash)
                        {
                            if (f.ManifestEntry.Xxh3Hash != null)
                            {
                                L.CallerWarning($"{f.ManifestEntry.Path}: updating {nameof(f.ManifestEntry.Xxh3Hash)} in manifest");
                            }

                            f.ManifestEntry.Xxh3Hash = f.DownloadResult.Xxh3Hash;
                        }

                        lock (totalsLock)
                        {
                            ++totalFilesDownloaded;
                            downloadResults.Add(f.DownloadResult);

                            var pv = new PV();

                            pv.SetFileCount(totalFilesDownloaded, totalFilesToDownload);
                            if (totalBytesToDownload != null)
                            {
                                pv.SetTotal(totalBytesDownloaded, totalBytesToDownload.Value);
                            }
                            else
                            {
                                pv.SetTotal(totalFilesDownloaded, totalFilesToDownload);
                            }
                            pv.SetBytes(totalBytesDownloaded, totalBytesToDownload);
                            progress?.Report(pv.Extract());
                        }
                    }
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Swallow any cancellations to make sure DownloadResults get returned
            }

            return downloadResults.ToArray();
        }

        private static async Task RestoreFilesAsync(
            WorkItem[] filesToRestore,
            CancellationToken ct,
            IProgress<ProgressValues.IData>? progress)
        {
            if (!filesToRestore.Any())
            {
                // Return early not to end up with totalBytesToRestore == 0
                L.CallerWarning($"Nothing to restore.");
                return;
            }

            using var loggedRoutine = new LoggedRoutine();

            var updateThrottle = new UpdateThrottle();

            int totalFilesRestored = 0;
            int totalFilesToRestore = filesToRestore.Length;
            long totalBytesRestored = 0;
            long? totalBytesToRestore = filesToRestore.Any(f => f.ManifestEntry.Size == null) ? (long?)null :
                filesToRestore
                .Sum(f => f.ManifestEntry.Size!.Value);

            {
                var totalBytesToRestoreStr = totalBytesToRestore == null ? "?" : Formatting.FormatSizeInMiB(totalBytesToRestore.Value);

                L.CallerInformation($"Restoring {totalFilesToRestore} files ({totalBytesToRestoreStr} total)...");
            }

            progress?.Report(new PV()
                .Clear()
                .SetFileCount(totalFilesRestored, totalFilesToRestore)
                .SetBytes(totalBytesRestored, totalBytesToRestore)
                .Extract()
            );

            // Perform this stage sequentially

            foreach (var f in filesToRestore)
            {
                L.CallerDebug($"{f.InstallPath}: restoring...");

                await CopyFileAsync(f.DownloadPath, f.InstallPath, ct, new DirectProgress<Tuple<long, long, long>>(t =>
                {
                    (var fileBytesCopied, var _, var _) = t;

                    totalBytesRestored += fileBytesCopied;

                    updateThrottle.UpdateIfPossible(() =>
                    {
                        var pv = new PV();

                        if (totalBytesToRestore != null)
                        {
                            pv.SetTotal(totalBytesRestored, totalBytesToRestore.Value);
                        }
                        else
                        {
                            pv.SetTotal(totalFilesRestored, totalFilesToRestore);
                        }
                        pv.SetBytes(totalBytesRestored, totalBytesToRestore);
                        progress?.Report(pv.Extract());
                    });
                })).ConfigureAwait(false);

                ++totalFilesRestored;

                var pv = new PV();

                pv.SetFileCount(totalFilesRestored, totalFilesToRestore);
                if (totalBytesToRestore != null)
                {
                    pv.SetTotal(totalBytesRestored, totalBytesToRestore.Value);
                }
                else
                {
                    pv.SetTotal(totalFilesRestored, totalFilesToRestore);
                }
                pv.SetBytes(totalBytesRestored, totalBytesToRestore);
                progress?.Report(pv.Extract());
            }
        }

        public async Task UpdateAsync(
            bool workOffline,
            UpdateMode updateMode,
            bool useHttp2,
            FileUpdateModel fileUpdateModel,
            IProgress<ProgressValues.IData>? progress = null,
            IProgress<string>? disabledTextProgress = null,
            IProgress<bool>? offlineIndicatorProgress = null,
            IProgress<bool>? downloadErrorIndicatorProgress = null,
            CancellationToken cancellationToken = default)
        {
            using var loggedRoutine = new LoggedRoutine();

            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            using var socketsHttpHandler = new SocketsHttpHandler()
            {
                ConnectTimeout = TimeSpan.FromSeconds(3),

                // These seem to be only relevant for HTTP/2
                KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,
                KeepAlivePingDelay = TimeSpan.FromSeconds(5),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(5),
            };

            using var httpClient = new HttpClient(socketsHttpHandler)
            {
                DefaultVersionPolicy = useHttp2 ? HttpVersionPolicy.RequestVersionOrHigher : HttpVersionPolicy.RequestVersionOrLower,
                DefaultRequestVersion = useHttp2 ? new Version(2, 0) : new Version(1, 1),

#if !DEBUG
                // Slightly above the 15 sec timeout of a DNS query (https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.timeout#remarks).
                // This will only affect stalling connections in practice.
                // Any downloads interrupted due to timeout can be subsequently resumed.
                Timeout = TimeSpan.FromSeconds(20)
#else
                // Use an aggressive timeout for debugging
                Timeout = TimeSpan.FromSeconds(2)
#endif
            };

            httpClient.DefaultRequestHeaders.UserAgent.Clear();
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PD2Launcher", Constants.VersionString));

            L.CallerWrite(workOffline ? LogEventLevel.Warning : LogEventLevel.Information, $"Attempting to work {(workOffline ? "OFFLINE" : "online")}...");
            L.CallerWrite(updateMode.IsNormal() ? LogEventLevel.Information : LogEventLevel.Warning, $"Using {updateMode} {nameof(UpdateMode)}");
            L.CallerInformation($"Using HttpClient with HTTP/{httpClient.DefaultRequestVersion}");

            // Make sure remoteUrlRoot ends with a single '/' for easy concatenation
            string remoteUrlRoot = fileUpdateModel.Client.TrimEnd('/') + "/";
            // The only better alternative to simple concatenation here is a dedicated library
            string metadataUrl = remoteUrlRoot + "metadata.json";

            string installRoot = Env.GetCwd();
            string launcherFilesRoot = Env.GetLauncherFilesRootDirPath();
            string updateModelRoot = Path.Combine(launcherFilesRoot, fileUpdateModel.FilePath);
            string downloadRoot = Path.Combine(updateModelRoot, "downloads");

            string manifestPath = Path.Combine(updateModelRoot, "manifest.json");

            L.CallerInformation($"Using install path: '{installRoot}'");
            L.CallerInformation($"Using download path: '{downloadRoot}'");

            // Load current context

            if (!_fileUpdateModelToContext.TryGetValue(fileUpdateModel, value: out Context? ctx))
            {
                ctx = new();
                _fileUpdateModelToContext.Add(fileUpdateModel, ctx);
            }

            // Load local manifest if not loaded already

            // ...unless running in Reset mode
            if (updateMode.IsReset())
            {
                L.CallerWarning($"Clearing manifest due to {updateMode} {nameof(UpdateMode)}...");

                ctx.manifestEntries = Array.Empty<ManifestEntry>();
            }
            else
            {
                if (!ctx.manifestEntries.Any())
                {
                    try
                    {
                        ctx.manifestEntries = LoadManifest(manifestPath);

                        L.CallerInformation($"Loaded {ctx.manifestEntries.Length} manifest entries");
                    }
                    catch (LoadManifestException ex)
                    {
                        L.CallerError(ex.InnerException, ex.Message);
                    }
                    catch (Exception ex)
                    {
                        L.CallerError(ex.InnerException, $"{nameof(LoadManifest)}() threw");
                    }
                }
                else
                {
                    L.CallerInformation($"Manifest entries already loaded: {ctx.manifestEntries.Length}");
                }
            }

            // Retrieve remote metadata...

            bool isOffline = true;
            Exception? metadataEx = null;

            // ...unless working offline
            if (workOffline)
            {
                L.CallerWarning($"Skipping metadata download due to working OFFLINE...");
            }
            else
            {
                disabledTextProgress?.Report("Metadata...");

                ManifestEntry[] metadataEntries = Array.Empty<ManifestEntry>();

                try
                {
                    metadataEntries = await DownloadMetadata(httpClient, metadataUrl, parallelOptions.CancellationToken).ConfigureAwait(false);

                    L.CallerInformation($"Metadata entries retrieved: {metadataEntries.Length}");
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
                {
                    // Rethrow own cancellation
                    throw;
                }
                catch (LoadMetadataException ex)
                {
                    metadataEx = ex;

                    L.CallerError(ex.InnerException, ex.Message);
                }
                catch (Exception ex)
                {
                    metadataEx = ex;

                    if (ex is HttpRequestException || ex is OperationCanceledException)
                    {
                        // Treat external cancellation as a likely connection disruption
                        offlineIndicatorProgress?.Report(true);
                    }

                    L.CallerError(ex.InnerException, $"{nameof(DownloadMetadata)} threw");
                }

                if (metadataEx == null)
                {
                    isOffline = false;

                    if (!metadataEntries.Any())
                    {
                        offlineIndicatorProgress?.Report(true);

                        // This should really never happen, but if the retrieved metadata is, in fact, invalid, it's impossible to proceed.
                        throw new InvalidMetadataRetrieved();
                    }

                    // ...and merge with manifestEntries

                    int newMetadataEntries = 0;
                    int reusedMetadataEntries = 0;
                    int updatedMetadataEntries = 0;

                    // For every matching entry in manifestEntries with the same MD5, retain any additional info present in manifestEntries...
                    //
                    // Since left outer joins are only available since .NET 10, rely on GroupJoin (https://learn.microsoft.com/en-us/dotnet/csharp/linq/standard-query-operators/join-operations#emulate-a-left-outer-join)
                    foreach (var e in metadataEntries.GroupJoin(ctx.manifestEntries, meta => meta.Path, mani => mani.Path, (meta, manis) => new
                    {
                        meta,
                        // Expect manifest entries to be de-duplicated at this point, therefore the collection should either contain one element or none
                        mani = manis.FirstOrDefault()
                    }, StringComparer.OrdinalIgnoreCase))
                    {
                        // No matching manifestEntry for metadata one
                        if (e.mani == null)
                        {
                            ++newMetadataEntries;
                            continue;
                        }

                        if (e.meta.Md5Hash == e.mani.Md5Hash)
                        {
                            ++reusedMetadataEntries;
                            e.meta.Size = e.mani.Size;
                            e.meta.Xxh3Hash = e.mani.Xxh3Hash;
                            e.meta.Dirty = false;
                        }
                        else
                        {
                            ++updatedMetadataEntries;
                        }
                    }

                    // ...and eventually, replace local manifestEntries with trusted metadataEntries.
                    ctx.manifestEntries = metadataEntries;

                    L.CallerInformation($"Final manifest entries: {metadataEntries.Length} ({newMetadataEntries} new, {reusedMetadataEntries} reused, {updatedMetadataEntries} updated)");
                    LogManifestStats(ctx.manifestEntries);
                }

                // Based on successful metadata download, indicate whether we think we're offline or not

                L.CallerWrite(isOffline ? LogEventLevel.Warning : LogEventLevel.Information, $"Deemed {(isOffline ? "OFFLINE" : "online")}");
                offlineIndicatorProgress?.Report(isOffline);
            }

            if (isOffline && !ctx.manifestEntries.Any())
            {
                // Running offline with no prior manifest
                throw new OfflineInvalidManifest(metadataEx);
            }

            // Validate files according to the manifest and, if needed, determine files to restore and to download

            var filesToRestore = Array.Empty<WorkItem>();
            var filesToDownload = Array.Empty<WorkItem>();

            disabledTextProgress?.Report("Validating...");
            {
                using var loggedScope = new LoggedScope("Validating...");

                var workItems = ctx.manifestEntries
                    .Select(e => new WorkItem(
                        manifestEntry: e,
                        // Just concat as that's the most reliable approach given remoteUrlRoot has been sanitized
                        url: remoteUrlRoot + e.Path,
                        // Use Path.GetFullPath() in place of Path.Combine() since Path is expected to be relative and GetFullPath()
                        // can deal with and transform all path separators to native ones
                        downloadPath: Path.GetFullPath(e.Path, downloadRoot),
                        installPath: Path.GetFullPath(e.Path, installRoot),
                        partialDownload: ctx.partialDownloads.GetValueOrDefault(e.Path)))
                    .ToArray();

                // Verify that PartialDownloads refer to the exact files present in the manifest (in case of metadata update occurring between cancel/resume)
                foreach (var d in workItems)
                {
                    if (d.PartialDownload != null && d.PartialDownload.ReferenceMd5Hash != d.ManifestEntry.Md5Hash)
                    {
                        L.CallerWarning($"{d.ManifestEntry.Path}: partial download refers to a different version of the file. Discarding...");

                        d.PartialDownload = null;
                    }
                }

                switch (updateMode)
                {
                    case UpdateMode.Normal:
                        // (I1) FilesToRestore  = [All files] -> [InstallFiles that failed Validation]
                        {
                            await ValidateFilesAsync(workItems, ValidationKind.InstallFiles, parallelOptions, progress).ConfigureAwait(false);

                            // Restore all InstallFiles that explicitly failed validation (excluding loosely validated InstallFiles)
                            filesToRestore = workItems
                                .Where(wi => !wi.InstallFileValidated)
                                .ToArray();
                        }
                        break;

                    case UpdateMode.Restore:
                        // (I2) FilesToRestore  = [All files] -> [InstallFiles that failed Validation] + [InstallFiles loosely validated]
                        {
                            await ValidateFilesAsync(workItems, ValidationKind.InstallFiles, parallelOptions, progress).ConfigureAwait(false);

                            // InstallFiles that either failed validation or were loosely validated
                            filesToRestore = workItems
                                .Where(wi => !wi.InstallFileValidated || wi.ManifestEntry.LooseValidation)
                                .ToArray();
                        }
                        break;

                    case UpdateMode.Download:
                        // (I3) FilesToRestore  = [None]
                        {
                            L.CallerWarning($"Not restoring any files due to {updateMode} {nameof(UpdateMode)}...");
                        }
                        break;

                    case UpdateMode.Reset:
                        // (I4) FilesToRestore  = [All files]
                        {
                            L.CallerWarning($"Forcing unconditional restore of all {workItems.Length} files due to {updateMode} {nameof(UpdateMode)}...");

                            filesToRestore = workItems;
                        }
                        break;
                }

                L.CallerInformation($"Files to restore: {filesToRestore.Length}");

                switch (updateMode)
                {
                    case UpdateMode.Normal:
                    case UpdateMode.Restore:
                        // (D1) FilesToDownload = [FilesToRestore] -> [DownloadFiles that failed Validation] + [DownloadFiles with PartialDownloads]
                        {
                            await ValidateFilesAsync(filesToRestore, ValidationKind.DownloadFiles, parallelOptions, progress).ConfigureAwait(false);

                            filesToDownload = filesToRestore
                                .Where(wi => !wi.DownloadFileValidated || wi.PartialDownload != null)
                                .ToArray();
                        }
                        break;

                    case UpdateMode.Download:
                        // (D2) FilesToDownload = [All files] -> [DownloadFiles that failed Validation] + [DownloadFiles with PartialDownloads]
                        {
                            await ValidateFilesAsync(workItems, ValidationKind.DownloadFiles, parallelOptions, progress).ConfigureAwait(false);

                            filesToDownload = workItems
                                .Where(wi => !wi.DownloadFileValidated || wi.PartialDownload != null)
                                .ToArray();
                        }
                        break;

                    case UpdateMode.Reset:
                        // (D3) FilesToDownload = [All files]
                        {
                            // Validate any associated PartialDownloads
                            var partialDownloads = workItems
                                .Where(wi => wi.PartialDownload != null)
                                .ToArray();

                            L.CallerWarning($"Forcing validation of {partialDownloads.Length} {ValidationKind.DownloadFiles} with a {nameof(PartialDownload)} due to {updateMode} {nameof(UpdateMode)}...");

                            await ValidateFilesAsync(partialDownloads, ValidationKind.DownloadFiles, parallelOptions, progress).ConfigureAwait(false);

                            L.CallerWarning($"Forcing download of all {workItems.Length} {ValidationKind.DownloadFiles} due to {updateMode} {nameof(UpdateMode)}...");

                            filesToDownload = workItems;
                        }
                        break;
                }

                {
                    var partialDownloadsCount = filesToDownload.Count(f => f.PartialDownload != null);

                    L.CallerInformation($"Files to download: {filesToDownload.Length} (including {partialDownloadsCount} partial download(s))");
                }
            }

            // Attempt to save the manifest at this stage.
            //
            // This can succeed when the files were already there (and have been validated) but the manifest was missing.
            //
            // (Disallow cancelling this)
            await SaveManifest(manifestPath, ctx.manifestEntries, ct: default).ConfigureAwait(false);

            // Validation failed and some files need to be re-downloaded, which is impossible
            if (isOffline && filesToDownload.Any())
            {
                throw new OfflineNeedsDownload(metadataEx);
            }

            DownloadResult[] downloadResults = Array.Empty<DownloadResult>();

            if (!isOffline && filesToDownload.Any())
            {
                // Determine Content-Length and availability of Content-Range for any download missing Size...

                // ...or all of them in case of running in Reset mode
                if (updateMode.IsReset())
                {
                    L.CallerWarning($"Forcing query of all {filesToDownload.Length} files due to {updateMode} {nameof(UpdateMode)}...");
                }

                var filesMissingSize = updateMode.IsReset() ? filesToDownload :
                    filesToDownload
                    .Where(f => f.ManifestEntry.Size == null)
                    .ToArray();

                if (filesMissingSize.Any())
                {
                    disabledTextProgress?.Report("Querying...");

                    var initialSize = updateMode.IsReset() ? 0 :
                        filesToDownload
                        .Where(f => f.ManifestEntry.Size != null)
                        .Sum(f => f.ManifestEntry.Size!.Value);

                    await QueryDownloadsAsync(httpClient, filesMissingSize, initialSize, parallelOptions, progress).ConfigureAwait(false);

                    // Attempt to save the manifest at this stage.
                    //
                    // This should succeed as all missing Sizes should have been just retrieved.
                    //
                    // (Disallow cancelling this)
                    await SaveManifest(manifestPath, ctx.manifestEntries, ct: default).ConfigureAwait(false);
                }

                disabledTextProgress?.Report("Downloading...");

                // Prune PartialDownloads before starting downloads.
                // These should have been already assigned to their respective WorkItems before Validation.
                // Once the downloads start, these PartialDownloads are instantly inaccurate/invalid.
                ctx.partialDownloads.Clear();

                // DownloadFilesAsync() should catch most exceptions to be able to return its DownloadResults
                downloadResults = await DownloadFilesAsync(
                    httpClient,
                    filesToDownload,
                    parallelOptions,
                    progress,
                    offlineIndicatorProgress,
                    downloadErrorIndicatorProgress).ConfigureAwait(false);

                // Go over all DownloadResults and store PartialDownloads
                foreach (var f in filesToDownload)
                {
                    if (f.DownloadResult?.PartialDownload != null)
                    {
                        L.CallerInformation($"{f.ManifestEntry.Path}: storing partial download at {f.DownloadResult.PartialDownload.PartialSize} ({Formatting.FormatSizeInMiB(f.DownloadResult.PartialDownload.PartialSize)})");

                        ctx.partialDownloads.Add(f.ManifestEntry.Path, f.DownloadResult.PartialDownload);
                    }
                }

                L.CallerInformation($"Successfully downloaded {filesToDownload.Count(f => f.DownloadResult?.IsSuccess == true)}/{filesToDownload.Length} files");

                // Just throw if cancellation occurred (likely inside DownloadFilesAsync()) without going over all of DownloadResults' Exceptions.
                // Since subsequent operations will also trip up on OperationCanceledException, there's no point in trying to continue.
                parallelOptions.CancellationToken.ThrowIfCancellationRequested();
            }

            // Attempt to try to save the manifest as soon as possible after downloading.
            //
            // This should succeed in case Sizes could only be determined by downloading files in full.
            // However, if any of the files fail to download, the manifest will never get saved.
            //
            // Due to variety of possible failures in DownloadFilesAsync(), attempt to save the manifest safely, so that any failure in doing so
            // won't overshadow exceptions thrown by DownloadFilesAsync().
            //
            // (Disallow cancelling this)
            (var _, var saveManifestEx) = await TrySaveManifest(manifestPath, ctx.manifestEntries, ct: default).ConfigureAwait(false);

            if (saveManifestEx != null)
            {
                L.CallerError(saveManifestEx, $"{nameof(TrySaveManifest)}() failed.");
            }

            // Rethrow any exceptions from DownloadFilesAsync()
            if (downloadResults.Any(r => !r.IsSuccess && r.PartialDownload == null))
            {
                throw new DownloadException(new AggregateException(downloadResults
                    .Where(r => !r.IsSuccess && r.PartialDownload == null)
                    .Select(r => r.Exception!)
                    .ToArray()),
                    $"Failed downloads: {downloadResults.Count(r => !r.IsSuccess && r.PartialDownload == null)}/{downloadResults.Length}");
            }

            // Rethrow any exceptions from TrySaveManifest()
            if (saveManifestEx != null)
            {
                throw saveManifestEx;
            }

            // Restore files

            if (filesToRestore.Any())
            {
                var restorableFiles = filesToRestore
                    .Where(f => f.DownloadFileValidated || f.DownloadResult?.IsSuccess == true)
                    .ToArray();

                L.CallerInformation($"Restorable files: {restorableFiles.Length}/{filesToRestore.Length}");

                if (restorableFiles.Any())
                {
                    disabledTextProgress?.Report("Restoring...");

                    await RestoreFilesAsync(restorableFiles, parallelOptions.CancellationToken, progress).ConfigureAwait(false);
                }
            }
        }
    }
}
