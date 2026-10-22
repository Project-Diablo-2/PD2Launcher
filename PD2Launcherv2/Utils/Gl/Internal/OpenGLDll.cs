using System.Runtime.InteropServices;

namespace PD2Launcherv2.Utils.Gl.Internal
{
    internal static class OpenGLDll
    {
        public const string LibraryName = "opengl32.dll";

        [DllImport(LibraryName, ExactSpelling = true)]
        public static extern IntPtr wglGetCurrentContext();

        [DllImport(LibraryName, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr wglCreateContext(IntPtr hdc);

        [DllImport(LibraryName, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);

        [DllImport(LibraryName, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool wglDeleteContext(IntPtr hglrc);

        [DllImport(LibraryName, ExactSpelling = true, SetLastError = true, CharSet = CharSet.Ansi)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization",
                "CA2101:Specify marshaling for P/Invoke string arguments",
                Justification = "ANSI strings are expected, thus CharSet.Ansi is the correct choice.")]
        public static extern IntPtr wglGetProcAddress(string lpszProc);
    }
}
