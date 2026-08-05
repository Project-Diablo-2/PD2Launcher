using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using PD2Shared.Logging;
using static PD2Shared.Logging.LoggingStatic;
using PD2Shared.Utils;

namespace PD2Launcherv2.Utils.Gl.Internal
{
    internal class Ctx : IDisposable
    {
        // A regex to parse the GL_VERSION string
        // (https://wikis.khronos.org/opengl/OpenGL_Context#Context_information_queries)
        // (https://wikis.khronos.org/opengl/GLAPI/glGetString#Description)
        private static readonly Regex GlVersionRegex = new(@"^(\d+\.\d+(?:\.\d+)?)", RegexOptions.Compiled);

        private static IntPtr _hOpenGLDll = IntPtr.Zero;

        private readonly CtxDelegates _delegates = new();
        private bool _wglFunctionsLoaded = false;
        private bool _glFunctionsLoaded = false;

        private bool _disposed = false;
        private readonly IntPtr _hRenderCtx;
        private readonly IntPtr _hdc;

        public Ctx(ref IntPtr hRenderCtx, IntPtr hdc)
        {
            if (hRenderCtx == IntPtr.Zero)
            {
                throw new ArgumentException($"'{nameof(hRenderCtx)}' is invalid.", nameof(hRenderCtx));
            }

            _hdc = hdc;

            // Take ownership of the handle
            _hRenderCtx = hRenderCtx;
            hRenderCtx = IntPtr.Zero;
        }

        // Context operations

        private GlCtxInfo? _info = null;
        public GlCtxInfo Info
        {
            get
            {
                if (_info == null)
                {
                    LoadGlFunctions();

                    // Since it is unknown whether this is a >=3.0 context, don't use glGetIntegerv() with GL_MAJOR_VERSION and GL_MINOR_VERSION.
                    // Just resort to parsing GL_VERSION in a classic fashion.

                    FlushGlError();

                    string? glVersionString = glGetString(GLenum.GL_VERSION);
                    Version? version = null;

                    if (glVersionString == null)
                    {
                        L.CallerError($"{nameof(glGetString)}({GLenum.GL_VERSION}) failed with {glGetError()}.");
                    }
                    else
                    {
                        var regexMatch = GlVersionRegex.Match(glVersionString);

                        if (!regexMatch.Success || !Version.TryParse(regexMatch.ValueSpan, out version))
                        {
                            L.CallerWarning($"Failed to parse version number from GL_VERSION string: '{glVersionString}'.");
                        }
                    }

                    // Additional context information (https://wikis.khronos.org/opengl/OpenGL_Context#Context_flags)

                    bool? isForwardCompatible = null;
                    bool? isDebug = null;
                    bool? isRobust = null;
                    bool? isNoError = null;

                    bool? isCore = null;
                    bool? isCompatibility = null;

                    if (version != null)
                    {
                        if (version >= new Version(3, 0))
                        {
                            if (glGetIntegerv(GLenum.GL_CONTEXT_FLAGS, out int flags) == GLenumError.GL_NO_ERROR)
                            {
                                var flagsAsGLenum = (GLenum)flags;

                                isForwardCompatible = flagsAsGLenum.HasFlag(GLenum.GL_CONTEXT_FLAG_FORWARD_COMPATIBLE_BIT);
                                isDebug = flagsAsGLenum.HasFlag(GLenum.GL_CONTEXT_FLAG_DEBUG_BIT);
                                isRobust = flagsAsGLenum.HasFlag(GLenum.GL_CONTEXT_FLAG_ROBUST_ACCESS_BIT);
                                isNoError = flagsAsGLenum.HasFlag(GLenum.GL_CONTEXT_FLAG_NO_ERROR_BIT);
                            }
                            else
                            {
                                L.CallerError($"{nameof(glGetIntegerv)}({GLenum.GL_CONTEXT_FLAGS}) failed.");
                            }
                        }

                        if (version >= new Version(3, 2))
                        {
                            if (glGetIntegerv(GLenum.GL_CONTEXT_PROFILE_MASK, out int mask) == GLenumError.GL_NO_ERROR)
                            {
                                var maskAsGLenum = (GLenum)mask;

                                isCore = maskAsGLenum.HasFlag(GLenum.GL_CONTEXT_CORE_PROFILE_BIT);
                                isCompatibility = maskAsGLenum.HasFlag(GLenum.GL_CONTEXT_COMPATIBILITY_PROFILE_BIT);
                            }
                            else
                            {
                                L.CallerError($"{nameof(glGetIntegerv)}({GLenum.GL_CONTEXT_PROFILE_MASK}) failed.");
                            }
                        }
                    }

                    string glGetStringOrDefault(GLenum name)
                    {
                        string? res = glGetString(name);

                        if (res == null)
                        {
                            L.CallerError($"{nameof(glGetString)}({name}) failed with {glGetError()}.");

                            return string.Empty;
                        }

                        return res;
                    }

                    _info = new GlCtxInfo(
                        version: version,
                        glVendor: glGetStringOrDefault(GLenum.GL_VENDOR),
                        glRenderer: glGetStringOrDefault(GLenum.GL_RENDERER),
                        glVersion: glVersionString ?? string.Empty,
                        glShadingLanguageVersion: glGetStringOrDefault(GLenum.GL_SHADING_LANGUAGE_VERSION),

                        isForwardCompatible: isForwardCompatible,
                        isDebug: isDebug,
                        isRobust: isRobust,
                        isNoError: isNoError,

                        isCore: isCore,
                        isCompatibility: isCompatibility
                    );
                }

                return _info;
            }
        }

        public void MakeCurrent()
        {
            if (!OpenGLDll.wglMakeCurrent(_hdc, _hRenderCtx))
            {
                throw Win32.GetLastException(nameof(OpenGLDll.wglMakeCurrent));
            }
        }

