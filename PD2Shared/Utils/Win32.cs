using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PD2Shared.Utils
{
    public static class Win32
    {
        public const int ERROR_SUCCESS = 0;

        public static string GetLastErrorMessage(int error)
        {
            return GetLastException(error).Message;
        }

        public static string GetLastErrorMessage()
        {
            return GetLastException().Message;
        }

        public static string GetLastErrorMessage(int error, string functionName, params object?[] args)
        {
            return GetLastException(error, functionName, args).Message;
        }

        public static string GetLastErrorMessage(string functionName, params object?[] args)
        {
            return GetLastException(functionName, args).Message;
        }

        public static Win32Exception GetLastException(int error)
        {
            return new Win32Exception(error);
        }

        public static Win32Exception GetLastException()
        {
            return new Win32Exception(Marshal.GetLastWin32Error());
        }

        public static Win32Exception GetLastException(int error, string functionName, params object?[] args)
        {
            return new Win32Exception(error, $"{functionName}({string.Join(", ", args.Select(a => a is null ? "NULL" : a is string ? $"\"{a}\"" : a))}) failed: {new Win32Exception(error).Message}");
        }

        public static Win32Exception GetLastException(string functionName, params object?[] args)
        {
            return GetLastException(Marshal.GetLastWin32Error(), functionName, args);
        }
    }
}
