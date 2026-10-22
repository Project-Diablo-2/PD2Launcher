using System.Globalization;

namespace PD2Shared.Extensions
{
    public static class DateTimeOffsetEx
    {
        public static string ToRfc1123(this DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
