using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using PD2Launcherv2.Enums;
using PD2Launcherv2.Helpers;
using PD2Launcherv2.Messages;
using PD2Shared.Helpers;
using PD2Shared.Interfaces;
using PD2Shared.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace PD2Launcherv2.ViewModels
{
    public class AboutViewModel : ViewModelBase
    {
        private readonly ILocalStorage _localStorage;
        private bool _isDevActionRunning;
        private bool _showCustomEnv;
        private string _customClientUrl = string.Empty;

        public bool ShowCustomEnv
        {
            get => _showCustomEnv;
            set
            {
                _showCustomEnv = value;
                OnPropertyChanged();
            }
        }

        public string CustomClientUrl
        {
            get => _customClientUrl;
            set
            {
                _customClientUrl = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand ProdBucket { get; }
        public RelayCommand BetaBucket { get; }
        public RelayCommand CustomBucket { get; }
        public RelayCommand ForceLauncherUpdateCommand { get; }
        public RelayCommand ForceGameFilesUpdateCommand { get; }

        public AboutViewModel(ILocalStorage localStorage)
        {
            _localStorage = localStorage;

            ProdBucket = new RelayCommand(ProdBucketAssign);
            BetaBucket = new RelayCommand(BetaBucketAssign);
            CustomBucket = new RelayCommand(CustomBucketAssign);
            ForceLauncherUpdateCommand = new RelayCommand(ForceLauncherUpdate);
            ForceGameFilesUpdateCommand = new RelayCommand(ForceGameFilesUpdate);
            CloseCommand = new RelayCommand(CloseView);
        }

        public void ProdBucketAssign()
        {
            Debug.WriteLine("\nstart ProdBucketAssign");

            var fileUpdateModel = new FileUpdateModel
            {
                Client = "https://pd2-client-files.projectdiablo2.com/",
                Launcher = "https://storage.googleapis.com/storage/v1/b/pd2-launcher-update/o",
                FilePath = "Live"
            };

            _localStorage.Update(StorageKey.FileUpdateModel, fileUpdateModel);
            SendConfigurationChange(isBeta: false, isCustom: false);

            Debug.WriteLine("end ProdBucketAssign\n");
            Messenger.Default.Send(new NavigationMessage
            {
                Action = NavigationAction.GoBack
            });
        }

        public void BetaBucketAssign()
        {
            Debug.WriteLine("\nstart BetaBucketAssign");

            var fileUpdateModel = new FileUpdateModel
            {
                Client = "https://pd2-beta-client-files.projectdiablo2.com/",
                Launcher = "https://storage.googleapis.com/storage/v1/b/pd2-launcher-update/o",
                FilePath = "Beta"
            };

            _localStorage.Update(StorageKey.FileUpdateModel, fileUpdateModel);
            SendConfigurationChange(isBeta: true, isCustom: false);

            Debug.WriteLine("end BetaBucketAssign \n");
            Messenger.Default.Send(new NavigationMessage
            {
                Action = NavigationAction.GoBack
            });
        }

        private void CustomBucketAssign()
        {
            Debug.WriteLine("\nstart SetCustomEnvironment");

            if (string.IsNullOrWhiteSpace(_customClientUrl))
            {
                return;
            }

            if (!Uri.TryCreate(
                    CustomClientUrl,
                    UriKind.Absolute,
                    out Uri validatedUri) ||
                (validatedUri.Scheme != Uri.UriSchemeHttp &&
                 validatedUri.Scheme != Uri.UriSchemeHttps))
            {
                MessageBox.Show(
                    "The data provided is not valid. Please contact the dev team for clarification.",
                    "Invalid Entry",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var fileUpdateModel = new FileUpdateModel
            {
                Client = CustomClientUrl,
                Launcher = "https://storage.googleapis.com/storage/v1/b/pd2-launcher-update/o",
                FilePath = "Custom"
            };

            _localStorage.Update(StorageKey.FileUpdateModel, fileUpdateModel);
            SendConfigurationChange(isBeta: false, isCustom: true);

            Debug.WriteLine("end SetCustomEnvironment\n");
            Messenger.Default.Send(new NavigationMessage
            {
                Action = NavigationAction.GoBack
            });
        }

        private async void ForceLauncherUpdate()
        {
            Debug.WriteLine("start ForceLauncherUpdate()");

            if (_isDevActionRunning)
            {
                return;
            }

            var confirm = MessageBox.Show(
                "Force launcher update will download launcher files now and restart the launcher. Continue?",
                "Force Launcher Update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _isDevActionRunning = true;

                var mainWindow = Application.Current.Windows
                    .OfType<PD2Launcherv2.MainWindow>()
                    .FirstOrDefault();

                if (mainWindow == null)
                {
                    MessageBox.Show(
                        "Could not find the main launcher window.",
                        "Launcher Update",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                Debug.WriteLine("start UpdateLauncherCheck(forceUpdate: true)");
                await mainWindow.UpdateLauncherCheck(
                    _localStorage,
                    new Progress<double>(),
                    () => { },
                    forceUpdate: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Force launcher update failed: {ex}");
                MessageBox.Show(
                    $"Force launcher update failed: {ex.Message}",
                    "Launcher Update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isDevActionRunning = false;
            }
        }

        private async void ForceGameFilesUpdate()
        {
            if (_isDevActionRunning)
            {
                return;
            }

            var confirm = MessageBox.Show(
                "Force game files update will re-check metadata and sync files now. Continue?",
                "Force Game Files Update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _isDevActionRunning = true;

                var gameFileUpdater =
                    App.ServiceProvider.GetService(typeof(GameFileUpdateHelpers))
                    as GameFileUpdateHelpers;

                var fileUpdateHelpers =
                    App.ServiceProvider.GetService(typeof(FileUpdateHelpers))
                    as FileUpdateHelpers;

                if (gameFileUpdater == null || fileUpdateHelpers == null)
                {
                    MessageBox.Show(
                        "Could not resolve update helpers from the service provider.",
                        "Game Files Update",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                var localMetaPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "local_metadata.json");

                if (File.Exists(localMetaPath))
                {
                    File.Delete(localMetaPath);
                }

                await gameFileUpdater.UpdateFromShaMetadataAsync(
                    _localStorage,
                    new Progress<double>(),
                    () => { });

                await fileUpdateHelpers.SyncFilesFromEnvToRoot(_localStorage);

                MessageBox.Show(
                    "Force game files update completed.",
                    "Game Files Update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Force game files update failed: {ex}");
                MessageBox.Show(
                    $"Force game files update failed: {ex.Message}",
                    "Game Files Update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isDevActionRunning = false;
            }
        }

        private void SendConfigurationChange(bool isBeta, bool isCustom)
        {
            Messenger.Default.Send(new ConfigurationChangeMessage
            {
                IsBeta = isBeta,
                IsCustom = isCustom,
                IsDisableUpdates = GetDisableAutoUpdateSetting()
            });
        }

        private bool GetDisableAutoUpdateSetting()
        {
            var launcherArgs = _localStorage.LoadSection<LauncherArgs>(
                StorageKey.LauncherArgs);

            return launcherArgs?.disableAutoUpdate == true;
        }

        private void CloseView()
        {
            Messenger.Default.Send(new NavigationMessage
            {
                Action = NavigationAction.GoBack
            });
        }
    }
}