using System.Text;
using System.Windows;
using PD2Launcherv2.Utils;
using PD2Launcherv2.Utils.Gl;
using PD2Shared.Logging;
using static PD2Shared.Logging.LoggingStatic;
using PD2Shared.Utils;

namespace PD2Launcherv2
{
    internal static class SanityChecks
    {
        private static bool FailurePrompt(string message)
        {
            if (MsgBox.Warn(
                message + "\n" +
                "\n" +
                "Continue regardless?",
                MessageBoxButton.YesNo,
                MessageBoxResult.No) == MessageBoxResult.No)
            {
                return false;
            }

            L.CallerWarning("User ignored sanity check failure.");
            return true;
        }

        private static bool CheckGameDirPathEncoding()
        {
            string gameDirPath = Env.GetCwd();

            var encodingAltNames = new string[] { Env.AnsiEncoding.WebName, Env.AnsiEncoding.BodyName, Env.AnsiEncoding.HeaderName }
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .Select(n => $"'{n}'");
            string encodingDisplayName = $"'{Env.AnsiEncoding.EncodingName}' (aka {string.Join(", ", encodingAltNames)})";

            string roundTrip = Env.AnsiEncoding.GetString(Encoding.Convert(Encoding.Unicode, Env.AnsiEncoding, Encoding.Unicode.GetBytes(gameDirPath)));

            if (roundTrip != gameDirPath)
            {
                L.CallerWarning($"Game directory '{gameDirPath}' cannot be represented in system-default ANSI encoding: {encodingDisplayName}.");

                if (!FailurePrompt(
                    $"Game directory '{gameDirPath}' cannot be represented in system-default ANSI encoding: {encodingDisplayName}.\n" +
                    "\n" +
                    $"Problematic characters: '{new string(gameDirPath.Except(roundTrip).Distinct().ToArray())}'.\n" +
                    "\n" +
                    "This will likely cause PD2 to crash."))
                {
                    return false;
                }
            }
            else
            {
                L.CallerInformation($"Game directory '{gameDirPath}' can be represented in system-default ANSI encoding: {encodingDisplayName}.");
            }

            return true;
        }

        private static bool CheckIfDirectoriesWritable()
        {
            {
                string dirPath = Env.ProcessDirPath;

                var ex = Env.CheckIfDirectoryIsWritable(dirPath);

                if (ex != null)
                {
                    L.CallerWarning(ex, $"Launcher directory '{dirPath}' is not writable.");

                    if (!FailurePrompt(
                        $"Launcher directory '{dirPath}' is not writable.\n" +
                        "\n" +
                        "This can lead to unexpected issues."))
                    {
                        return false;
                    }
                }
                else
                {
                    L.CallerInformation($"Launcher directory '{dirPath}' is writable.");
                }
            }

            {
                string dirPath = Env.GetCwd();

                var ex = Env.CheckIfDirectoryIsWritable(dirPath);

                if (ex != null)
                {
                    L.CallerWarning(ex, $"Launcher working directory '{dirPath}' is not writable.");

                    if (!FailurePrompt(
                        $"Launcher working directory '{dirPath}' is not writable.\n" +
                        "\n" +
                        "This can lead to unexpected issues."))
                    {
                        return false;
                    }
                }
                else
                {
                    L.CallerInformation($"Launcher working directory '{dirPath}' is writable.");
                }
            }

            return true;
        }

        private static bool TestGlContext()
        {
            using LoggedRoutine loggedRoutine = new();

            // Force creating GL contexts
            _ = GlTest.BestCtx;

            // This is too early to complain about any issues with GL.
            // Evaluate GlTest.BestCtx in light of current launcher options in MainWindow.
            return true;
        }

        public static bool Run()
        {
            List<Func<bool>> sanityChecks = new()
            {
                () => CheckGameDirPathEncoding(),
                () => CheckIfDirectoriesWritable(),
                () => TestGlContext()
            };

            using LoggedScope loggedScope = new($"Running {sanityChecks.Count} sanity check(s)...");

            for (int i = 0; i < sanityChecks.Count; ++i)
            {
                var check = sanityChecks[i];

                L.CallerInformation($"> {i + 1}/{sanityChecks.Count}");

                if (!check())
                {
                    return false;
                }
            }

            return true;
        }
    }
}
