using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace PD2Shared.Helpers
{
    public static class HttpFileHelper
    {
        private static readonly HttpClient _client = new();

        public static async Task<bool> FileNeedsUpdateAsync(string remoteUrl, string localPath)
        {
            if (!File.Exists(localPath))
            {
                Console.WriteLine($"[UpdateCheck] Local file '{localPath}' is missing.");
                return true;
            }

            var headRequest = new HttpRequestMessage(HttpMethod.Head, remoteUrl);
            var response = await _client.SendAsync(headRequest);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[UpdateCheck] HEAD request failed: {response.StatusCode}");
                return true; // Fail safe: assume file needs update
            }

            long? remoteSize = response.Content.Headers.ContentLength;
            long localSize = new FileInfo(localPath).Length;

            Console.WriteLine($"[UpdateCheck] Remote Size: {remoteSize}, Local Size: {localSize}");

            return remoteSize != localSize;
        }
    }
}