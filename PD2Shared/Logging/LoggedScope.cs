using System.Runtime.CompilerServices;
using PD2Shared.Utils;
using static PD2Shared.Logging.LoggingStatic;

namespace PD2Shared.Logging
{
    public class LoggedScope : TimedDisposable
    {
        public LoggedScope(string message,
            Serilog.Events.LogEventLevel logEventLevel = Serilog.Events.LogEventLevel.Information,
            [CallerMemberName] string callerName = null!)
            : base(() =>
        {
            L.CallerWrite(logEventLevel, $"> {message}", propertyValues: null, callerName);
        },
        timeSpan =>
        {
            L.CallerWrite(logEventLevel, $"< {message} Finished in {timeSpan}.", propertyValues: null, callerName);
        })
        {
        }
    }
}
