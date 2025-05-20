﻿using Microsoft.Extensions.DependencyInjection;
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
            services.AddSingleton<GameFileUpdateHelpers>();
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

                    var launcherArgs = localStorage.LoadSection<LauncherArgs>(StorageKey.LauncherArgs);
                    if (!launcherArgs.disableAutoUpdate)
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
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Steam launch error: {ex.Message}");
                }

                return;
            }

            // Normal UI mode
            base.OnStartup(e);
            var mainWindow = _serviceProvider.GetService<MainWindow>();
            mainWindow?.Show();
        }

        // Handle non-UI thread exceptions
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogException(e.ExceptionObject as Exception);
        }

        // Handle UI thread exceptions
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);
            e.Handled = true; // Prevent application from crashing
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

        private void LogException(Exception ex)
        {
            if (ex == null) return;

            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);

            string logFile = Path.Combine(logPath, $"pd2launcher_error__{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");

            // Use StackTrace to get more detailed info about where the exception occurred
            var stackTrace = new StackTrace(ex, true);
            var frame = stackTrace.GetFrames()?.FirstOrDefault();
            var method = frame?.GetMethod();
            var declaringType = method?.DeclaringType;
            var methodName = method?.Name;

            // Prepare the log entry
            var sb = new StringBuilder();
            sb.AppendLine("==============================================================================");
            sb.AppendLine($"Timestamp: {DateTime.Now}");
            sb.AppendLine("Exception Class:");
            sb.AppendLine(declaringType?.FullName ?? "N/A");
            sb.AppendLine("Exception Method:");
            sb.AppendLine($"{methodName ?? "N/A"}");
            sb.AppendLine("Exception Message:");
            sb.AppendLine(ex.Message);
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(ex.StackTrace);

            // Include inner exception details if available
            if (ex.InnerException != null)
            {
                sb.AppendLine("Inner Exception Message:");
                sb.AppendLine(ex.InnerException.Message);
                sb.AppendLine("Inner Exception Stack Trace:");
                sb.AppendLine(ex.InnerException.StackTrace);
            }
            sb.AppendLine("==============================================================================");

            // Append the log entry to the file
            File.AppendAllText(logFile, sb.ToString());
        }
    }
}