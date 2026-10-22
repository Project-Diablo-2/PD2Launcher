using System.Net;

namespace PD2Shared.Extensions
{
    public static class IPAddressEx
    {
        public static IPAddress GetCleanAddress(this IPAddress ipAddress)
        {
            return ipAddress.IsIPv4MappedToIPv6 ? ipAddress.MapToIPv4() : ipAddress;
        }
    }
}
