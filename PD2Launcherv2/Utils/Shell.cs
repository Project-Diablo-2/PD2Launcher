using System.IO;
using System.Runtime.InteropServices;

namespace PD2Launcherv2.Utils
{
    public static class Shell
    {
        private static class DllImports
        {
            [DllImport("shell32.dll")]
            [return: MarshalAs(UnmanagedType.Error)]
            public static extern int SHOpenFolderAndSelectItems(
                IntPtr pidlFolder,
                uint cidl,
                [In, MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl,
                uint dwFlags);

            [DllImport("shell32.dll")]
            [return: MarshalAs(UnmanagedType.Error)]
            public static extern int SHParseDisplayName(
                [MarshalAs(UnmanagedType.LPWStr)] string name,
                IntPtr pBindCtx,
                [Out] out IntPtr pidl,
                uint sfgaoIn,
                [Out] out uint sfgaoOut);
        }

        public static async Task OpenFolderAndSelectItemsAsync(string dirPath, params string[] fileNames)
        {
            // No need for a full set of constants
            const int S_OK = 0;

            IntPtr dirPidl = IntPtr.Zero;
            List<IntPtr> filePidls = new(fileNames.Length);

            try
            {
                // Docs for SHParseDisplayName() suggest calling it from a separate thread (https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shparsedisplayname#remarks).
                // Additionally, all .NET threads have COM initialized with multi-threaded apartment (https://stackoverflow.com/a/70127040), so this should be safe.
                await Task<int>.Run(() =>
                {
                    int hResult;

                    hResult = DllImports.SHParseDisplayName(
                        dirPath,
                        pBindCtx: IntPtr.Zero,
                        out dirPidl,
                        sfgaoIn: 0,
                        sfgaoOut: out _);

                    if (hResult != S_OK)
                    {
                        Marshal.ThrowExceptionForHR(hResult);
                    }

                    foreach (string fileName in fileNames)
                    {
                        hResult = DllImports.SHParseDisplayName(
                            Path.Combine(dirPath, fileName),
                            pBindCtx: IntPtr.Zero,
                            out var filePidl,
                            sfgaoIn: 0,
                            sfgaoOut: out _);

                        if (hResult != S_OK)
                        {
                            Marshal.ThrowExceptionForHR(hResult);
                        }

                        filePidls.Add(filePidl);
                    }

                    if (!filePidls.Any())
                    {
                        // Make sure not to pass zero 'cidl' (item identifier list count) as that will change the behavior of SHOpenFolderAndSelectItems().
                        // Passing a NULL item identifier list in the array is harmless, however.
                        filePidls.Add(IntPtr.Zero);
                    }
                });

                {
                    IntPtr[] filePidsArray = filePidls.ToArray();

                    int hResult = DllImports.SHOpenFolderAndSelectItems(
                        dirPidl,
                        (uint)filePidsArray.Length,
                        filePidsArray,
                        dwFlags: 0);

                    if (hResult != S_OK)
                    {
                        Marshal.ThrowExceptionForHR(hResult);
                    }
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(dirPidl);

                foreach (var filePidl in filePidls)
                {
                    Marshal.FreeCoTaskMem(filePidl);
                }
            }
        }
    }
}
