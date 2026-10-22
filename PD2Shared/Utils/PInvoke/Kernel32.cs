using System.Runtime.InteropServices;
using System.Text;

namespace PD2Shared.Utils.PInvoke
{
    public static class Kernel32
    {
        private static class DllImports
        {
            private const string LibFileName = "kernel32.dll";

            [DllImport(LibFileName, SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr GetModuleHandle(string? lpModuleName);

            [DllImport(LibFileName, SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern uint GetModuleFileName
            (
                [In] IntPtr hModule,
                [Out] StringBuilder lpFilename,
                [In][MarshalAs(UnmanagedType.U4)] uint nSize
            );

            [DllImport(LibFileName, SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr LoadLibrary(string libFileName);

            [DllImport(LibFileName, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool FreeLibrary(IntPtr hLibModule);
        }

        public static IntPtr GetModuleHandle(string? moduleName)
        {
            return DllImports.GetModuleHandle(moduleName);
        }

        public static string? GetModuleFileName(IntPtr hModule)
        {
            var moduleNameBuilder = new StringBuilder(Win32.LongPathMaxPath);

            if (DllImports.GetModuleFileName(hModule, moduleNameBuilder, Win32.LongPathMaxPath) == 0)
            {
                return null;
            }

            return moduleNameBuilder.ToString();
        }

        public static IntPtr LoadLibrary(string libFileName)
        {
            return DllImports.LoadLibrary(libFileName);
        }

        public static bool FreeLibrary(IntPtr hLibModule)
        {
            return DllImports.FreeLibrary(hLibModule);
        }
    }
}
