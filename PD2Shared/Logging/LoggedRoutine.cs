using System.Runtime.CompilerServices;
using PD2Shared.Utils;
using static PD2Shared.Logging.LoggingStatic;

namespace PD2Shared.Logging
{
    public class LoggedRoutine : TimedDisposable
    {
        public LoggedRoutine(
            Serilog.Events.LogEventLevel logEventLevel = Serilog.Events.LogEventLevel.Information,
            [CallerMemberName] string callerName = null!)
            : base(() =>
        {
            L.CallerWrite(logEventLevel, $">>", propertyValues: null, callerName);
        },
        timeSpan =>
        {
            L.CallerWrite(logEventLevel, $"<< {timeSpan}", propertyValues: null, callerName);
        })
        {
        }
    }
}
