using System.Runtime.InteropServices;
using PD2Launcherv2.Utils.Gl.Internal;
using PD2Shared.Logging;
using static PD2Shared.Logging.LoggingStatic;
using PD2Shared.Utils;

namespace PD2Launcherv2.Utils.Gl
{
    public static class GlTest
    {
        // Tracks GetBestContext() progress to determine if an occurring failure deems GL driver unusable
        public enum BestContextStage
        {
            None,

            // Exceptions thrown here indicate GL failure (see: BestContextStageEx.IndicatesGlFailure())
            ForceLoadingOpenGLDll,

            WindowCreation,

            // Exceptions thrown here indicate GL failure (see: BestContextStageEx.IndicatesGlFailure())
            PixelFormatSelection,
            InitialCtxCreation,
            GotInitialCtx,
            BestCtxCreation,

            GotBestCtx,
            LeftWithInitialCtx
        }

        public class BestCtxInfo
        {
            public GlCtxInfo? GlCtxInfo { get; init; }
            public Exception? Exception { get; init; }
            public BestContextStage StageReached { get; init; }
        }

        private static BestCtxInfo? _bestContext = null;
        public static BestCtxInfo BestCtx
        {
            get
            {
                if (_bestContext == null)
                {
                    BestContextStage stageReached = BestContextStage.None;

                    try
                    {
                        _bestContext = new()
                        {
                            GlCtxInfo = GetBestContext(out stageReached),
                            Exception = null,
                            StageReached = stageReached
                        };

                        if (stageReached < BestContextStage.GotBestCtx)
                        {
                            L.CallerWarning($"{nameof(GetBestContext)}() reached '{stageReached}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        L.CallerWarning(ex, $"{nameof(GetBestContext)}() failed after reaching '{stageReached}'");

                        _bestContext = new()
                        {
                            GlCtxInfo = null,
                            Exception = ex,
                            StageReached = stageReached
                        };
                    }
                }

                return _bestContext;
            }
        }

        private static GlCtxInfo GetBestContext(out BestContextStage stage)
        {
            ushort classAtom = 0;
            IntPtr hwnd = IntPtr.Zero;
            IntPtr hdc = IntPtr.Zero;
            IntPtr hCtx = IntPtr.Zero;

            try
            {
                stage = BestContextStage.ForceLoadingOpenGLDll;

                // Force load opengl.dll to trigger any of its static initializers.
                // Otherwise, WinAPI-provided WGL might misbehave if this isn't done before setting pixel format.
                //
                // This might as well throw an EntryPointNotFoundException exception here.
                _ = OpenGLDll.wglGetCurrentContext();

                stage = BestContextStage.WindowCreation;

                // Window creation loosely based on OpenGL.Net (https://github.com/luca-piccioni/OpenGL.Net/blob/master/OpenGL.Net/DeviceContextWGL.cs#L205)

                DllImports.WNDCLASSEX windowClass = new("Hidden GL ctx window")
                {
                    style = DllImports.ClassStyles.CS_OWNDC
                };

                classAtom = DllImports.RegisterClassEx(windowClass);

                if (classAtom == 0)
                {
                    throw Win32.GetLastException(nameof(DllImports.RegisterClassEx));
                }

                hwnd = DllImports.CreateWindowEx(
                    dwExStyle: 0,
                    windowClass.lpszClassName,
                    lpWindowName: string.Empty,
                    dwStyle: 0,
                    x: 0,
                    y: 0,
                    nWidth: 0,
                    nHeight: 0,
                    hWndParent: IntPtr.Zero,
                    hMenu: IntPtr.Zero,
                    windowClass.hInstance,
                    lpParam: IntPtr.Zero
                );

                if (hwnd == IntPtr.Zero)
                {
                    throw Win32.GetLastException(nameof(DllImports.CreateWindowEx));
                }

                hdc = DllImports.GetDC(hwnd);

                if (hdc == IntPtr.Zero)
                {
                    throw new Exception($"{nameof(DllImports.GetDC)}() failed.");
                }

                stage = BestContextStage.PixelFormatSelection;

                // Pick the most sane pixel format (similar to D2GL) (https://github.com/bayaraa/d2gl/blob/master/d2gl/src/graphic/context.cpp#L37)
                DllImports.PIXELFORMATDESCRIPTOR pfd = new()
                {
                    dwFlags = 0
                        | DllImports.PfdFlags.PFD_DRAW_TO_WINDOW
                        | DllImports.PfdFlags.PFD_DOUBLEBUFFER
                        | DllImports.PfdFlags.PFD_SUPPORT_OPENGL
                        | DllImports.PfdFlags.PFD_GENERIC_ACCELERATED,
                    iPixelType = DllImports.PfdPixelType.PFD_TYPE_RGBA,
                    cColorBits = 32,
                    cDepthBits = 24,
                    cStencilBits = 8,
                    iLayerType = DllImports.PfdLayerType.PFD_MAIN_PLANE,
                };

                int pixelFormat = DllImports.ChoosePixelFormat(hdc, pfd);

                if (pixelFormat == 0)
                {
                    throw Win32.GetLastException(nameof(DllImports.ChoosePixelFormat));
                }

                if (DllImports.DescribePixelFormat(hdc, pixelFormat, pfd) == 0)
                {
                    throw Win32.GetLastException(nameof(DllImports.DescribePixelFormat));
                }

                L.CallerInformation($"Chosen pixel format: {pfd.Describe()} ({pixelFormat})");

                if (!DllImports.SetPixelFormat(hdc, pixelFormat, pfd))
                {
                    throw Win32.GetLastException(nameof(DllImports.SetPixelFormat));
                }

                stage = BestContextStage.InitialCtxCreation;

                hCtx = OpenGLDll.wglCreateContext(hdc);

                if (hCtx == IntPtr.Zero)
                {
                    throw Win32.GetLastException(nameof(OpenGLDll.wglCreateContext));
                }

                using Ctx initCtx = new(ref hCtx, hdc);

                // Preemptively load all GL functions explicitly.
                // If this fails, not even getting GlCtxInfo is possible, which would be VERY unlikely, but still...
                try
                {
                    initCtx.LoadGlFunctions();
                }
                catch (EntryPointNotFoundException)
                {
                    L.CallerError("Unable to load required GL functions for the initial context.");

                    throw;
                }

                L.CallerInformation("Initial GL context:");
                foreach (var line in initCtx.Info.ToLines())
                {
                    L.CallerInformation($"> {line}");
                }

                stage = BestContextStage.GotInitialCtx;

                // Preemptively load all WGL functions explicitly.
                // If this fails, it's impossible to create a better context and might as well bail.
                try
                {
                    initCtx.LoadWglFunctions();
                }
                catch (EntryPointNotFoundException ex)
                {
                    L.CallerError(ex, "Unable to load required WGL functions for the initial context.");
                    L.CallerWarning("No better context can be created. Using initial GL context as best context.");

                    return initCtx.Info;
                }

                if (!(initCtx.Info.Version >= new Version(3, 2) || initCtx.WglExtensions.Contains("WGL_ARB_create_context_profile")))
                {
                    L.CallerWarning("Initial GL context is neither >=3.2, nor supports WGL_ARB_create_context_profile.");
                    L.CallerWarning("No better context can be created. Using initial GL context as best context.");

                    return initCtx.Info;
                }

                stage = BestContextStage.BestCtxCreation;

                // Test creating contexts D2GL would normally request (https://github.com/bayaraa/d2gl/blob/master/d2gl/src/graphic/context.cpp#L63)
                foreach (Version version in new Version[] {
                    new(4, 6),
                    new(4, 5),
                    new(4, 4),
                    new(4, 3),
                    new(4, 2),
                    new(4, 1),
                    new(4, 0),
                    new(3, 3),
                })
                {
                    L.CallerDebug($"Attempting to create a {version} Core context...");

                    initCtx.MakeCurrent();
                    hCtx = initCtx.wglCreateContextAttribsARB(
                        hdc,
                        WglContextAttribs.WGL_CONTEXT_MAJOR_VERSION_ARB, version.Major,
                        WglContextAttribs.WGL_CONTEXT_MINOR_VERSION_ARB, version.Minor,
                        WglContextAttribs.WGL_CONTEXT_PROFILE_MASK_ARB, WglContextAttribs.WGL_CONTEXT_CORE_PROFILE_BIT_ARB,
                        // According to: https://wikis.khronos.org/opengl/OpenGL_Context#Forward_compatibility
                        // requesting forward compatibility for contexts >= 3.3 makes little sense...
                        // Still, keep this attribute for parity with D2GL.
                        WglContextAttribs.WGL_CONTEXT_FLAGS_ARB, WglContextAttribs.WGL_CONTEXT_FORWARD_COMPATIBLE_BIT_ARB
                    );

                    if (hCtx != IntPtr.Zero)
                    {
                        using Ctx bestCtx = new(ref hCtx, hdc);

                        // Preemptively load all GL functions explicitly.
                        // If this fails, GlCtxInfo won't work, so might as well ignore this context.
                        try
                        {
                            bestCtx.LoadGlFunctions();
                        }
                        catch (EntryPointNotFoundException ex)
                        {
                            L.CallerError(ex, $"Unable to load required GL functions for this {version} context. Skipping context.");
                            continue;
                        }

                        var parsedVersion = bestCtx.Info.Version;

                        if (parsedVersion == null)
                        {
                            L.CallerWarning($"Unable to determine the exact version of this {version} context:");

                            foreach (var line in bestCtx.Info.ToLines())
                            {
                                L.CallerWarning($"> {line}");
                            }
                            L.CallerWarning("Skipping context.");

                            continue;
                        }
                        else if (parsedVersion != version && parsedVersion != new Version(version.Major, version.Minor, 0))
                        {
                            L.CallerWarning($"This {version} context is in fact {parsedVersion}. Skipping context.");

                            continue;
                        }

                        stage = BestContextStage.GotBestCtx;

                        L.CallerInformation("Best GL context:");
                        foreach (var line in bestCtx.Info.ToLines())
                        {
                            L.CallerInformation($"> {line}");
                        }

                        return bestCtx.Info;
                    }
                    else
                    {
                        var lastError = Marshal.GetLastWin32Error();

                        string mainMsg = $"Failed to create {version} Core context";

                        // wglCreateContextAttribsARB() fails yet GetLastError() reports ERROR_SUCCESS. Weird, but not impossible.
                        if (lastError == Win32.ERROR_SUCCESS)
                        {
                            L.CallerWarning(mainMsg);
                        }
                        else
                        {
                            var asLastGLerror = (LastGLerror)lastError;

                            if (Enum.GetValues<LastGLerror>().Contains(asLastGLerror))
                            {
                                L.CallerWarning($"{mainMsg}: {asLastGLerror}");
                            }
                            else
                            {
                                L.CallerWarning($"{mainMsg}: {Win32.GetLastErrorMessage(lastError)}.");
                            }
                        }
                    }
                }

                // At this point, the initial context is the best we got.
                stage = BestContextStage.LeftWithInitialCtx;

                L.CallerWarning("No better context could be created. Using initial GL context as best context.");
                return initCtx.Info;
            }
            finally
            {
                if (hCtx != IntPtr.Zero)
                {
                    if (!OpenGLDll.wglDeleteContext(hCtx))
                    {
                        L.CallerError(Win32.GetLastErrorMessage(nameof(OpenGLDll.wglDeleteContext)));
                    }
                }

                if (hdc != IntPtr.Zero)
                {
                    if (!DllImports.ReleaseDC(hwnd, hdc))
                    {
                        L.CallerError($"{nameof(DllImports.ReleaseDC)}() failed.");
                    }
                }

                if (hwnd != IntPtr.Zero)
                {
                    if (!DllImports.DestroyWindow(hwnd))
                    {
                        L.CallerError(Win32.GetLastErrorMessage(nameof(DllImports.DestroyWindow)));
                    }
                }

                if (classAtom != 0)
                {
                    if (!DllImports.UnregisterClass(classAtom, DllImports.HThisInstance))
                    {
                        L.CallerError(Win32.GetLastErrorMessage(nameof(DllImports.UnregisterClass)));
                    }
                }
            }
        }
    }

    // Extension methods:

    public static class BestContextStageEx
    {
        public static bool IndicatesGlFailure(this GlTest.BestContextStage stage)
        {
            return false
                || stage == GlTest.BestContextStage.ForceLoadingOpenGLDll
                || stage == GlTest.BestContextStage.PixelFormatSelection
                || stage == GlTest.BestContextStage.InitialCtxCreation
                || stage == GlTest.BestContextStage.GotInitialCtx
                || stage == GlTest.BestContextStage.BestCtxCreation;
        }
    }
}
