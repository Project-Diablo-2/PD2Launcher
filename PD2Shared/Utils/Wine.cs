using System.Runtime.InteropServices;
using PD2Shared.Logging;
using static PD2Shared.Logging.LoggingStatic;

namespace PD2Shared.Utils
{
    public class Wine
    {
        private static class DllImports
        {
            [DllImport("ntdll.dll", CharSet = CharSet.Ansi)]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization",
                "CA2101:Specify marshaling for P/Invoke string arguments",
                Justification = "UTF-8 strings are expected, thus CharSet.Ansi is the correct choice.")]
            public static extern string wine_get_version();
        }

        private static readonly bool _runningUnderWine = false;
        private static readonly Version? _wineVersion = null;

        static Wine()
        {
            string versionString;

            try
            {
                versionString = DllImports.wine_get_version();
            }
            catch (EntryPointNotFoundException)
            {
                return;
            }

            _runningUnderWine = true;

            if (!Version.TryParse(versionString, out _wineVersion))
            {
                L.CallerError($"Failed to parse Wine {nameof(versionString)}: '{versionString}'");
            }
        }

        public static Version? WineVersion { get => _wineVersion; }
        public static bool IsRunningUnderWine { get => _runningUnderWine; }
    }
}
