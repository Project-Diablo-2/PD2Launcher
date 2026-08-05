namespace PD2Launcherv2.Utils.Gl.Internal
{
    // OpenGL core profile and ARB extension interfaces

    [Flags]
    internal enum GLenum : uint
    {
        // Include only a small subset
#pragma warning disable format
        // GL_VERSION_1_0
        GL_VENDOR                   = 0x1F00,
        GL_RENDERER                 = 0x1F01,
        GL_VERSION                  = 0x1F02,

        // GL_VERSION_2_0
        GL_SHADING_LANGUAGE_VERSION = 0x8B8C,

        // GL_VERSION_3_0
        GL_CONTEXT_FLAG_FORWARD_COMPATIBLE_BIT = 0x00000001,
        GL_CONTEXT_FLAG_DEBUG_BIT              = 0x00000002,

        GL_MAJOR_VERSION            = 0x821B,
        GL_MINOR_VERSION            = 0x821C,

        GL_CONTEXT_FLAGS            = 0x821E,

        // GL_VERSION_3_2
        GL_CONTEXT_CORE_PROFILE_BIT          = 0x00000001,
        GL_CONTEXT_COMPATIBILITY_PROFILE_BIT = 0x00000002,

        GL_CONTEXT_PROFILE_MASK     = 0x9126,

        // GL_VERSION_4_5
        GL_CONTEXT_FLAG_ROBUST_ACCESS_BIT    = 0x00000004,

        // GL_VERSION_4_6
        GL_CONTEXT_FLAG_NO_ERROR_BIT         = 0x00000008,
#pragma warning restore format
    }

    internal enum GLenumError : uint
    {
#pragma warning disable format
        // GL_VERSION_1_0
        GL_NO_ERROR                      = 0,
        GL_INVALID_ENUM                  = 0x0500,
        GL_INVALID_VALUE                 = 0x0501,
        GL_INVALID_OPERATION             = 0x0502,
        GL_STACK_OVERFLOW                = 0x0503,
        GL_STACK_UNDERFLOW               = 0x0504,
        GL_OUT_OF_MEMORY                 = 0x0505,

        // GL_VERSION_3_0
        GL_INVALID_FRAMEBUFFER_OPERATION = 0x0506,

        // GL_VERSION_4_5 (or ARB_KHR_robustness)
        GL_CONTEXT_LOST                  = 0x0507,
#pragma warning restore format
}

    // WGL_ARB_create_context

    internal enum WglContextAttribs : int
    {
#pragma warning disable format
        // Accepted as an attribute name in <*attribList>:
        WGL_CONTEXT_MAJOR_VERSION_ARB          = 0x2091,
        WGL_CONTEXT_MINOR_VERSION_ARB          = 0x2092,
        WGL_CONTEXT_LAYER_PLANE_ARB            = 0x2093,
        WGL_CONTEXT_FLAGS_ARB                  = 0x2094,
        WGL_CONTEXT_PROFILE_MASK_ARB           = 0x9126,

        // Accepted as bits in the attribute value for WGL_CONTEXT_FLAGS in <*attribList>:
        WGL_CONTEXT_DEBUG_BIT_ARB              = 0x0001,
        WGL_CONTEXT_FORWARD_COMPATIBLE_BIT_ARB = 0x0002,

        // Accepted as bits in the attribute value for WGL_CONTEXT_PROFILE_MASK_ARB in <*attribList>:
        // (Only available if WGL_ARB_create_context_profile is available or if >=3.2)
        WGL_CONTEXT_CORE_PROFILE_BIT_ARB          = 0x00000001,
        WGL_CONTEXT_COMPATIBILITY_PROFILE_BIT_ARB = 0x00000002,
#pragma warning restore format
    }

    // Additional GetLastError() WGL values
    internal enum LastGLerror : int
    {
        ERROR_INVALID_VERSION_ARB = 0x2095,
        ERROR_INVALID_PROFILE_ARB = 0x2096,
    }
}
