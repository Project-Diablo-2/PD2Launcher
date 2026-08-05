using System.Runtime.InteropServices;
using System.Text;
using Serilog;
using PD2Shared.Utils;
using static PD2Shared.Logging.LoggingStatic;

namespace PD2Shared.Logging
{
    public static class Logging
    {
        private static class DllImports
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool AllocConsole();

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool FreeConsole();

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetConsoleOutputCP([In] uint wCodePageID);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetConsoleTitle(string lpConsoleTitle);

            [DllImport("kernel32.dll")]
            public static extern IntPtr GetConsoleWindow();

            public enum ShowWindowCommand : int
            {
#pragma warning disable format
                SW_HIDE            = 0,
                SW_SHOWNORMAL      = 1,
                SW_NORMAL          = 1,
                SW_SHOWMINIMIZED   = 2,
                SW_SHOWMAXIMIZED   = 3,
                SW_MAXIMIZE        = 3,
                SW_SHOWNOACTIVATE  = 4,
                SW_SHOW            = 5,
                SW_MINIMIZE        = 6,
                SW_SHOWMINNOACTIVE = 7,
                SW_SHOWNA          = 8,
                SW_RESTORE         = 9,
                SW_SHOWDEFAULT     = 10,
                SW_FORCEMINIMIZE   = 11,
                SW_MAX             = 11,
#pragma warning restore format
            }

            [DllImport("user32.dll", ExactSpelling = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

            public static bool ShowWindowAsync(IntPtr hWnd, ShowWindowCommand nCmdShow)
            {
                return ShowWindowAsync(hWnd, (int)nCmdShow);
            }
        }

        private const string ConsoleOutputTemplate = "{Timestamp:HH:mm:ss.fff} {Level:u1}{Level:u1} {Message:lj}{NewLine}{Exception}";
        private const string FileOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u1}{Level:u1} {Message:lj}{NewLine}{Exception}";

        private static readonly object _logSyncObject = new();

        private static readonly string _logFileName;
        private static readonly string _logDirPath;
        private static readonly string _logPath;

        private static readonly string _previousLogFileName;
        private static readonly string _previousLogPath;

        private static bool _alreadySetUp = false;
        private static bool _consoleCreated = false;

        static Logging()
        {
            // Name the log file after the actual executable filename
            var processFileName = Path.GetFileName(Env.ProcessPath);
            var stem = Path.GetFileNameWithoutExtension(processFileName);

            _logFileName = string.Concat(stem, ".log");
            _previousLogFileName = string.Concat(stem, ".previous", ".log");

            // Since GetLauncherFilesRootDirPath() depends on GetCwd(), cache this
            _logDirPath = Env.GetLauncherFilesRootDirPath();

            _logPath = Path.Combine(Env.GetLauncherFilesRootDirPath(), _logFileName);
            _previousLogPath = Path.Combine(Env.GetLauncherFilesRootDirPath(), _previousLogFileName);
        }

        public static string LogPath { get => _logPath; }
        public static string LogDirPath { get => _logDirPath; }
        public static string LogFileName { get => _logFileName; }

        public static void SetUp(bool createConsole)
        {
#if DEBUG
            createConsole = true;
#endif

            if (_alreadySetUp)
            {
                throw new InvalidOperationException("The log has been already set up.");
            }

            if (createConsole)
            {
                if (!DllImports.AllocConsole())
                {
                    throw Win32.GetLastException(nameof(DllImports.AllocConsole));
                }

                // Force using UTF-8 as the output code page
                if (!DllImports.SetConsoleOutputCP((uint)Encoding.UTF8.CodePage))
                {
                    throw Win32.GetLastException(nameof(DllImports.SetConsoleOutputCP), Encoding.UTF8.CodePage);
                }

                const string ConsoleTitle = "Log";

                if (!DllImports.SetConsoleTitle(ConsoleTitle))
                {
                    throw Win32.GetLastException(nameof(DllImports.SetConsoleTitle), ConsoleTitle);
                }

                IntPtr consoleHwnd = DllImports.GetConsoleWindow();

                if (consoleHwnd == IntPtr.Zero)
                {
                    throw Win32.GetLastException(nameof(DllImports.GetConsoleWindow));
                }

                if (!DllImports.ShowWindowAsync(consoleHwnd, DllImports.ShowWindowCommand.SW_SHOWMAXIMIZED))
                {
                    throw Win32.GetLastException(nameof(DllImports.ShowWindowAsync), DllImports.ShowWindowCommand.SW_SHOWMAXIMIZED);
                }

                _consoleCreated = true;
            }

#if DEBUG
            // Since ILoggingFailureListener doesn't help much on sink initialization failure as of Serilog 4.3.1, resort to this:
            Serilog.Debugging.SelfLog.Enable(message =>
            {
                lock (_logSyncObject)
                {
                    Console.Error.WriteLine(message);
                }
            });
#endif

            // Rotate the log file manually, *sigh*...
            //
            // (Serilog blindly appends to an existing file and doesn't support simple file rotation unless files reach their configured limits).
            (var _, var logRotateEx) = TryRotateLogFile(_logPath, _previousLogPath);

            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Async(c => c.File(path: _logPath, outputTemplate: FileOutputTemplate, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug));

            if (createConsole)
            {
                loggerConfiguration.WriteTo.Async(c => c.Console(syncRoot: _logSyncObject, outputTemplate: ConsoleOutputTemplate));
            }

            Log.Logger = loggerConfiguration.CreateLogger();

            // <!> Should be able to tell here if the sink wasn't created successfully.
            //     Sadly, Serilog doesn't support any of that as of 4.3.1...

#if DEBUG
            // And now disable self-logging past sink creation as this excessive output won't be of much use.
            Serilog.Debugging.SelfLog.Disable();
#endif

            L.CallerInformation($"{Env.ProcessFileName} {Constants.VersionString}");
            L.CallerInformation($"Process path: '{Env.ProcessPath}'");
            L.CallerInformation($"Current working directory: '{Env.GetCwd()}'");
            L.CallerInformation($"Log file path: '{_logPath}'");

            if (logRotateEx != null)
            {
                L.CallerError(logRotateEx, $"{nameof(TryRotateLogFile)}() threw");
            }

            _alreadySetUp = true;
        }

        private static bool RotateLogFile(string logPath, string previousLogPath)
        {
            if (File.Exists(logPath))
            {
                File.Move(logPath, previousLogPath, overwrite: true);
                return true;
            }

            return false;
        }

        private static Tuple<bool?, Exception?> TryRotateLogFile(string logPath, string previousLogPath)
        {
            bool? res;

            try
            {
                res = RotateLogFile(logPath, previousLogPath);
            }
            catch (Exception ex)
            {
                return Tuple.Create((bool?)null, (Exception?)ex);
            }

            return Tuple.Create(res, (Exception?)null);
        }

        public static async void ShutDown(int exitCode)
        {
            if (!_alreadySetUp)
            {
                return;
            }

            L.CallerInformation($"Exiting with {exitCode} exit code...");
            L.CallerInformation("Closing logger...");
            await Log.CloseAndFlushAsync();

            if (_consoleCreated)
            {
                DllImports.FreeConsole();
            }
        }
    }
}
