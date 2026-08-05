using System.Runtime.InteropServices;
using PD2Shared.Utils;

namespace PD2Launcherv2.Utils.Gl.Internal
{
    internal static class DllImports
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle([Optional] string? lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadLibrary(string lpLibFileName);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Ansi)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization",
                "CA2101:Specify marshaling for P/Invoke string arguments",
                Justification = "ANSI strings are expected, thus CharSet.Ansi is the correct choice.")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        private static IntPtr _hThisInstance = IntPtr.Zero;
        public static IntPtr HThisInstance
        {
            get
            {
                if (_hThisInstance == IntPtr.Zero)
                {
                    _hThisInstance = DllImports.GetModuleHandle(null);

                    if (_hThisInstance == IntPtr.Zero)
                    {
                        throw Win32.GetLastException(nameof(GetModuleHandle), (string?)null);
                    }
                }

                return _hThisInstance;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public class WNDCLASSEX
        {
            public WNDCLASSEX(string className)
            {
                cbSize = (uint)Marshal.SizeOf(this);
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(WindowsWndProc);
                hInstance = DllImports.HThisInstance;
                lpszClassName = className;
            }

            public uint cbSize;
            public ClassStyles style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string? lpszMenuName;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        // Class styles
        [Flags]
        public enum ClassStyles : uint
        {
#pragma warning disable format
            CS_VREDRAW         = 0x0001,
            CS_HREDRAW         = 0x0002,
            CS_DBLCLKS         = 0x0008,
            CS_OWNDC           = 0x0020,
            CS_CLASSDC         = 0x0040,
            CS_PARENTDC        = 0x0080,
            CS_NOCLOSE         = 0x0200,
            CS_SAVEBITS        = 0x0800,
            CS_BYTEALIGNCLIENT = 0x1000,
            CS_BYTEALIGNWINDOW = 0x2000,
            CS_GLOBALCLASS     = 0x4000,

            CS_IME             = 0x00010000,
            CS_DROPSHADOW      = 0x00020000,
#pragma warning restore format
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private static readonly WndProc WindowsWndProc = DefWindowProc;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern ushort RegisterClassEx([In] WNDCLASSEX lpWndClass);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterClass(ushort lpClassAtom, [Optional] IntPtr hInstance);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateWindowEx(
            // Extended window styles enum has not been extracted
            uint dwExStyle,
            [Optional] string? lpClassName,
            [Optional] string? lpWindowName,
            // Window styles enum has not been extracted
            uint dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            [Optional] IntPtr hWndParent,
            [Optional] IntPtr hMenu,
            [Optional] IntPtr hInstance,
            [Optional] IntPtr lpParam
        );

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDc);

        [StructLayout(LayoutKind.Sequential)]
        public class PIXELFORMATDESCRIPTOR
        {
            public PIXELFORMATDESCRIPTOR()
            {
                nSize = (ushort)Marshal.SizeOf(this);
                nVersion = 1;
            }

            public ushort nSize;
            public ushort nVersion;
            public PfdFlags dwFlags;
            public PfdPixelType iPixelType;
            public byte cColorBits;
            public byte cRedBits;
            public byte cRedShift;
            public byte cGreenBits;
            public byte cGreenShift;
            public byte cBlueBits;
            public byte cBlueShift;
            public byte cAlphaBits;
            public byte cAlphaShift;
            public byte cAccumBits;
            public byte cAccumRedBits;
            public byte cAccumGreenBits;
            public byte cAccumBlueBits;
            public byte cAccumAlphaBits;
            public byte cDepthBits;
            public byte cStencilBits;
            public byte cAuxBuffers;
            public PfdLayerType iLayerType;
            public byte bReserved;
            public uint dwLayerMask;
            public uint dwVisibleMask;
            public uint dwDamageMask;

            private class ChannelDescription
            {
                public char name;
                public byte size;
                public byte shift;
            }

            public string Describe()
            {
                if (iPixelType != PfdPixelType.PFD_TYPE_RGBA)
                {
                    return "?";
                }

                var channels = new ChannelDescription[]
                {
                    new() {
                        name = 'R',
                        size = cRedBits,
                        shift = cRedShift
                    },
                    new() {
                        name = 'G',
                        size = cGreenBits,
                        shift = cGreenShift
                    },
                    new() {
                        name = 'B',
                        size = cBlueBits,
                        shift = cBlueShift
                    },
                    new() {
                        name = 'A',
                        size = cAlphaBits,
                        shift = cAlphaShift
                    },
                }
                .Where(c => c.size > 0)
                .OrderBy(c => c.shift);

                string channelDesc = string.Join(string.Empty, channels.Select(c => $"{c.name}{c.size}"));
                string channelOrder = string.Join(string.Empty, channels.Select(c => c.name));

                return $"{channelDesc} ({channelOrder}) Z:{cDepthBits} Stencil:{cStencilBits}";
            }
        }

        [Flags]
        public enum PfdFlags : uint
        {
#pragma warning disable format
            PFD_DOUBLEBUFFER         = 0x00000001,
            PFD_STEREO               = 0x00000002,
            PFD_DRAW_TO_WINDOW       = 0x00000004,
            PFD_DRAW_TO_BITMAP       = 0x00000008,
            PFD_SUPPORT_GDI          = 0x00000010,
            PFD_SUPPORT_OPENGL       = 0x00000020,
            PFD_GENERIC_FORMAT       = 0x00000040,
            PFD_NEED_PALETTE         = 0x00000080,
            PFD_NEED_SYSTEM_PALETTE  = 0x00000100,
            PFD_SWAP_EXCHANGE        = 0x00000200,
            PFD_SWAP_COPY            = 0x00000400,
            PFD_SWAP_LAYER_BUFFERS   = 0x00000800,
            PFD_GENERIC_ACCELERATED  = 0x00001000,
            PFD_SUPPORT_DIRECTDRAW   = 0x00002000,
            PFD_DIRECT3D_ACCELERATED = 0x00004000,
            PFD_SUPPORT_COMPOSITION  = 0x00008000,

            // PIXELFORMATDESCRIPTOR flags for use in ChoosePixelFormat only
            PFD_DEPTH_DONTCARE        = 0x20000000,
            PFD_DOUBLEBUFFER_DONTCARE = 0x40000000,
            PFD_STEREO_DONTCARE       = 0x80000000,
#pragma warning restore format
        }

        [Flags]
        public enum PfdPixelType : byte
        {
#pragma warning disable format
            PFD_TYPE_RGBA       = 0,
            PFD_TYPE_COLORINDEX = 1,
#pragma warning restore format
        }

        [Flags]
        public enum PfdLayerType : byte
        {
#pragma warning disable format
            PFD_MAIN_PLANE     = 0,
            PFD_OVERLAY_PLANE  = 1,
            PFD_UNDERLAY_PLANE = unchecked((byte)-1),
#pragma warning restore format
        }

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern int ChoosePixelFormat(IntPtr hdc, [In] PIXELFORMATDESCRIPTOR ppfd);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern int DescribePixelFormat(IntPtr hdc, int pixelFormat, uint bytes, [In, Out] PIXELFORMATDESCRIPTOR ppfd);

        public static int DescribePixelFormat(IntPtr hdc, int pixelFormat, PIXELFORMATDESCRIPTOR ppfd)
        {
            return DescribePixelFormat(hdc, pixelFormat, (uint)Marshal.SizeOf(ppfd), ppfd);
        }

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetPixelFormat(IntPtr hdc, int pixelFormat, [In] PIXELFORMATDESCRIPTOR ppfd);
    }
}
