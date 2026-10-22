using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.Web.WebView2.Wpf;
using PD2Launcherv2.Enums;
using PD2Launcherv2.Helpers;
using PD2Shared.Helpers;
using PD2Shared.Interfaces;
using PD2Shared.Models;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;

namespace PD2Launcherv2.ViewModels
{
    public class FiltersViewModel : ViewModelBase
    {
        private readonly FilterHelpers _filterHelpers;
        private readonly ILocalStorage _localStorage;
        public RelayCommand SaveFilterCommand { get; private set; }
        public RelayCommand OpenAuthorsPageCommand { get; private set; }
        public RelayCommand OpenHelpPageCommand { get; private set; }
        public RelayCommand CopyToLocalCommand { get; private set; }
        public RelayCommand FilterPreviewICommand { get; private set; }
        public ICommand OpenFilterBirdCommand { get; }
        public ICommand CloseFilterPreviewCommand { get; }

        private WebView2 _filterWebView2;

        private bool _isWebViewVisible;
        public bool IsWebViewVisible
        {
            get => _isWebViewVisible;
            set
            {
                _isWebViewVisible = value;
                OnPropertyChanged();
            }
        }

        private List<FilterAuthor> _authorsList = new();
        public List<FilterAuthor> AuthorsList
        {
            get => _authorsList;
            set
            {
                if (_authorsList != value)
                {
                    _authorsList = value ?? new List<FilterAuthor>();
                    OnPropertyChanged();
                }
            }
        }

        private FilterAuthor? _selectedAuthor;
        public FilterAuthor? SelectedAuthor
        {
            get => _selectedAuthor;
            set
            {
                if (_selectedAuthor != value)
                {
                    _selectedAuthor = value;
                    OnPropertyChanged();

                    // Never leave filters from the previous author visible.
                    SelectedFilter = null;
                    FiltersList = new List<FilterFile>();

                    if (value != null)
                    {
                        _ = FetchDataFromAuthorUrlAsync(value);
                    }
                }
            }
        }

        private List<FilterFile> _filtersList = new();
        public List<FilterFile> FiltersList
        {
            get => _filtersList;
            set
            {
                _filtersList = value ?? new List<FilterFile>();
                OnPropertyChanged();
                SelectStoredFilter();
            }
        }

