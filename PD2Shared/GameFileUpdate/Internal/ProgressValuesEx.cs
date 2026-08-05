namespace PD2Shared.GameFileUpdate.Internal
{
    internal static class ProgressValuesEx
    {
        private const long _invalidSize = -1;

        // Since Int64 is commonly used to store file sizes, whenever using a Nullable<long> is undesirable, resort to the special value of "InvalidSize".
        public static long GetInvalidSize(this long _) => _invalidSize;

        public static bool IsInvalidSize(this long value) => value == _invalidSize;
    }
}
