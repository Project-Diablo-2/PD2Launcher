using System.Runtime.InteropServices;

namespace PD2Launcherv2.Utils.Gl.Internal
{
    internal class CtxDelegates
    {
        // OpenGL core profile and ARB extension interfaces (https://registry.khronos.org/OpenGL/api/GL/glcorearb.h)

        protected delegate GLenumError glGetErrorDelegate();
        protected glGetErrorDelegate? _glGetError = null;
        public GLenumError glGetError()
        {
            if (_glGetError == null)
            {
                throw new EntryPointNotFoundException(nameof(glGetError));
            }

            return _glGetError();
        }

        protected delegate void glGetIntegervDelegate(GLenum pname, out int data);
        protected glGetIntegervDelegate? _glGetIntegerv = null;
        public void glGetIntegerv(GLenum pname, out int data)
        {
            if (_glGetIntegerv == null)
            {
                throw new NotImplementedException(nameof(glGetIntegerv));
            }

            _glGetIntegerv(pname, out data);
        }

        protected delegate IntPtr glGetStringDelegate(GLenum name);
        protected glGetStringDelegate? _glGetString = null;
        public IntPtr glGetString(GLenum name)
        {
            if (_glGetString == null)
            {
                throw new NotImplementedException(nameof(glGetString));
            }

            return _glGetString(name);
        }

        // WGL_ARB_create_context (https://registry.khronos.org/OpenGL/extensions/ARB/WGL_ARB_create_context.txt)

        protected delegate IntPtr wglCreateContextAttribsARBDelegate(IntPtr hdc, IntPtr hShareContext, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.I4)][In] int[] attribList);
        protected wglCreateContextAttribsARBDelegate? _wglCreateContextAttribsARB = null;
        public IntPtr wglCreateContextAttribsARB(IntPtr hdc, IntPtr hShareContext, int[] attribList)
        {
            if (_wglCreateContextAttribsARB == null)
            {
                throw new NotImplementedException(nameof(wglCreateContextAttribsARB));
            }

            return _wglCreateContextAttribsARB(hdc, hShareContext, attribList);
        }

        // WGL_ARB_extensions_string (https://registry.khronos.org/OpenGL/extensions/ARB/WGL_ARB_extensions_string.txt)

        protected delegate IntPtr wglGetExtensionsStringARBDelegate(IntPtr hdc);
        protected wglGetExtensionsStringARBDelegate? _wglGetExtensionsStringARB = null;
        public IntPtr wglGetExtensionsStringARB(IntPtr hdc)
        {
            if (_wglGetExtensionsStringARB == null)
            {
                throw new NotImplementedException(nameof(wglGetExtensionsStringARB));
            }

            return _wglGetExtensionsStringARB(hdc);
        }
    }
}