        // Function/extension loading

        private void LoadFunctions(Func<FieldInfo, bool> predicate)
        {
            // A sane approach to this would be to use an established utility library.
            //
            // However, given how narrow the scope is, (with no need to specify/resolve extension and GL version relationships
            // except for WGL_ARB_create_context_profile which doesn't even specify additional entry points) -- attempt to
            // load this handful of functions manually.

            MakeCurrent();

            SortedSet<string> failedFunctions = new();

            foreach (var f in _delegates.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.FieldType.IsSubclassOf(typeof(Delegate)))
                .Where(f => predicate(f))
            )
            {
                // Strip the leading '_'
                string funcName = f.Name[1..];

                IntPtr funcPtr = OpenGLDll.wglGetProcAddress(funcName);

                // https://wikis.khronos.org/opengl/Load_OpenGL_Functions#Windows
                if (funcPtr == IntPtr.Zero ||
                    funcPtr == (IntPtr)1 ||
                    funcPtr == (IntPtr)2 ||
                    funcPtr == (IntPtr)3 ||
                    funcPtr == (IntPtr)(-1)
                )
                {
                    // It is not uncommon for the call to not return a valid function pointer yet GetLastError() to return ERROR_SUCCESS
                    if (Marshal.GetLastWin32Error() != Win32.ERROR_SUCCESS)
                    {
                        L.CallerError(Win32.GetLastErrorMessage(nameof(OpenGLDll.wglGetProcAddress), funcName));
                    }

                    // Attempt to use native GetProcAddress() instead (https://wikis.khronos.org/opengl/Load_OpenGL_Functions#Windows)

                    if (_hOpenGLDll == IntPtr.Zero)
                    {
                        _hOpenGLDll = DllImports.LoadLibrary(OpenGLDll.LibraryName);

                        if (_hOpenGLDll == IntPtr.Zero)
                        {
                            throw Win32.GetLastException(nameof(DllImports.LoadLibrary), OpenGLDll.LibraryName);
                        }
                    }

                    funcPtr = DllImports.GetProcAddress(_hOpenGLDll, funcName);

                    if (funcPtr != IntPtr.Zero)
                    {
                        L.CallerWarning($"{funcName}() resolved via {nameof(DllImports.GetProcAddress)}()");
                    }
                    else if (Marshal.GetLastWin32Error() != Win32.ERROR_SUCCESS)
                    {
                        L.CallerError(Win32.GetLastErrorMessage(nameof(DllImports.GetProcAddress), funcName));
                    }
                }

                if (funcPtr != IntPtr.Zero)
                {
                    f.SetValue(_delegates, Marshal.GetDelegateForFunctionPointer(funcPtr, f.FieldType));
                }
                else
                {
                    failedFunctions.Add(funcName);
                    L.CallerError($"{funcName}() could not be resolved");
                }
            }

            if (failedFunctions.Any())
            {
                throw new EntryPointNotFoundException($"Failed to resolve {failedFunctions.Count} function(s): {string.Join("; ", failedFunctions)}");
            }
        }

        public void LoadWglFunctions()
        {
            if (_wglFunctionsLoaded)
            {
                return;
            }

            _wglFunctionsLoaded = true;

            LoadFunctions(fieldInfo => fieldInfo.Name.StartsWith("_wgl"));
        }

        public void LoadGlFunctions()
        {
            if (_glFunctionsLoaded)
            {
                return;
            }

            _glFunctionsLoaded = true;

            LoadFunctions(fieldInfo => fieldInfo.Name.StartsWith("_gl"));
        }

        private ImmutableHashSet<string>? _wglExtensions = null;
        public ImmutableHashSet<string> WglExtensions
        {
            get
            {
                if (_wglExtensions == null)
                {
                    LoadWglFunctions();

                    string? extensionsString = wglGetExtensionsStringARB(_hdc) ?? throw Win32.GetLastException(nameof(wglGetExtensionsStringARB));

                    _wglExtensions = ImmutableHashSet.Create<string>(extensionsString.Split());
                }

                return _wglExtensions;
            }
        }

        // OpenGL core profile and ARB extension interfaces

        public GLenumError glGetError()
        {
            return _delegates.glGetError();
        }

        public GLenumError glGetIntegerv(GLenum pname, out int data)
        {
            _delegates.glGetIntegerv(pname, out data);

            return glGetError();
        }

        public string? glGetString(GLenum name)
        {
            return Marshal.PtrToStringAnsi(_delegates.glGetString(name));
        }

        // WGL_ARB_create_context

        public IntPtr wglCreateContextAttribsARB(IntPtr hdc, params object[] attribList)
        {
            // Assume every attribList element is convertible to int.
            // Additionally, always append the mandatory NULL to terminate the list.
            return _delegates.wglCreateContextAttribsARB(hdc, hShareContext: IntPtr.Zero, attribList
                .Select(a => (int)a)
                .Append(0)
                .ToArray()
            );
        }

        // WGL_ARB_extensions_string

        public string? wglGetExtensionsStringARB(IntPtr hdc)
        {
            return Marshal.PtrToStringAnsi(_delegates.wglGetExtensionsStringARB(hdc));
        }

        // Helpers

        public void FlushGlError()
        {
            GLenumError error;

            do
            {
                error = glGetError();
            }
            while (error != GLenumError.GL_NO_ERROR);
        }

        // IDisposable interface

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                if (!OpenGLDll.wglDeleteContext(_hRenderCtx))
                {
                    L.CallerError(Win32.GetLastErrorMessage(nameof(OpenGLDll.wglDeleteContext)));
                }

                _disposed = true;
            }
        }

        ~Ctx()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
