using System.Runtime.InteropServices;
using Microsoft.Win32;
using PD2Shared.Logging;
using static PD2Shared.Logging.LoggingStatic;

namespace PD2Shared.Utils
{
    public class Wine
    {
        private static class DllImports
        {
            [DllImport("ntdll.dll", CallingConvention = CallingConvention.Cdecl)]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization",
                "CA2101:Specify marshaling for P/Invoke string arguments",
                Justification = "UTF-8 strings are expected, thus MarshalAs(UnmanagedType.LPUTF8Str) is the correct choice.")]
            [return: MarshalAs(UnmanagedType.LPUTF8Str)]
            public static extern string wine_get_version();

            [DllImport("ntdll.dll", CallingConvention = CallingConvention.Cdecl)]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization",
                "CA2101:Specify marshaling for P/Invoke string arguments",
                Justification = "UTF-8 strings are expected, thus MarshalAs(UnmanagedType.LPUTF8Str) is the correct choice.")]
            [return: MarshalAs(UnmanagedType.LPUTF8Str)]
            public static extern string wine_get_build_id();

            [DllImport("ntdll.dll", CallingConvention = CallingConvention.Cdecl)]
            public static extern void wine_get_host_version(ref IntPtr sysname, ref IntPtr release);
        }

        const string HkcuExeKeyPath = @"Software\Wine\AppDefaults\Game.exe";
        const string HkcuDllOverridesKeyPath = HkcuExeKeyPath + @"\DllOverrides";
        const string HkcuX11KeyPath = HkcuExeKeyPath + @"\X11 Driver";

        // MSVC2019 runtime
        private static readonly string[] Msvc2019Libs = {
            "api-ms-win-crt-conio-l1-1-0",
            "api-ms-win-crt-heap-l1-1-0",
            "api-ms-win-crt-locale-l1-1-0",
            "api-ms-win-crt-math-l1-1-0",
            "api-ms-win-crt-private-l1-1-0",
            "api-ms-win-crt-runtime-l1-1-0",
            "api-ms-win-crt-stdio-l1-1-0",
            "api-ms-win-crt-time-l1-1-0",
            "atl140",
            "concrt140",
            "msvcp140",
            "msvcp140_1",
            "msvcp140_2",
            "msvcp140_atomic_wait",
            "msvcp140_codecvt_ids",
            "ucrtbase",
            "vcamp140",
            "vccorlib140",
            "vcomp140",
            "vcruntime140",
            "vcruntime140_1"
        };

        public class WineException : Exception
        {
            public WineException(string? message, Exception? innerException = null) : base(message, innerException) { }
        }

        static Wine()
        {
            string? versionString;

            try
            {
                versionString = DllImports.wine_get_version();
            }
            catch (EntryPointNotFoundException)
            {
                return;
            }

            IsRunningUnderWine = true;

            if (Version.TryParse(versionString, out Version? version))
            {
                Version = version;
            }
            else
            {
                L.CallerError($"Failed to parse output of {nameof(DllImports.wine_get_version)}(): '{versionString}'");
            }

            try
            {
                BuildId = DllImports.wine_get_build_id();
            }
            catch (EntryPointNotFoundException)
            {
                L.CallerWarning($"{nameof(DllImports.wine_get_build_id)}() entry point not found");
            }

            try
            {
                IntPtr sysnamePtr = IntPtr.Zero;
                IntPtr releasePtr = IntPtr.Zero;

                DllImports.wine_get_host_version(ref sysnamePtr, ref releasePtr);

                OsName = Marshal.PtrToStringUTF8(sysnamePtr);
                OsRelease = Marshal.PtrToStringUTF8(releasePtr);
            }
            catch (EntryPointNotFoundException)
            {
                L.CallerWarning($"{nameof(DllImports.wine_get_host_version)}() entry point not found");
            }
        }

        public static bool IsRunningUnderWine { get; }
        public static Version? Version { get; }
        public static string? BuildId { get; }
        public static string? OsName { get; }
        public static string? OsRelease { get; }

        public static void ApplyWineConfiguration()
        {
            // What needs to be set:
            //
            // [HKEY_CURRENT_USER\Software\Wine\AppDefaults\Game.exe]
            // ; Workaround for InitializeCriticalSection() in Wine>=9.9 (won't help if running >=9.5, which introduced the unconditional API change)
            // "Version"="win7"
            //
            // [HKEY_CURRENT_USER\Software\Wine\AppDefaults\Game.exe\DllOverrides]
            // ; Provided DDraw wrapper
            // "ddraw"="native"
            // ; MSVC2019 runtime
            // "*api-ms-win-crt-conio-l1-1-0"="native,builtin"
            // "*api-ms-win-crt-heap-l1-1-0"="native,builtin"
            // "*api-ms-win-crt-locale-l1-1-0"="native,builtin"
            // "*api-ms-win-crt-math-l1-1-0"="native,builtin"
            // "*api-ms-win-crt-private-l1-1-0"="native,builtin"
            // "*api-ms-win-crt-runtime-l1-1-0"="native,builtin"
            // "*api-ms-win-crt-stdio-l1-1-0"="native,builtin"
            // "*api-ms-win-crt-time-l1-1-0"="native,builtin"
            // "*atl140"="native,builtin"
            // "*concrt140"="native,builtin"
            // "*msvcp140"="native,builtin"
            // "*msvcp140_1"="native,builtin"
            // "*msvcp140_2"="native,builtin"
            // "*msvcp140_atomic_wait"="native,builtin"
            // "*msvcp140_codecvt_ids"="native,builtin"
            // "*ucrtbase"="native,builtin"
            // "*vcamp140"="native,builtin"
            // "*vccorlib140"="native,builtin"
            // "*vcomp140"="native,builtin"
            // "*vcruntime140"="native,builtin"
            // "*vcruntime140_1"="native,builtin"
            //
            // [HKEY_CURRENT_USER\Software\Wine\AppDefaults\Game.exe\X11 Driver]
            // ; Emulate modesetting -- don't change the actual display resolution
            // ; Available since Wine 9.22
            // "EmulateModeset"="Y"

            using (var key = Registry.CurrentUser.CreateSubKey(HkcuExeKeyPath, writable: true))
            {
                if (key == null)
                {
                    throw new WineException($"Failed to create '{Registry.CurrentUser}{HkcuExeKeyPath}' registry key");
                }

                key.SetValue("Version", "win7", RegistryValueKind.String);
            }

            using (var key = Registry.CurrentUser.CreateSubKey(HkcuDllOverridesKeyPath, writable: true))
            {
                if (key == null)
                {
                    throw new WineException($"Failed to create '{Registry.CurrentUser}{HkcuDllOverridesKeyPath}' registry key");
                }

                // Provided DDraw wrapper
                key.SetValue("ddraw", "native", RegistryValueKind.String);

                // MSVC2019 runtime libs
                foreach (var libName in Msvc2019Libs)
                {
                    key.SetValue($"*{libName}", "native,builtin", RegistryValueKind.String);
                }
            }

            using (var key = Registry.CurrentUser.CreateSubKey(HkcuX11KeyPath, writable: true))
            {
                if (key == null)
                {
                    throw new WineException($"Failed to create '{Registry.CurrentUser}{HkcuX11KeyPath}' registry key");
                }

                // Emulate modesetting
                key.SetValue("EmulateModeset", "Y", RegistryValueKind.String);
            }
        }

        public static void RemoveWineConfiguration()
        {
            Registry.CurrentUser.DeleteSubKeyTree(HkcuExeKeyPath, throwOnMissingSubKey: false);
        }
    }
}
