using System.Globalization;

namespace PD2Shared.Utils
{
    public static class Formatting
    {
        private const long Mebibyte = 1024 * 1024;

        private const int DefaultPrecision = 2;

        private static string FormatSizeInMiB(double value, string unitsStr, int precision)
        {
            string format = $"{{0:N{precision}}}{{1}}";

            return string.Format(CultureInfo.InvariantCulture, format, value, unitsStr);
        }

        // Don't attempt any sophisticated human-readable formatting for now
        public static string FormatSizeInMiB(long bytes, bool appendUnits = true, int precision = DefaultPrecision)
        {
            return FormatSizeInMiB(bytes / (double)Mebibyte, appendUnits ? " MiB" : "", precision);
        }

        public static string FormatThroughputInMiB(long bytes, long elapsedMilliseconds, bool appendUnits = true, int precision = DefaultPrecision)
        {
            // var mebibytesPerSec = bytes / ((double)elapsedMilliseconds / 1000) / Mebibyte;
            // This is a transformation of the above equation ^
            var mebibytesPerSec = (bytes * 1000) / (elapsedMilliseconds * (double)Mebibyte);

            return FormatSizeInMiB(mebibytesPerSec, appendUnits ? " MiB/s" : "", precision);
        }

        public static string FormatThroughputInMiB(long bytesPerSec, bool appendUnits = true, int precision = DefaultPrecision)
        {
            var mebibytesPerSec = bytesPerSec / (double)Mebibyte;

            return FormatSizeInMiB(mebibytesPerSec, appendUnits ? " MiB/s" : "", precision);
        }
    }
}
