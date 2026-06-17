using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using PD2Launcherv2.Enums;
using PD2Launcherv2.Helpers;
using PD2Shared.Helpers;
using PD2Shared.Interfaces;
using PD2Launcherv2.Messages;
using PD2Shared.Models;
using PD2Launcherv2.Views;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.IO;
using PD2Launcherv2.Utils;
using PD2Shared.GameFileUpdate;
using PD2Shared.Logging;
using static PD2Shared.Logging.LoggingStatic;
using PD2Shared.Utils;

namespace PD2Launcherv2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>.
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private enum KeyComboDown
        {
            Play,

            Update,
            Restore,
            Download,
            Reset
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private readonly ILocalStorage _localStorage;
        private readonly FileUpdateHelpers _fileUpdateHelpers;
        private readonly FilterHelpers _filterHelpers;
        private readonly LaunchGameHelpers _launchGameHelpers;
        private readonly NewsHelpers _newsHelpers;
        private readonly DDrawHelpers _dDrawHelpers;
        private readonly GameFileUpdater _gameFileUpdater;

        private CancellationTokenSource? _currentCts = null;
        private bool _cancellingAllowed = false;
        private bool _closePending = false;
        private bool _closePendingAllowClose = false;

        private bool _isOffline;

        private KeyComboDown _keyComboDown = KeyComboDown.Play;

        private readonly ProgressCookie _progressCookie = new();

        TextBlock? _progressTotalText = null;
        TextBlock? _progressFileCountText = null;
        TextBlock? _progressBytesText = null;
        TextBlock? _progressBytesPerSecText = null;
        int _progressBytesPrecision;

        private string _playButtonText;
        private bool _playButtonTextLocked;
        private bool _progressErrorShown;

        private readonly Brush NormalTextBrush;
        private readonly Brush ErrorTextBrush;

        private bool _isBeta;
        public bool IsBeta
        {
            get => _isBeta;
            set
            {
                if (_isBeta != value)
                {
                    _isBeta = value;
                    Debug.WriteLine($"IsBeta changing to.: {value}");
                    BetaVisibility = value ? Visibility.Visible : Visibility.Collapsed;
                    OnPropertyChanged(nameof(IsBeta));
                }
            }
        }
        private Visibility _betaVisibility = Visibility.Collapsed;
        public Visibility BetaVisibility
        {
            get => _betaVisibility;
            set
            {
                if (_betaVisibility != value)
                {
                    _betaVisibility = value;
                    OnPropertyChanged(nameof(BetaVisibility));
                }
            }
        }

        private bool _isCustom;
        public bool IsCustom
        {
            get => _isCustom;
            set
            {
                if (_isCustom != value)
                {
                    _isCustom = value;
                    Debug.WriteLine($"IsCustom changing to: {value}");
                    CustomVisibility = value ? Visibility.Visible : Visibility.Collapsed;
                    OnPropertyChanged(nameof(IsCustom));
                }
            }
        }

        private Visibility _customVisibility = Visibility.Collapsed;
        public Visibility CustomVisibility
        {
            get => _customVisibility;
            set
            {
                if (_customVisibility != value)
                {
                    _customVisibility = value;
                    OnPropertyChanged(nameof(CustomVisibility));
                }
            }
        }

        private bool _forceSoftwareRenderer;
        public bool ForceSoftwareRenderer
        {
            get => _forceSoftwareRenderer;
            set
            {
                if (_forceSoftwareRenderer != value)
                {
                    _forceSoftwareRenderer = value;
                    System.Windows.Media.RenderOptions.ProcessRenderMode = _forceSoftwareRenderer ? System.Windows.Interop.RenderMode.SoftwareOnly : System.Windows.Interop.RenderMode.Default;
                    OnPropertyChanged(nameof(ForceSoftwareRenderer));
                }
            }
        }

        private bool _useHttp2;
        public bool UseHttp2
        {
            get => _useHttp2;
            set
            {
                if (_useHttp2 != value)
                {
                    _useHttp2 = value;
                    OnPropertyChanged(nameof(UseHttp2));
                }
            }
        }

        private bool _isDisableUpdates;
        public bool IsDisableUpdates
        {
            get => _isDisableUpdates;
            set
            {
                if (_isDisableUpdates != value)
                {
                    _isDisableUpdates = value;
                    UpdatesNotificationVisibility = value ? Visibility.Visible : Visibility.Collapsed;
                    OnPropertyChanged(nameof(IsDisableUpdates));
                }
            }
        }

        private Visibility _updatesNotificationVisibility = Visibility.Collapsed;
        public Visibility UpdatesNotificationVisibility
        {
            get => _updatesNotificationVisibility;
            set
            {
                if (_updatesNotificationVisibility != value)
                {
                    _updatesNotificationVisibility = value;
                    OnPropertyChanged(nameof(UpdatesNotificationVisibility));
                }
            }
        }

        public List<NewsItem> NewsItems { get; set; }
        public ICommand OpenOptionsCommand { get; private set; }
        public ICommand OpenLootCommand { get; private set; }
        public ICommand OpenAboutCommand { get; private set; }

        public MainWindow()
        {
            InitializeComponent();

            NormalTextBrush = (Brush)FindResource("GoldLighterBrush");
            ErrorTextBrush = (Brush)FindResource("RedLighterBrush");

            OpenOptionsCommand = new RelayCommand(ShowOptionsView);
            OpenLootCommand = new RelayCommand(ShowLootView);
            OpenAboutCommand = new RelayCommand(ShowAboutView);
            _dDrawHelpers = new DDrawHelpers();
            _localStorage = (ILocalStorage)App.ServiceProvider.GetService(typeof(ILocalStorage));
            InitializeDefaultSettings(_localStorage);
            _fileUpdateHelpers = (FileUpdateHelpers)App.ServiceProvider.GetService(typeof(FileUpdateHelpers));
            _filterHelpers = (FilterHelpers)App.ServiceProvider.GetService(typeof(FilterHelpers));
            _launchGameHelpers = (LaunchGameHelpers)App.ServiceProvider.GetService(typeof(LaunchGameHelpers));
            _newsHelpers = (NewsHelpers)App.ServiceProvider.GetService(typeof(NewsHelpers));
            _gameFileUpdater = (GameFileUpdater)App.ServiceProvider.GetService(typeof(GameFileUpdater));
            LoadAndUpdateDDrawOptions();
            InitWindow();
            EnsureWindowIsVisible();
            Loaded += MainWindow_Loaded;
            LoadConfiguration();
            LoadOptions();

            // Registering to receive NavigationMessage
            Messenger.Default.Register<NavigationMessage>(this, OnNavigationMessageReceived);
            Messenger.Default.Register<ConfigurationChangeMessage>(this, OnConfigurationChanged);
            Messenger.Default.Register<LauncherOptionsChangeMessage>(this, OnLauncherOptionsChanged);
            DataContext = this;

            this.Closed += MainWindow_Closed;

            // Load or setup default file update model
            FileUpdateModel storeUpdate = _localStorage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel) ?? new FileUpdateModel
            {
                Client = "https://pd2-client-files.projectdiablo2.com/",
                FilePath = "Live"
            };

            //s11 hotfix
            if (storeUpdate.Client == "https://storage.googleapis.com/storage/v1/b/pd2-client-files/o")
            {
                storeUpdate.Client = "https://pd2-client-files.projectdiablo2.com/";
                _localStorage.Update(StorageKey.FileUpdateModel, storeUpdate);
            }

            this.Title = MsgBox.DefaultDialogTitle;
            this.VersionText.Text = PD2Shared.Constants.VersionString;
            UseFileCountProgressMapping();
            ResetUI();

            if (Wine.IsRunningUnderWine)
            {
                WineLogo16Image.ToolTip = Wine.WineVersion != null ? $"Wine {Wine.WineVersion} detected" : "Undetermined Wine version";
            }
            else
            {
                WineLogo16Image.Visibility = Visibility.Hidden;
            }

            // Don't try to update launcher in debug mode
            // TEST

#if DEBUG
            CheckForUpdates();
#else
                CheckForUpdates();
#endif
        }
        private void OnNavigationMessageReceived(NavigationMessage message)
        {
            Overlay.Visibility = Visibility.Collapsed;
            // Handle the message
            if (message.Action == NavigationAction.GoBack)
            {
                // Assuming MainFrame is your Frame control
                if (MainFrame.CanGoBack)
                {
                    MainFrame.GoBack();
                }
                else
                {
                    // If no navigation history, just clear the content of the frame,,
                    MainFrame.Content = null;
                }
            }
        }

        // trigger the update, in an async event handler
        private async void CheckForUpdates()
        {
            Debug.WriteLine("\n\n -=-=-=-=-=-=-=- CheckForUpdates() start");
            Debug.WriteLine($"☼§  IsDisableUpdates: {IsDisableUpdates}");
            var installPath = Directory.GetCurrentDirectory();
            Debug.WriteLine($"Current directory: {installPath}");
            Debug.WriteLine($"Process path: {Environment.ProcessPath}");

            UpdateUIForOperationStart(); // Prepare the UI for the update operation

            // Initialize the progress handler to update progress bar
            var progressHandler = new Progress<double>(value =>
            {
                Dispatcher.Invoke(() =>
                {
                    DownloadProgressBar.Value = value * DownloadProgressBar.Maximum;
                    if (DownloadProgressBar.Visibility != Visibility.Visible)
                    {
                        DownloadProgressBar.Visibility = Visibility.Visible;
                    }
                });
            });

            // Define the completion action to reset UI after update
            Action onDownloadComplete = ResetUI;

            // Trigger the update check and download process
            await UpdateLauncherCheck(_localStorage, progressHandler, onDownloadComplete);
            Debug.WriteLine("-=-=-=-=-=-= CheckForUpdates() End\n\n\n");
        }

        private void ClearNavigationStack()
        {
            while (MainFrame.CanGoBack)
            {
                MainFrame.RemoveBackEntry();
            }
        }

        private void BackgroundImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("PlayButton_Click start");

            L.Separator();
            L.CallerInformation($"Clicked on '{PlayButton.Text}'");

            // Store this early on to allow releasing the keys immediately upon clicking the button
            KeyComboDown keyComboDown = _keyComboDown;
            UpdateMode updateMode;
            bool noFilterUpdate;
            bool noLaunch;

            switch (keyComboDown)
            {
                case KeyComboDown.Play:
                    updateMode = UpdateMode.Normal;
                    noFilterUpdate = false;
                    noLaunch = false;
                    break;

                case KeyComboDown.Update:
                    updateMode = UpdateMode.Normal;
                    noFilterUpdate = false;
                    noLaunch = true;
                    break;

                case KeyComboDown.Restore:
                    updateMode = UpdateMode.Restore;
                    noFilterUpdate = true;
                    noLaunch = true;
                    break;

                case KeyComboDown.Download:
                    updateMode = UpdateMode.Download;
                    noFilterUpdate = true;
                    noLaunch = true;
                    break;

                case KeyComboDown.Reset:
                    updateMode = UpdateMode.Reset;
                    noFilterUpdate = true;
                    noLaunch = true;
                    break;

                // <!> Only switch expressions can benefit from "exhaustive switch"
                default:
                    throw new InvalidEnumArgumentException();
            }

            UpdateUIForOperationStart();

            try
            {
                bool proceed = false;

                {
                    Exception? caughtEx = null;
                    bool workOffline = IsDisableUpdates && !noLaunch;

                    using (_currentCts = new CancellationTokenSource())
                    {
                        try
                        {
                            CancelButton.IsEnabled = true;
                            CancelButton.Visibility = Visibility.Visible;
                            _cancellingAllowed = true;

                            L.Separator();

                            await _gameFileUpdater.UpdateAsync(
                                workOffline,
                                updateMode,
                                UseHttp2,
                                _localStorage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel),
                                new ProgressWithCookie<ProgressValues.IData>(_progressCookie, UpdateProgressValues),
                                new ProgressWithCookie<string>(_progressCookie, UpdatePlayButtonText),
                                new ProgressWithCookie<bool>(_progressCookie, ToggleProgressErrorIndicator),
                                _currentCts.Token);
                        }
                        catch (OperationCanceledException ex) when (ex.CancellationToken == _currentCts.Token)
                        {
                            // A user-requested cancellation -- just bail
                            L.CallerWarning("Canceled.");
                            return;
                        }
                        catch (DownloadException ex)
                        {
                            // These contain AggregateException and are vile to log
                            // Since all contained inner exceptions must have been logged already -- don't log them here
                            L.CallerError($"{nameof(DownloadException)} caught: '{ex.Message}'");

                            caughtEx = ex;
                        }
                        catch (FatalGameFileUpdateException ex)
                        {
                            // These will be handled below
                            L.CallerError($"{nameof(FatalGameFileUpdateException)} caught: '{ex.Message}'");

                            caughtEx = ex;
                        }
                        catch (Exception ex)
                        {
                            L.CallerError(ex, $"{nameof(GameFileUpdater.UpdateAsync)}() threw");

                            caughtEx = ex;
                        }
                        finally
                        {
                            _cancellingAllowed = false;
                            CancelButton.Visibility = Visibility.Hidden;

                            _currentCts = null;

                            if (_closePending)
                            {
                                _closePendingAllowClose = true;
                                this.Close();
                            }
                        }
                    }

                    if (caughtEx == null)
                    {
                        proceed = true;
                    }
                    else
                    {
                        void HandleFatalGameFileUpdateException(string cause, string effect)
                        {
                            const string ActionMsg = "\nRefusing to launch the game.";
                            const string OfflineActionMsg = "\nAttempt to launch the game anyway?";

                            if (noLaunch)
                            {
                                MsgBox.Exception(
                                    caughtEx.InnerException,
                                    cause);
                            }
                            else
                            {
                                if (!workOffline)
                                {
                                    MsgBox.Exception(
                                        caughtEx.InnerException,
                                        string.Join('\n', cause, effect, ActionMsg));
                                }
                                else
                                {
                                    if (MsgBox.Exception(
                                            caughtEx.InnerException,
                                            string.Join('\n', cause, effect, OfflineActionMsg),
                                            MessageBoxImage.Warning,
                                            MessageBoxButton.YesNo,
                                            MessageBoxResult.No) == MessageBoxResult.Yes)
                                    {
                                        proceed = true;
                                    }
                                }
                            }
                        }

                        if (caughtEx is OfflineInvalidManifest)
                        {
                            HandleFatalGameFileUpdateException(
                                cause: updateMode == UpdateMode.Reset ?
                                    // Manifest gets cleared during Reset
                                    "Failed to retrieve metadata." :
                                    "Failed to retrieve metadata and there is no local manifest to work with.",
                                effect: "Game files could not be validated and the integrity of the game cannot be guaranteed."
                            );
                        }
                        else if (caughtEx is InvalidMetadataRetrieved)
                        {
                            HandleFatalGameFileUpdateException(
                                cause: "Retrieved metadata is invalid.",
                                effect: "Game files could not be validated and the integrity of the game cannot be guaranteed."
                            );
                        }
                        else if (caughtEx is OfflineNeedsDownload)
                        {
                            HandleFatalGameFileUpdateException(
                                cause: "Game files failed validation and cannot be re-downloaded.",
                                effect: "The integrity of the game cannot be guaranteed."
                            );
                        }
                        else
                        {
                            MsgBox.Exception(caughtEx);
                        }
                    }
                }

                if (!proceed)
                {
                    return;
                }

                // Clear progress indicator at this point
                UpdateProgressValues(new ProgressValues().Clear().Extract());

                if (!noFilterUpdate)
                {
                    // Make this step obey IsDisableUpdates and also bail in case of _isOffline not to produce more errors
                    if (!IsDisableUpdates && !_isOffline)
                    {
                        var selectedAuthorAndFilter = _localStorage.LoadSection<SelectedAuthorAndFilter>(StorageKey.SelectedAuthorAndFilter);
                        if (selectedAuthorAndFilter?.selectedFilter != null)
                        {
                            UpdatePlayButtonText("Updating filter...");

                            try
                            {
                                await _filterHelpers.CheckAndUpdateFilterAsync(selectedAuthorAndFilter);
                            }
                            catch (Exception ex)
                            {
                                L.CallerError(ex, $"{nameof(FilterHelpers.CheckAndUpdateFilterAsync)}() threw");
                                MsgBox.Exception(ex, "Failed to update the filter:");

                                return;
                            }
                        }
                    }
                }

                if (noLaunch)
                {
                    return;
                }

                UpdatePlayButtonText("Launching...");
                try
                {
                    if (Process.GetProcessesByName("Game").Any())
                    {
                        MsgBox.Warn("Game is already running.");
                        return;
                    }

                    _launchGameHelpers.LaunchGame(_localStorage);
                }
                catch (Exception ex)
                {
                    L.CallerError(ex, $"{nameof(LaunchGameHelpers.LaunchGame)}() threw");
                    MsgBox.Exception(ex, "Failed to launch the game:");
                }
            }
            finally
            {
                // Reset UI to the default "Play" state regardless of the operation outcome
                ResetUI();
                Debug.WriteLine("PlayButton_Click end");
            }
        }

        private bool Cancel()
        {
            if (!_cancellingAllowed)
            {
                return false;
            }

            _cancellingAllowed = false;
            CancelButton.IsEnabled = false;

            L.CallerWarning("Cancellation requested!");
            _currentCts!.Cancel(throwOnFirstException: true);

            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Cancel();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_closePending)
            {
                e.Cancel = !_closePendingAllowClose;
                return;
            }

            if (this.Cancel())
            {
                e.Cancel = true;

                _closePending = true;
            }
        }

        private void CheckKeys(KeyboardDevice kd)
        {
            _keyComboDown = kd.Modifiers switch
            {
                // Pressing Alt+Space will pop up system menu. Similarly, pressing Alt alone can focus it (even with WindowStyle.None).
                // Therefore, handling Alt alone isn't great (without disabling system menu first, but that's too invasive).

                ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt => KeyComboDown.Reset,
                ModifierKeys.Control | ModifierKeys.Shift => KeyComboDown.Download,
                ModifierKeys.Shift => KeyComboDown.Restore,
                ModifierKeys.Control => KeyComboDown.Update,
                _ => KeyComboDown.Play,
            };
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            CheckKeys(e.KeyboardDevice);
            RefreshPlayButtonText();
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            CheckKeys(e.KeyboardDevice);
            RefreshPlayButtonText();
        }

        private void Window_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!IsKeyboardFocusWithin)
            {
                _keyComboDown = KeyComboDown.Play;
            }

            RefreshPlayButtonText();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            // Attempt to refocus when closing a modal dialog to get keyboard focus back
            if (!this.IsKeyboardFocusWithin)
            {
                this.Focus();
            }
        }

        private void UpdateUIForOperationStart()
        {
            Mouse.OverrideCursor = Cursors.AppStarting;

            _playButtonTextLocked = true;
            UpdatePlayButtonText("Updating...");
            PlayButton.IsEnabled = false;

            UpdateProgressValues(new ProgressValues().Clear().Extract());
            DownloadProgressBar.Visibility = Visibility.Visible;
            AboutButton.IsEnabled = false;
        }

        [MemberNotNull(nameof(_playButtonText))]
        private void ResetUI()
        {
            _progressCookie.Advance();

            AboutButton.IsEnabled = true;
            CancelButton.Visibility = Visibility.Hidden;
            CancelButton.IsEnabled = true;
            DownloadProgressBar.Visibility = Visibility.Hidden;
            UpdateProgressValues(new ProgressValues().Clear().Extract());
            ToggleProgressErrorIndicator(false);

            PlayButton.IsEnabled = true;
            _playButtonTextLocked = false;
            UpdatePlayButtonText("Play");

            Mouse.OverrideCursor = null;
        }

        private void UseFileCountProgressMapping()
        {
            UpdateProgressValues(new ProgressValues().Clear().Extract());
            _progressBytesPrecision = 2;

            _progressTotalText = null;
            _progressFileCountText = ProgressLargeText;
            _progressBytesText = ProgressSmallText1;
            _progressBytesPerSecText = ProgressSmallText2;
        }

        private void UseTotalProgressMapping()
        {
            UpdateProgressValues(new ProgressValues().Clear().Extract());
            _progressBytesPrecision = 0;

            _progressTotalText = ProgressLargeText;
            _progressFileCountText = ProgressSmallText2;
            _progressBytesText = ProgressSmallText1;
            _progressBytesPerSecText = null;
        }

        private void UpdateProgressValues(ProgressValues.IData progressData)
        {
            if (progressData.TotalSet) UpdateTotalProgress(progressData.Total);
            if (progressData.FileCountSet) UpdateFileCountProgress(progressData.FileCount);
            if (progressData.BytesSet) UpdateBytesProgress(progressData.Bytes);
            if (progressData.BytesPerSecSet) UpdateBytesPerSecProgress(progressData.BytesPerSec);
        }

        private void UpdateTotalProgress(double? progress)
        {
            if (progress == null)
            {
                DownloadProgressBar.Value = 0;
            }
            else
            {
                DownloadProgressBar.Value = progress.Value * DownloadProgressBar.Maximum;
            }

            if (_progressTotalText == null)
            {
                return;
            }

            if (progress == null)
            {
                _progressTotalText.Visibility = Visibility.Hidden;
            }
            else
            {
                _progressTotalText.Text = $"{progress * 100:N1}%";
                _progressTotalText.Visibility = Visibility.Visible;
            }
        }

        private void UpdateFileCountProgress(ProgressValues.FileCountProgress? progress)
        {
            if (_progressFileCountText == null)
            {
                return;
            }

            if (progress == null)
            {
                _progressFileCountText.Visibility = Visibility.Hidden;
                return;
            }

            _progressFileCountText.Text = $"{progress.Current:N0}/{progress.Total:N0}";
            _progressFileCountText.Visibility = Visibility.Visible;
        }

        private void UpdateBytesProgress(ProgressValues.BytesProgress? progress)
        {
            if (_progressBytesText == null)
            {
                return;
            }

            if (progress == null)
            {
                _progressBytesText.Visibility = Visibility.Hidden;
                return;
            }

            var currentStr = Formatting.FormatSizeInMiB(progress.Current, appendUnits: progress.Total == null, _progressBytesPrecision);
            var slashStr = progress.Total == null ? "" : "/";
            var totalStr = progress.Total == null ? "" : Formatting.FormatSizeInMiB(progress.Total.Value, appendUnits: true, _progressBytesPrecision);

            _progressBytesText.Text = $"{currentStr}{slashStr}{totalStr}";
            _progressBytesText.Visibility = Visibility.Visible;
        }

        private void UpdateBytesPerSecProgress(ProgressValues.BytesPerSecProgress? progress)
        {
            if (_progressBytesPerSecText == null)
            {
                return;
            }

            if (progress == null)
            {
                _progressBytesPerSecText.Visibility = Visibility.Hidden;
                return;
            }

            _progressBytesPerSecText.Text = $"({Formatting.FormatThroughputInMiB(progress.Bytes, progress.ElapsedMilliseconds)})";
            _progressBytesPerSecText.Visibility = Visibility.Visible;
        }

        [MemberNotNull(nameof(_playButtonText))]
        private void UpdatePlayButtonText(string text)
        {
            _playButtonText = text;

            RefreshPlayButtonText();
        }

        private static string GetTextForKeyComboDown(KeyComboDown keyComboDown)
        {
            switch (keyComboDown)
            {
                case KeyComboDown.Play:
                    return null!;

                case KeyComboDown.Update:
                    return "Update";
                case KeyComboDown.Restore:
                    return "Restore";
                case KeyComboDown.Download:
                    return "Download";
                case KeyComboDown.Reset:
                    return "Reset";

                // <!> Only switch expressions can benefit from "exhaustive switch"
                default:
                    throw new InvalidEnumArgumentException();
            }
        }

        private void RefreshPlayButtonText()
        {
            if (_playButtonTextLocked)
            {
                PlayButton.Text = _playButtonText;
            }
            else
            {
                PlayButton.Text = GetTextForKeyComboDown(_keyComboDown) ?? _playButtonText;
            }
        }

        private void ToggleProgressErrorIndicator(bool show)
        {
            if (_progressErrorShown == show)
            {
                return;
            }

            _progressErrorShown = show;

            var brush = show ? ErrorTextBrush : NormalTextBrush;

            ProgressLargeText.Foreground = brush;
            ProgressSmallText1.Foreground = brush;
            ProgressSmallText2.Foreground = brush;
        }

        private void onDownloadComplete()
        {
            Dispatcher.Invoke(() =>
            {
                // Actions to take when the download is complete, before resetting UI
                _launchGameHelpers.LaunchGame(_localStorage);
            });
        }

        private void OptionsButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("OptionsButton_Click start");
            ShowOptionsView();
            Debug.WriteLine("OptionsButton_Click end");
        }

        private void ShowOptionsView()
        {
            ClearNavigationStack();
            Overlay.Visibility = Visibility.Visible;
            MainFrame.Navigate(new OptionsView());
        }

        private void ShowLootView()
        {
            ClearNavigationStack();
            Overlay.Visibility = Visibility.Visible;
            MainFrame.Navigate(new FiltersView());
        }

        private void ShowAboutView()
        {
            ClearNavigationStack();
            Overlay.Visibility = Visibility.Visible;
            MainFrame.Navigate(new AboutView());
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // Use the ProcessStartInfo class to open the link in the default browser
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });

            // Prevent the default behavior of opening the link
            e.Handled = true;
        }

        private void DonateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://www.projectdiablo2.com/donate") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void TextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.Tag is string url)
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }

        private void LoadConfiguration()
        {
            var fileUpdateModel = _localStorage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel);
            IsBeta = fileUpdateModel?.FilePath == "Beta";
            IsCustom = fileUpdateModel?.FilePath == "Custom";
        }

        private void LoadOptions()
        {
            var launcherOptions = _localStorage.LoadSection<LauncherOptions>(StorageKey.LauncherOptions);
            ForceSoftwareRenderer = launcherOptions?.ForceSoftwareRenderer == true;
            UseHttp2 = launcherOptions?.UseHttp2 == true;
            IsDisableUpdates = launcherOptions?.DisableAutoUpdate == true;
        }

        private void OnConfigurationChanged(ConfigurationChangeMessage message)
        {
            IsBeta = message.IsBeta;
            OnPropertyChanged(nameof(IsBeta));
            IsCustom = message.IsCustom;
            OnPropertyChanged(nameof(IsCustom));
        }

        private void OnLauncherOptionsChanged(LauncherOptionsChangeMessage message)
        {
            ForceSoftwareRenderer = message.ForceSoftwareRenderer;
            OnPropertyChanged(nameof(ForceSoftwareRenderer));
            UseHttp2 = message.UseHttp2;
            OnPropertyChanged(nameof(UseHttp2));
            IsDisableUpdates = message.DisableAutoUpdate;
            OnPropertyChanged(nameof(IsDisableUpdates));
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            // Fetch and store the latest news from the repository
            await _newsHelpers.FetchAndStoreNewsAsync(_localStorage);
            // Fetch and store the latest reset info from the repository
            await _newsHelpers.FetchResetInfoAsync(_localStorage);

            // Load the stored news
            News theNews = _localStorage.LoadSection<News>(StorageKey.News);
            List<NewsItem> newsItems = theNews?.news ?? new List<NewsItem>();

            // Check and append reset news item if the reset time is in the future
            AppendResetNewsItemIfApplicable(newsItems);

            // Set the modified list as the item source for the UI
            NewsListBox.ItemsSource = newsItems;
        }

        private void LoadAndUpdateDDrawOptions()
        {
            // Read the current settings from ddraw.ini
            DdrawOptions currentDdrawOptions = _dDrawHelpers.ReadDdrawOptions();

            // Update the local storage with the current ddraw
            _localStorage.Update(StorageKey.DdrawOptions, currentDdrawOptions);
        }

        private void NewsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is NewsItem selectedItem)
            {
                var uri = selectedItem.Link;
                if (!string.IsNullOrWhiteSpace(uri))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        // If there's an error opening the link, show an error message
                        ShowErrorMessage($"Failed to open the link: {ex.Message}\nPlease check your internet connection or try again later.");
                        Debug.WriteLine($"Failed to open link: {ex.Message}");
                    }
                }
                // If the link is null or empty, do nothing

                ((ListBox)sender).SelectedItem = null;
            }
        }

        private void CenterWindowOnScreen()
        {
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            this.Left = (screenWidth - this.Width) / 2;
            this.Top = (screenHeight - this.Height) / 2;
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            var windowPosition = new WindowPositionModel
            {
                Left = this.Left,
                Top = this.Top,
            };

            Debug.WriteLine($"\n\n Saving window position: Left = {this.Left}, Top = {this.Top} \n\n");

            _localStorage.Update(StorageKey.WindowPosition, windowPosition);
        }

        private void EnsureWindowIsVisible()
        {
            var windowPosition = _localStorage.LoadSection<WindowPositionModel>(StorageKey.WindowPosition);

            // Check if the window is out of bounds
            bool isOutOfBounds =
                windowPosition.Left < SystemParameters.VirtualScreenLeft ||
                windowPosition.Top < SystemParameters.VirtualScreenTop ||
                windowPosition.Left > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth ||
                windowPosition.Top > SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

            if (windowPosition == null || isOutOfBounds)
            {
                CenterWindowOnScreen();
            }
            else
            {
                // Restore the window to its last saved position
                this.Left = windowPosition.Left;
                this.Top = windowPosition.Top;
            }
        }

        private void InitWindow()
        {
            var windowPosition = _localStorage.LoadSection<WindowPositionModel>(StorageKey.WindowPosition);

            Debug.WriteLine($"\n\n Loaded window position: Left = {windowPosition?.Left}, Top = {windowPosition?.Top} \n\n");

            if (windowPosition == null || (windowPosition.Left == 0 && windowPosition.Top == 0))
            {
                CenterWindowOnScreen();
            }
            else
            {
                this.Left = windowPosition.Left;
                this.Top = windowPosition.Top;
            }
        }

        private void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public void InitializeDefaultSettings(ILocalStorage localStorage)
        {
            // <!> Since LauncherOptions has been recently added, expect existing config files (aka LocalStorage)
            //     to be missing that section. Since the whole LocalStorage implementation is a bit sketchy and the below
            //     initialization doesn't even work -- perform only this specific manual step for now.
            //
            //     Keep in mind that LocalStorage.Update() will still rotate and rewrite the entire file. *sigh*
            if (_localStorage.LoadSectionIfExists<LauncherOptions>(StorageKey.LauncherOptions) == null)
            {
                bool localStorageUpdated = false;

                if (Wine.IsRunningUnderWine)
                {
                    if (MsgBox.Info(
                        "It appears to be the first time the launcher has been run.\n" +
                        "Additionally, it's running under Wine.\n" +
                        "\n" +
                        "Would you like to set up launcher options for maximum Wine compatibility?\n" +
                        "(This can be re-adjusted in the Options menu at any time).",
                        MessageBoxButton.YesNo,
                        MessageBoxResult.Yes) == MessageBoxResult.Yes)
                    {
                        localStorage.Update<LauncherOptions>(StorageKey.LauncherOptions, new LauncherOptions()
                        {
                            ForceSoftwareRenderer = true,
                            // HTTP/2 performance in Wine is currently subpar
                            UseHttp2 = false
                        });

                        localStorageUpdated = true;
                    }

                    if (MsgBox.Info(
                        "Would you like to apply PD2-specific Wine configuration?\n" +
                        "(This can be re-adjusted in the Options menu at any time).",
                        MessageBoxButton.YesNo,
                        MessageBoxResult.Yes) == MessageBoxResult.Yes)
                    {
                        try
                        {
                            Wine.ApplyWineConfiguration();
                        }
                        catch (Wine.WineException ex)
                        {
                            L.CallerError(ex.InnerException, ex.Message);
                            MsgBox.Exception(ex, "Failed to apply Wine configuration:");
                        }
                        catch (Exception ex)
                        {
                            L.CallerError(ex, "Failed to apply Wine configuration.");
                            MsgBox.Exception(ex, "Failed to apply Wine configuration:");
                        }
                    }
                }

                if (!localStorageUpdated)
                {
                    localStorage.Update<LauncherOptions>(StorageKey.LauncherOptions, new LauncherOptions());
                }
            }

            // <!> None of the below logic works as expected
            _localStorage.InitializeIfNotExists(StorageKey.FileUpdateModel, new FileUpdateModel());
            _localStorage.InitializeIfNotExists(StorageKey.DdrawOptions, new DdrawOptions());
            _localStorage.InitializeIfNotExists(StorageKey.LauncherArgs, new LauncherArgs());
            _localStorage.InitializeIfNotExists(StorageKey.LauncherOptions, new LauncherOptions());
            _localStorage.InitializeIfNotExists(StorageKey.SelectedAuthorAndFilter, new SelectedAuthorAndFilter());
            _localStorage.InitializeIfNotExists(StorageKey.Pd2AuthorList, new Pd2AuthorList());
            _localStorage.InitializeIfNotExists(StorageKey.News, new News());
            _localStorage.InitializeIfNotExists(StorageKey.WindowPosition, new WindowPositionModel());
            _localStorage.InitializeIfNotExists(StorageKey.ResetInfo, new ResetInfo());

            Debug.WriteLine("Default settings initialized if missing.");
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async Task FetchAndHandleResetInfoAsync()
        {
            await _newsHelpers.FetchResetInfoAsync(_localStorage);
        }

        private void AppendResetNewsItemIfApplicable(List<NewsItem> newsItems)
        {
            var resetInfo = _localStorage.LoadSection<ResetInfo>(StorageKey.ResetInfo);
            if (resetInfo != null && resetInfo.ResetData != null)
            {
                var resetTimeUtc = resetInfo.ResetData.ResetTime;
                // Check if the reset time is in the future
                if (resetTimeUtc > DateTime.UtcNow)
                {
                    // Convert UTC reset time to local time
                    var resetTimeLocal = resetTimeUtc.ToLocalTime();

                    // Format the local reset time
                    string formattedLocalResetTime = resetTimeLocal.ToString("MMMM dd 'at' hh:mm tt", CultureInfo.InvariantCulture);

                    // Append or insert the local reset time into the summary
                    string updatedSummary = $"{resetInfo.ResetData.ResetSummary} {formattedLocalResetTime}).";

                    var resetNewsItem = new NewsItem
                    {
                        Date = resetTimeUtc.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture),
                        Title = resetInfo.ResetData.ResetTitle,
                        Summary = updatedSummary,
                        Content = resetInfo.ResetData.ResetContent ?? "Check out the details for the upcoming season reset.",
                        Link = resetInfo.ResetData.ResetLink
                    };

                    // Prepend the reset news item to the list
                    newsItems.Insert(0, resetNewsItem);
                }
            }
        }

        public async Task UpdateLauncherCheck(ILocalStorage _localStorage, IProgress<double> progress, Action onDownloadComplete)
        {
            if (IsDisableUpdates)
            {
                onDownloadComplete?.Invoke();
                return;
            }

#if DEBUG
            string installPath = AppContext.BaseDirectory;

            Debug.WriteLine($"Launcher update directory: {installPath}");
#else
                var installPath = Directory.GetCurrentDirectory();
#endif
            Debug.WriteLine($"installPath {installPath}");
            Debug.WriteLine($"Launcher directory: {AppContext.BaseDirectory}");

            Debug.WriteLine($"Working directory: {Directory.GetCurrentDirectory()}");
            Debug.WriteLine($"Launcher update directory: {installPath}");
            Debug.WriteLine($"Running process: {Environment.ProcessPath}");


            // Initialise it as empty to satisfy dependencies
            var cloudFileItems = new List<CloudFileItem>();

            try
            {
                Debug.WriteLine(
                    $"Launcher metadata URL: " +
                    $"{PD2Shared.Constants.LauncherUpdate.MetadataUrl}");

                var updateDebugLog =
    Path.Combine(
        AppContext.BaseDirectory,
        "launcher-update-debug.log");

                File.AppendAllText(
                    updateDebugLog,
                    $"{DateTime.Now:O}{Environment.NewLine}" +
                    $"ProcessPath: {Environment.ProcessPath}{Environment.NewLine}" +
                    $"BaseDirectory: {AppContext.BaseDirectory}{Environment.NewLine}" +
                    $"WorkingDirectory: {Environment.CurrentDirectory}{Environment.NewLine}" +
                    $"MetadataUrl: {PD2Shared.Constants.LauncherUpdate.MetadataUrl}{Environment.NewLine}" +
                    Environment.NewLine);

                cloudFileItems =
                    await _fileUpdateHelpers.GetCloudFileMetadataAsync(
                        PD2Shared.Constants.LauncherUpdate.MetadataUrl);

                Debug.WriteLine(
                    $"Launcher manifest returned {cloudFileItems.Count} files.");

                if (cloudFileItems == null || cloudFileItems.Count == 0)
                {
                    onDownloadComplete?.Invoke();
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unhandled exception: {ex}");
                UpdatesNotificationVisibility = Visibility.Visible;
                onDownloadComplete?.Invoke();
                return;
            }

            var bigFour = new List<string> { "PD2Launcher.exe", "SteamPD2.exe", "UpdateUtility.exe" };
            bool big4NeedsUpdate = false;

            foreach (var fileName in bigFour)
            {
                var cloudItem = cloudFileItems.FirstOrDefault(i => i.Name == fileName);
                if (cloudItem == null)
                {
                    continue;
                }

                var localPath = Path.Combine(installPath, fileName);
                if (!File.Exists(localPath))
                {
                    big4NeedsUpdate = true;
                    break;
                }

                if (!_fileUpdateHelpers.CompareCRC(localPath, cloudItem.Crc32c))
                {
                    big4NeedsUpdate = true;
                    break;
                }
            }

            if (!big4NeedsUpdate)
            {
                //check and update all other cloud files
                foreach (var cloudItem in cloudFileItems)
                {
                    if (bigFour.Contains(cloudItem.Name)) continue;
                    if (_fileUpdateHelpers.IsFileExcluded(cloudItem.Name)) continue;

                    string localPath = Path.Combine(installPath, cloudItem.Name);
                    if (!File.Exists(localPath) || !_fileUpdateHelpers.CompareCRC(localPath, cloudItem.Crc32c))
                    {
                        bool downloaded = await _fileUpdateHelpers.PrepareLauncherUpdateAsync(cloudItem.MediaLink, localPath, progress);
                        if (!downloaded)
                        {
                            MessageBox.Show(
                                $"Failed to download {cloudItem.Name}.",
                                "Update Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);

                            onDownloadComplete?.Invoke();
                            return;
                        }
                    }
                }

                onDownloadComplete?.Invoke();
                return;
            }

            foreach (var fileName in bigFour)
            {
                var cloudItem = cloudFileItems.FirstOrDefault(i => i.Name == fileName);
                if (cloudItem == null)
                {
                    continue;
                }

                string targetName = (fileName == "UpdateUtility.exe") ? fileName : "Temp" + fileName;
                string path = Path.Combine(installPath, targetName);

                bool downloaded = await _fileUpdateHelpers.PrepareLauncherUpdateAsync(cloudItem.MediaLink, path, progress);
                if (!downloaded)
                {
                    MessageBox.Show(
                        $"Failed to download {cloudItem.Name}.",
                        "Update Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    onDownloadComplete?.Invoke();
                    return;
                }
            }

            foreach (var cloudItem in cloudFileItems)
            {
                if (bigFour.Contains(cloudItem.Name)) continue;
                if (_fileUpdateHelpers.IsFileExcluded(cloudItem.Name))
                {
                    continue;
                }

                string localPath = Path.Combine(installPath, cloudItem.Name);
                if (!File.Exists(localPath))
                {
                }
                else if (_fileUpdateHelpers.CompareCRC(localPath, cloudItem.Crc32c))
                {
                    continue;
                }
                else
                {
                }

                bool downloaded = await _fileUpdateHelpers.PrepareLauncherUpdateAsync(cloudItem.MediaLink, localPath, progress);
                if (!downloaded)
                {
                    MessageBox.Show(
                        $"Failed to download {cloudItem.Name}.",
                        "Update Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    onDownloadComplete?.Invoke();
                    return;
                }
            }

            // Wait till downloaded and flushed
            var tempFilesToCheck = new List<string> { "PD2Launcher.exe", "SteamPD2.exe" };

            foreach (var fileName in tempFilesToCheck)
            {
                string tempPath = Path.Combine(installPath, "Temp" + fileName);
                int retries = 0;
                const int maxRetries = 10;

                while ((!File.Exists(tempPath) || new FileInfo(tempPath).Length < 32768) && retries++ < maxRetries)
                {
                    await Task.Delay(300);
                }
            }

            ShowTopmostMessageBox("Launcher update is ready. The app will now close and update...", "Update Ready");
            _fileUpdateHelpers.StartUpdateProcess();

            await Task.Delay(250);
            Process.GetCurrentProcess().Kill();
        }

        public static void ShowTopmostMessageBox(string message, string title)
        {
            var topmostWindow = new Window
            {
                Width = 0,
                Height = 0,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -1000,
                Top = -1000
            };

            topmostWindow.Loaded += (s, e) =>
            {
                topmostWindow.Hide();
                MessageBox.Show(topmostWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
                topmostWindow.Close();
            };

            topmostWindow.Show();
        }
    }
}