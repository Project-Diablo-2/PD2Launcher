using Microsoft.Extensions.DependencyInjection;
using PD2Launcherv2.Helpers;
using PD2Shared.Helpers;
using PD2Shared.Interfaces;
using PD2Shared.Models;
using PD2Shared.Storage;
using PD2Launcherv2.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using PD2Launcherv2.Utils;
using PD2Shared.GameFileUpdate;
using PD2Shared.Logging;
using static PD2Shared.Logging.LoggingStatic;
using PD2Shared.Utils;

[assembly: System.Runtime.CompilerServices.RuntimeCompatibilityAttribute(WrapNonExceptionThrows = true)]

namespace PD2Launcherv2
{
    /// <summary>
    /// Represents the main entry point for the application, handling application startup tasks
    /// and dependency injection configuration.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Holds the service provider for dependency injection.
        /// </summary>
        private readonly IServiceProvider _serviceProvider;
        public static IServiceProvider ServiceProvider { get; private set; }
        public static T Resolve<T>() => ((App)Current)._serviceProvider.GetRequiredService<T>();

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// Configures services and builds the service provider.
        /// </summary>
        public App()
        {
            // Initializes a new instance of the service collection
            ServiceCollection services = new();
            ConfigureServices(services);
            // Builds the service provider from the service collection
            _serviceProvider = services.BuildServiceProvider();
            ServiceProvider = _serviceProvider;

            // Subscribe to unhandled exception events
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        /// <summary>
        /// Configures services for the application's dependency injection container.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        private static void ConfigureServices(ServiceCollection services)
        {
            // Registers the LocalStorage service with its interface for dependency injection.
            // This makes LocalStorage available throughout the application via DI.
            services.AddSingleton<ILocalStorage, LocalStorage>();
            services.AddSingleton<FilterHelpers>();
            services.AddSingleton<HttpClient>();
            services.AddSingleton<LaunchGameHelpers>();
            services.AddSingleton<NewsHelpers>();
            services.AddSingleton<DDrawHelpers>();
            services.AddSingleton<GameFileUpdater>();
            services.AddSingleton<FileUpdateHelpers>(provider =>
            new FileUpdateHelpers( provider.GetRequiredService<HttpClient>()));
            services.AddTransient<OptionsViewModel>();
            services.AddTransient<AboutViewModel>();
            services.AddTransient<FiltersViewModel>();
            services.AddTransient<MainWindow>();

            // Additional services and view models can be registered here as needed.
        }


        /// <summary>
        /// Overrides the <see cref="OnStartup"/> method to perform tasks when the application starts.
        /// This method is used to display the main window of the application using the services
        /// provided by dependency injection.
        /// </summary>
        /// <param name="e">Contains the arguments for the startup event.</param>
        protected override async void OnStartup(StartupEventArgs e)
        {
            CleanUpTempStorageFiles();
            var currentProcessName = Process.GetCurrentProcess().ProcessName;
            if (Process.GetProcessesByName(currentProcessName).Length > 1)
            {
                MessageBox.Show("An instance of the launcher is already running.");
                Shutdown();
                return;
            }

            if (e.Args.Any(arg => arg.Equals("--launch", StringComparison.OrdinalIgnoreCase)))
            {
                Debug.WriteLine("Steam arg Identified: Running Headless Launcher");

                var localStorage = _serviceProvider.GetRequiredService<ILocalStorage>();
                var filterHelpers = _serviceProvider.GetRequiredService<FilterHelpers>();
                var fileUpdateHelpers = _serviceProvider.GetRequiredService<FileUpdateHelpers>();
                var launchGameHelpers = _serviceProvider.GetRequiredService<LaunchGameHelpers>();

                try
                {
                    if (Process.GetProcessesByName("Game").Any())
                    {
                        Debug.WriteLine("Game already running.");
                        return;
                    }

                    var selected = localStorage.LoadSection<SelectedAuthorAndFilter>(StorageKey.SelectedAuthorAndFilter);
                    if (selected?.selectedFilter != null)
                    {
                        await filterHelpers.CheckAndUpdateFilterAsync(selected);
                    }

                    var launcherOptions = localStorage.LoadSection<LauncherOptions>(StorageKey.LauncherOptions);
                    if (!launcherOptions.DisableAutoUpdate)
                    {
                        try
                        {
                            await fileUpdateHelpers.UpdateFilesCheck(localStorage, new Progress<double>(), () => { });
                            await fileUpdateHelpers.SyncFilesFromEnvToRoot(localStorage);
                        }
                        catch (HttpRequestException ex)
                        {
                            Debug.WriteLine($"Headless update failed: {ex.Message}");
                        }
                    }

                    launchGameHelpers.LaunchGame(localStorage);
                    Shutdown();
                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Steam launch error: {ex.Message}");
                }

                return;
            }

            // This is a bit meh, but let's keep the convention and make this case-insensitive
            var createConsole = e.Args.Any(arg => arg.Equals("--console", StringComparison.OrdinalIgnoreCase));

            // This is not expected to throw
            Logging.SetUp(createConsole);

            if (Wine.IsRunningUnderWine)
            {
                if (Wine.Version != null)
                {
                    L.CallerInformation($"Running under Wine {Wine.Version}");
                }
                else
                {
                    L.CallerInformation($"Running under an undetermined Wine version");
                }
            }

            L.CallerInformation($"Using up to {Environment.ProcessorCount} concurrent task(s)");

            if (!SanityChecks.Run())
            {
                L.CallerInformation($"Sanity checks failed and user declined to continue.");

                this.Shutdown(1);
                return;
            }

            // Normal UI mode
            base.OnStartup(e);
            var mainWindow = _serviceProvider.GetService<MainWindow>();
            mainWindow?.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logging.ShutDown(e.ApplicationExitCode);

            base.OnExit(e);
        }

        // Handle non-UI thread exceptions
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Blindly cast the object to Exception thanks to RuntimeCompatibilityAttribute (https://learn.microsoft.com/en-us/dotnet/api/system.unhandledexceptioneventargs.exceptionobject#remarks)
            var ex = (Exception)e.ExceptionObject;

            L.Fatal(ex, "Unhandled exception");

            this.Dispatcher.Invoke(() =>
            {
                MsgBox.Exception(ex, "Unhandled exception:");

                // Unlike DispatcherUnhandledException, WER will still kick in.
                // However, due to reliance on task asynchronous programming model, it is very unlikely that this handler will ever be used.
                this.Shutdown(1);
            });
        }

        // Handle UI thread exceptions
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            var ex = e.Exception;

            L.Fatal(ex, "Unhandled exception");
            MsgBox.Exception(ex, "Unhandled exception:");

            e.Handled = true; // Prevent application from crashing

            // ...yet shut it down on our own terms now, knowing that we have prevented Windows Error Reporting from kicking in.
            this.Shutdown(1);
        }

        private void CleanUpTempStorageFiles()
        {
            string storageDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppData");

            if (!Directory.Exists(storageDir))
                return;

            foreach (var tmpFile in Directory.GetFiles(storageDir, "*.tmp"))
            {
                try
                {
                    File.Delete(tmpFile);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to delete temp file: {ex.Message}");
                }
            }
        }
    }
}