        private FilterFile? _selectedFilter;
        public FilterFile? SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (_selectedFilter != value)
                {
                    _selectedFilter = value;
                    OnPropertyChanged();
                    SaveFilterToStorage();
                }
            }
        }

        private string _changeProperty;
        public string ChangeProperty
        {
            get => _changeProperty;
            set
            {
                if (_changeProperty != value)
                {
                    _changeProperty = value;
                    OnPropertyChanged();
                }
            }
        }

        public RelayCommand AuthorCall { get; private set; }
        public RelayCommand FilterCall { get; private set; }

        public FiltersViewModel(ILocalStorage localStorage)
        {
            CloseCommand = new RelayCommand(CloseView);
            _localStorage = localStorage;
            _filterHelpers = new FilterHelpers(new HttpClient(), _localStorage);
            SaveFilterCommand = new RelayCommand(SaveFilterExecute);
            OpenAuthorsPageCommand = new RelayCommand(OpenAuthorsPageExecute);
            OpenHelpPageCommand = new RelayCommand(OpenHelpPageExecute);
            FilterPreviewICommand = new RelayCommand(OpenFilterPreviewIExecute);
            OpenFilterBirdCommand = new RelayCommand(OpenFilterBird);
            CloseFilterPreviewCommand = new RelayCommand(CloseFilterPreview);
            CopyToLocalCommand = new RelayCommand(CopyFilterToLocalExecute);
            IsWebViewVisible = false;
        }

        public async Task InitializeAsync()
        {
            Debug.WriteLine("Initializing FiltersViewModel...");
            await FetchAndStoreFilterAuthorsAsync();
            SelectStoredAuthorAndFilter();
        }

        private void CheckLocalFilterTooltip()
        {
            if (SelectedAuthor?.Name == "Local Filter")
            {
                MessageBox.Show(
                    "Note: Local filters do not receive automatic updates.",
                    "Info",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        public void SetWebView2(WebView2 webView2)
        {
            _filterWebView2 = webView2;
        }

        private async void OpenFilterBird()
        {
            if (SelectedFilter == null)
            {
                Debug.WriteLine("No filter selected.");
                return;
            }

            string remoteUrl = "https://equa1itype4ce.github.io/filterbird/";

            try
            {
                if (_filterWebView2 == null)
                {
                    Debug.WriteLine("WebView2 control is not set.");
                    return;
                }

                if (_filterWebView2.CoreWebView2 == null)
                {
                    await _filterWebView2.EnsureCoreWebView2Async(null);
                }

                var filterContent = await _filterHelpers
                    .FetchFilterContentAsyncForFilterBird(
                        SelectedFilter.DownloadUrl);

                string sanitizedFilterContent = filterContent
                    .Replace("\\", "\\\\")
                    .Replace("'", "\\'")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r");

                IsWebViewVisible = true;
                _filterWebView2.Source = new Uri(remoteUrl);

                _filterWebView2.CoreWebView2.NavigationCompleted +=
                    async (sender, args) =>
                {
                    string script =
                        $"document.getElementById('filter_text_1').innerHTML = '{sanitizedFilterContent}';";
                    await _filterWebView2.CoreWebView2.ExecuteScriptAsync(script);
                    await _filterWebView2.CoreWebView2.ExecuteScriptAsync(
                        "loadedFromApp();");
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "Error loading or interacting with the HTML file: " +
                    ex.Message);
            }
        }

        private void CloseFilterPreview()
        {
            IsWebViewVisible = false;
            _filterWebView2.CoreWebView2.NavigateToString("");
        }

        private void OpenHelpPageExecute()
        {
            OpenUrlInBrowser("https://github.com/Project-Diablo-2/LootFilters");
        }

        private void OpenFilterPreviewIExecute()
        {
            OpenUrlInBrowser("https://github.com/Equa1ityPe4ce/filterbird");
        }

        private void SelectStoredAuthorAndFilter()
        {
            var storedSelection = _localStorage.LoadSection<SelectedAuthorAndFilter>(
                StorageKey.SelectedAuthorAndFilter);
            if (storedSelection?.selectedAuthor != null)
            {
                SelectedAuthor = AuthorsList.FirstOrDefault(a =>
                    a.Author == storedSelection.selectedAuthor.Author);
            }
        }

        private void SelectStoredFilter()
        {
            if (SelectedAuthor == null || FiltersList == null)
            {
                return;
            }

            var storedSelection = _localStorage.LoadSection<SelectedAuthorAndFilter>(
                StorageKey.SelectedAuthorAndFilter);

            if (storedSelection == null)
            {
                return;
            }

            // Do not auto-select a similarly named filter from a different author.
            if (storedSelection.selectedAuthor == null ||
                !string.Equals(
                    storedSelection.selectedAuthor.Author,
                    SelectedAuthor.Author,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(storedSelection.selectedFilterId))
            {
                SelectedFilter = FiltersList.FirstOrDefault(f =>
                    string.Equals(
                        f.FilterId,
                        storedSelection.selectedFilterId,
                        StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedFilter == null && storedSelection.selectedFilter != null)
            {
                SelectedFilter = FiltersList.FirstOrDefault(f =>
                    string.Equals(
                        f.Name,
                        storedSelection.selectedFilter.Name,
                        StringComparison.OrdinalIgnoreCase));
            }
        }

        private async Task FetchDataFromAuthorUrlAsync(FilterAuthor author)
        {
            if (string.Equals(
                author.Name,
                "Local Filter",
                StringComparison.OrdinalIgnoreCase))
            {
                LoadLocalFilters();
                return;
            }

            if (string.IsNullOrWhiteSpace(author.Url) ||
                string.Equals(author.Url, "null", StringComparison.OrdinalIgnoreCase))
            {
                FiltersList = new List<FilterFile>();
                MessageBox.Show(
                    "This filter author does not have a valid repository URL.",
                    "Filter Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                var filterContents =
                    await _filterHelpers.FetchFilterContentsAsync(author.Url);

                // If the user picked another author while this request was running,
                // do not let the old request overwrite the new selection.
                if (SelectedAuthor != author)
                {
                    return;
                }

                if (filterContents == null)
                {
                    FiltersList = new List<FilterFile>();
                    MessageBox.Show(
                        "The filter list could not be loaded. This can be caused by a temporary GitHub/API failure or rate limit. Please try again.",
                        "Filter Load Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                FiltersList = filterContents;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load filters for {author.Name}: {ex.Message}");
                FiltersList = new List<FilterFile>();
                MessageBox.Show(
                    $"The filter list could not be loaded: {ex.Message}",
                    "Filter Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async Task FetchAndStoreFilterAuthorsAsync()
        {
            Debug.WriteLine("start FetchAndStoreFilterAuthorsAsync");
            await _filterHelpers.FetchAndStoreFilterAuthorsAsync();

            var storedData = _localStorage.LoadSection<Pd2AuthorList>(
                StorageKey.Pd2AuthorList);

            var updatedList = new List<FilterAuthor>
            {
                new FilterAuthor
                {
                    Name = "Local Filter",
                    Url = "local",
                    Author = "Local Filter"
                }
            };

            // The helper intentionally leaves the previous cached author list in
            // storage when the network request fails, so use it as a fallback.
            if (storedData?.StorageAuthorList != null)
            {
                updatedList.AddRange(storedData.StorageAuthorList);
            }

            AuthorsList = updatedList;
            Debug.WriteLine("end FetchAndStoreFilterAuthorsAsync");
        }

        private void LoadAuthorsFromStorage()
        {
            var storedData = _localStorage.LoadSection<Pd2AuthorList>(
                StorageKey.Pd2AuthorList);
            if (storedData?.StorageAuthorList != null)
            {
                AuthorsList = storedData.StorageAuthorList;
            }
        }

        private void LoadLocalFilters()
        {
            string rootDirectory = Directory.GetCurrentDirectory();
            var localFiltersPath = Path.Combine(rootDirectory, "filters", "local");
            Debug.WriteLine($"localFiltersPath: {localFiltersPath}");
            Debug.WriteLine($"localFiltersPath: {localFiltersPath}");
            Debug.WriteLine($"localFiltersPath: {localFiltersPath}");
            Debug.WriteLine($"localFiltersPath: {localFiltersPath}");
            if (Directory.Exists(localFiltersPath))
            {
                var filterFiles = Directory.EnumerateFiles(localFiltersPath, "*.filter")
                    .Select(path => new FilterFile
                    {
                        Name = Path.GetFileName(path),
                        DisplayName = Path.GetFileName(path),
                        Description = string.Empty,
                        Path = path
                    })
                    .ToList();

                FiltersList = filterFiles;
            }
            else
            {
                FiltersList = new List<FilterFile>();
            }
        }

        private async void CopyFilterToLocalExecute()
        {
            if (SelectedFilter == null || SelectedAuthor == null)
            {
                MessageBox.Show(
                    "Please select a filter to copy.",
                    "No Filter Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                "Applying Local Filter: Note that a local filter will not receive automatic updates unless you manually update it.\n\n" +
                "Do you want to proceed with copying this filter to local storage?",
                "Copy Filter to Local",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.OK)
            {
                try
                {
                    string localPath = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "filters",
                        "local",
                        SelectedFilter.Name);
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(localPath) ?? string.Empty);

                    var filterContent = await _filterHelpers
                        .FetchFilterContentAsyncForFilterBird(
                            SelectedFilter.DownloadUrl);

                    await File.WriteAllTextAsync(localPath, filterContent);

                    SelectedAuthor = AuthorsList.FirstOrDefault(
                        author => author.Name == "Local Filter");

                    Debug.WriteLine(
                        "Filter copied to local storage and 'Local Filter' selected.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to copy filter to local storage: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    Debug.WriteLine(
                        "Error copying filter to local storage: " + ex.Message);
                }
            }
        }

        private void SaveFilterToStorage()
        {
            if (SelectedAuthor != null && SelectedFilter != null)
            {
                var selection = new SelectedAuthorAndFilter
                {
                    selectedAuthor = SelectedAuthor,
                    selectedFilter = SelectedFilter,
                    selectedFilterId = SelectedFilter.FilterId
                };

                _localStorage.Update(StorageKey.SelectedAuthorAndFilter, selection);
            }
        }

        private void CloseView()
        {
            Messenger.Default.Send(new NavigationMessage
            {
                Action = NavigationAction.GoBack
            });
        }

        private async void SaveFilterExecute()
        {
            SaveFilterToStorage();
            if (SelectedFilter != null && SelectedAuthor != null)
            {
                bool success = await _filterHelpers.ApplyLootFilterAsync(
                    SelectedAuthor.Name,
                    SelectedFilter.Name,
                    SelectedFilter.DownloadUrl,
                    true);

                if (success)
                {
                    SaveFilterToStorage();
                    Debug.WriteLine("Filter applied");
                    Messenger.Default.Send(new NavigationMessage
                    {
                        Action = NavigationAction.GoBack
                    });
                }
                else
                {
                    Debug.WriteLine("Failed to apply filter.");
                    MessageBox.Show(
                        "The selected filter could not be downloaded or applied. Please try again.",
                        "Filter Apply Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private async void OpenAuthorsPageExecute()
        {
            if (SelectedAuthor != null && !string.IsNullOrEmpty(SelectedAuthor.Url))
            {
                string modifiedUrl = SelectedAuthor.Url
                    .Replace("api.github.com/repos", "github.com")
                    .Replace("/contents", "");

                OpenUrlInBrowser(modifiedUrl);
            }
        }

        private void OpenUrlInBrowser(string url)
        {
            if (url == "local")
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Failed to open URL: {url}. Error: {ex.Message}");
            }
        }
    }
}
