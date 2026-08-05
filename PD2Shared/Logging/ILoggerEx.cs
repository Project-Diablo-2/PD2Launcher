using Serilog;
using System.Runtime.CompilerServices;

namespace PD2Shared.Logging
{
    public static class ILoggerEx
    {
        private static readonly string SeparatorLine = "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -";

        private static readonly object _lastExceptionStackTraceLock = new();
        private static string? _lastExceptionStackTrace = null;

        private static void CallerWrite(this ILogger logger, Serilog.Events.LogEventLevel logEventLevel, Exception? exception, string messageTemplate, string callerName, object?[]? propertyValues)
        {
            if (exception != null)
            {
                // This can have moderate impact on the logger's performance, but should help de-clutter the log.
                lock (_lastExceptionStackTraceLock)
                {
                    if (_lastExceptionStackTrace == exception.StackTrace)
                    {
                        messageTemplate = messageTemplate + Environment.NewLine +
                            exception.GetType() + ": " + exception.Message + Environment.NewLine +
                            "   <Repeated stack trace>";
                        exception = null;
                    }
                    else
                    {
                        _lastExceptionStackTrace = exception.StackTrace;
                    }
                }
            }

            logger.Write(logEventLevel, exception, $"{(callerName + "()"),-30} " + messageTemplate, propertyValues);
        }

        public static void CallerWrite(this ILogger logger, Serilog.Events.LogEventLevel logEventLevel, string messageTemplate, object?[]? propertyValues = null, [CallerMemberName] string callerName = "?")
        {
            CallerWrite(logger, logEventLevel, exception: null, messageTemplate, callerName, propertyValues);
        }

        public static void CallerVerbose(this ILogger logger, string messageTemplate, object?[]? propertyValues = null, [CallerMemberName] string callerName = "?")
        {
            CallerWrite(logger, Serilog.Events.LogEventLevel.Verbose, exception: null, messageTemplate, callerName, propertyValues);
        }

        public static void CallerVerbose(this ILogger logger, Exception? exception, string messageTemplate, object?[]? propertyValues = null, [CallerMemberName] string callerName = "?")
        {
            CallerWrite(logger, Serilog.Events.LogEventLevel.Verbose, exception, messageTemplate, callerName, propertyValues);
        }

        public static void CallerDebug(this ILogger logger, string messageTemplate, object?[]? propertyValues = null, [CallerMemberName] string callerName = "?")
        {
            CallerWrite(logger, Serilog.Events.LogEventLevel.Debug, exception: null, messageTemplate, callerName, propertyValues);
        }

        public static void CallerDebug(this ILogger logger, Exception? exception, string messageTemplate, object?[]? propertyValues = null, [CallerMemberName] string callerName = "?")
        {
            CallerWrite(logger, Serilog.Events.LogEventLevel.Debug, exception, messageTemplate, callerName, propertyValues);
        }

        public static void CallerInformation(this ILogger logger, string messageTemplate, object?[]? propertyValues = null, [CallerMemberName] string callerName = "?")
        {
            CallerWrite(logger, Serilog.Events.LogEventLevel.Information, exception: null, messageTemplate, callerName, propertyValues);
        }

        public static void CallerInformation(this ILogger logger, Exception? exception, string messageTemplate, object?[]? propertyValues = null, [CallerMemberName] string callerName = "?")
        {
            CallerWrite(logger, Serilog.Events.LogEventLevel.Information, exception, messageTemplate, callerName, propertyValues);
        }

        public static void CallerWarning(this ILogger logger, string messageTemplate, object?[]? propertyValues = null, [CallerMemberName] string callerName = "?")
        {
            CallerWrite(logger, Serilog.Events.LogEventLevel.Warning, exception: null, messageTemplate, callerName, propertyValues);
        }

        public static void CallerWarning(this ILogger logger, Exception? exception, string messageTemplate, object?[]? propertyValues = null, [CallerMemberName] string callerName = "?")
        {
            CallerWrite(logger, Serilog.Events.LogEventLevel.Warning, exception, messageTemplate, callerName, propertyValues);
        }

        public static void CallerError(this ILogger logger, string messageTemplate, object?[]? propertyValues = null, [CallerMemberName] string callerName = "?")
        {
            CallerWrite(logger, Serilog.Events.LogEventLevel.Error, exception: null, messageTemplate, callerName, propertyValues);
        }

        public static void CallerError(this ILogger logger, Exception? exception, string messageTemplate, object?[]? propertyValues = null, [CallerMemberName] string callerName = "?")
        {
            CallerWrite(logger, Serilog.Events.LogEventLevel.Error, exception, messageTemplate, callerName, propertyValues);
        }

        public static void CallerFatal(this ILogger logger, string messageTemplate, object?[]? propertyValues = null, [CallerMemberName] string callerName = "?")
        {
            CallerWrite(logger, Serilog.Events.LogEventLevel.Fatal, exception: null, messageTemplate, callerName, propertyValues);
        }

        public static void CallerFatal(this ILogger logger, Exception? exception, string messageTemplate, object?[]? propertyValues = null, [CallerMemberName] string callerName = "?")
        {
            CallerWrite(logger, Serilog.Events.LogEventLevel.Fatal, exception, messageTemplate, callerName, propertyValues);
        }

        public static void Separator(this ILogger logger, Serilog.Events.LogEventLevel logEventLevel = Serilog.Events.LogEventLevel.Information)
        {
            logger.Write(logEventLevel, SeparatorLine);
        }
    }
}
