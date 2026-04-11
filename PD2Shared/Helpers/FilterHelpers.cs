using Newtonsoft.Json;
using PD2Shared.Interfaces;
using PD2Shared.Models;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace PD2Shared.Helpers
{
    public class FilterHelpers : IFilterHelpers
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorage _localStorage;
        private const string FilterAuthorUrl = "https://raw.githubusercontent.com/Project-Diablo-2/LootFilters/main/filters.json";

        public FilterHelpers(HttpClient httpClient, ILocalStorage localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
        }

        public async Task<string> FetchFilterContentAsyncForFilterBird(
            string downloadUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching filter content: {ex.Message}");
                return string.Empty;
            }
        }

        private async Task<HttpResponseMessage> GetAsync(
            string url,
            string eTag = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrWhiteSpace(eTag))
            {
                request.Headers.IfNoneMatch.Add(
                    new EntityTagHeaderValue($"\"{eTag}\""));
                request.Headers.Add("User-Agent", "PD2Launcherv2");
            }

            return await _httpClient.SendAsync(request);
        }

        private async Task<HttpResponseMessage> GetFilterListAsync(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "PD2Launcherv2");
            return await _httpClient.SendAsync(request);
        }

        private bool IsBetaLauncher()
        {
            var fileUpdateModel = _localStorage.LoadSection<FileUpdateModel>(
                StorageKey.FileUpdateModel);

            return string.Equals(
                fileUpdateModel?.FilePath,
                "Beta",
                StringComparison.OrdinalIgnoreCase);
        }

        private async Task<FilterDefinitionsDocument?> FetchFilterDefinitionsAsync(
            string downloadUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<FilterDefinitionsDocument>(
                    content);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Error fetching filter definitions: {ex.Message}");
                return null;
            }
        }

        private List<FilterFile> BuildLegacyFilterList(List<FilterFile> filterFiles)
        {
            foreach (var file in filterFiles)
            {
                file.DisplayName = file.Name;
                file.Description = string.Empty;
                file.FilterId = string.Empty;
            }

            return filterFiles;
        }

        private async Task<List<FilterFile>> BuildFilterListAsync(
            List<FilterFile> repoFiles)
        {
            var filterFiles = repoFiles
                .Where(f => f.Name.EndsWith(
                    ".filter",
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            var legacyList = BuildLegacyFilterList(filterFiles);

            var definitionFile = repoFiles.FirstOrDefault(f =>
                f.Name.Equals(
                    "filter_definitions.json",
                    StringComparison.OrdinalIgnoreCase));

            if (definitionFile == null)
            {
                return legacyList;
            }

            var definitions = await FetchFilterDefinitionsAsync(
                definitionFile.DownloadUrl);

            if (definitions?.FilterInfo == null ||
                definitions.FilterInfo.Count == 0)
            {
                return legacyList;
            }

            bool isBeta = IsBetaLauncher();
            var mappedFilters = new List<FilterFile>();

            foreach (var pair in definitions.FilterInfo)
            {
                string id = pair.Key;
                var entry = pair.Value;
                string resolvedFileName = entry.ResolveFileName(isBeta);

                var actualFile = filterFiles.FirstOrDefault(f =>
                    f.Name.Equals(
                        resolvedFileName,
                        StringComparison.OrdinalIgnoreCase));

                if (actualFile == null)
                {
                    Debug.WriteLine(
                        $"Definition '{id}' could not find file " +
                        $"'{resolvedFileName}'.");
                    continue;
                }

                actualFile.FilterId = id;
                actualFile.DisplayName = string.IsNullOrWhiteSpace(
                    entry.DisplayName)
                    ? actualFile.Name
                    : entry.DisplayName;
                actualFile.Description = entry.Description ?? string.Empty;

                mappedFilters.Add(actualFile);
            }

            return mappedFilters.Count > 0 ? mappedFilters : legacyList;
        }

        private async Task<FilterFile?> ResolveRemoteFilterAsync(
            SelectedAuthorAndFilter selected)
        {
            var filterListResponse = await GetFilterListAsync(
                selected.selectedAuthor.Url);
            if (!filterListResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var filterListContent =
                await filterListResponse.Content.ReadAsStringAsync();
            var repoFiles = JsonConvert.DeserializeObject<List<FilterFile>>(
                                filterListContent) ?? new List<FilterFile>();

            var filters = await BuildFilterListAsync(repoFiles);

            if (!string.IsNullOrWhiteSpace(selected.selectedFilterId))
            {
                var byId = filters.FirstOrDefault(f =>
                    string.Equals(
                        f.FilterId,
                        selected.selectedFilterId,
                        StringComparison.OrdinalIgnoreCase));

                if (byId != null)
                {
                    return byId;
                }
            }

            if (selected.selectedFilter != null)
            {
                return filters.FirstOrDefault(f =>
                    string.Equals(
                        f.Name,
                        selected.selectedFilter.Name,
                        StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        public async Task<List<FilterFile>> FetchFilterContentsAsync(string url)
        {
            try
            {
                var response = await GetFilterListAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var allFiles = JsonConvert.DeserializeObject<List<FilterFile>>(
                        content) ?? new List<FilterFile>();

                    return await BuildFilterListAsync(allFiles);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching filter contents: {ex.Message}");
            }

            return null;
        }

        public async Task FetchAndStoreFilterAuthorsAsync()
        {
            Debug.WriteLine("\nstart FetchAndStoreFilterAuthorsAsync");
            try
            {
                var storedData = _localStorage.LoadSection<Pd2AuthorList>(
                    PD2Shared.Models.StorageKey.Pd2AuthorList);
                var eTag = storedData?.StorageETag ?? string.Empty;

                var response = await GetAsync(FilterAuthorUrl, eTag);

                if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
                {
                    Console.WriteLine(
                        $"Code 304? {System.Net.HttpStatusCode.NotModified}");
                    Console.WriteLine("Filter authors data not changed.");
                    return;
                }

                Debug.WriteLine(
                    $"response.IsSuccessStatusCode {response.IsSuccessStatusCode}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var authors = JsonConvert.DeserializeObject<List<FilterAuthor>>(
                        content);
                    var eTagValue = response.Headers.ETag?.Tag?.Trim('"');
                    Debug.WriteLine($"eTagValue {eTagValue}");
                    if (authors != null)
                    {
                        Pd2AuthorList eTaggedData = new()
                        {
                            StorageETag = eTagValue,
                            StorageAuthorList = authors
                        };

                        _localStorage.Update(
                            PD2Shared.Models.StorageKey.Pd2AuthorList,
                            eTaggedData);
                        Console.WriteLine("Filter authors data updated.");
                    }
                }
                else
                {
                    Console.WriteLine("Failed to fetch filter authors.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching filter authors: {ex.Message}");
            }

            Debug.WriteLine("end FetchAndStoreFilterAuthorsAsync\n");
        }

        private async Task<bool> DownloadFileAsync(
            string downloadUrl,
            string targetPath)
        {
            Debug.WriteLine("DownloadFileAsync start");
            try
            {
                var response = await _httpClient.GetAsync(
                    downloadUrl,
                    HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using (var fileStream = new FileStream(
                    targetPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                {
                    await response.Content.CopyToAsync(fileStream);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> ApplyLootFilterAsync(
            string author,
            string filterName,
            string downloadUrl,
            bool updateNeeded)
        {
            try
            {
                string installPath = Directory.GetCurrentDirectory();
                string filtersBasePath = Path.Combine(installPath, "filters");
                string localPath = Path.Combine(filtersBasePath, "local");
                string onlinePath = Path.Combine(filtersBasePath, "online");
                string defaultFilterPath = Path.Combine(installPath, "loot.filter");

                Directory.CreateDirectory(localPath);
                Directory.CreateDirectory(onlinePath);
                string targetFilterPath;
                if (author.Equals("Local Filter", StringComparison.OrdinalIgnoreCase))
                {
                    targetFilterPath = Path.Combine(localPath, filterName);
                }
                else
                {
                    targetFilterPath = Path.Combine(onlinePath, filterName);
                    if (updateNeeded || !File.Exists(targetFilterPath))
                    {
                        bool downloadSuccess = await DownloadFileAsync(
                            downloadUrl,
                            targetFilterPath);
                        if (!downloadSuccess)
                        {
                            Debug.WriteLine(
                                "Failed to download or update the filter file.");
                            return false;
                        }
                    }
                }

                File.Copy(targetFilterPath, defaultFilterPath, true);

                Debug.WriteLine("Filter applied successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying loot filter: {ex.Message}");
                return false;
            }
        }

        public bool ForceInstallLocalFilters()
        {
            var storedData = _localStorage.LoadSection<SelectedAuthorAndFilter>(
                PD2Shared.Models.StorageKey.SelectedAuthorAndFilter);
            string installPath = Directory.GetCurrentDirectory();
            string filtersBasePath = Path.Combine(installPath, "filters");
            string localPath = Path.Combine(filtersBasePath, "local");
            string defaultFilterPath = Path.Combine(installPath, "loot.filter");

            string targetFilterPath;
            if (storedData.selectedAuthor.Author.Equals(
                "Local Filter",
                StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"{storedData.selectedFilter.Name}");
                Debug.WriteLine($"{localPath}");
                targetFilterPath = Path.Combine(
                    localPath,
                    storedData.selectedFilter.Name);
                File.Copy(targetFilterPath, defaultFilterPath, true);
                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> CheckAndUpdateFilterAsync(
            SelectedAuthorAndFilter selected)
        {
            Debug.WriteLine("\n\nCheckAndUpdateFilterAsync start");
            try
            {
                if (selected.selectedAuthor.Name == "Local Filter")
                {
                    return ForceInstallLocalFilters();
                }

                var targetFilter = await ResolveRemoteFilterAsync(selected);

                if (targetFilter == null)
                {
                    Debug.WriteLine("Target filter was not found.");
                    return false;
                }

                bool updateNeeded = !string.Equals(
                    targetFilter.Sha,
                    selected.selectedFilter?.Sha,
                    StringComparison.OrdinalIgnoreCase);

                bool success;
                if (updateNeeded)
                {
                    success = await ApplyLootFilterAsync(
                        selected.selectedAuthor.Name,
                        targetFilter.Name,
                        targetFilter.DownloadUrl,
                        true);
                }
                else
                {
                    Debug.WriteLine("The filter is up-to-date.");
                    success = true;
                }

                if (success)
                {
                    selected.selectedFilter = targetFilter;

                    if (!string.IsNullOrWhiteSpace(targetFilter.FilterId))
                    {
                        selected.selectedFilterId = targetFilter.FilterId;
                    }

                    _localStorage.Update(
                        StorageKey.SelectedAuthorAndFilter,
                        selected);
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking and updating filter: {ex.Message}");
                return false;
            }
            finally
            {
                Debug.WriteLine("CheckAndUpdateFilterAsync end\n\n");
            }
        }
    }
}
