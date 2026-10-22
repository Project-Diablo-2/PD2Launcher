namespace PD2Shared.Logging
{
    public static class LoggingStatic
    {
        // A convenience property to be combined with ILoggerEx
        public static Serilog.ILogger L { get => Serilog.Log.Logger; }

        // A convenience method for constructing arrays from "params" to be easily passed to ILoggerEx methods
        public static object?[]? ExplicitArray(params object?[] args)
        {
            return args;
        }
    }
}
