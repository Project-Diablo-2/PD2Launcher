namespace PD2Shared.Extensions
{
    public static class HttpResponseMessageEx
    {
        // An alternative to EnsureSuccessStatusCode() since it only produces HttpRequestExceptions with (questionably useful) messages such as:
        // 'Response status code does not indicate success: 404 (Not Found).'
        public static void ThrowIfUnsuccessful(this HttpResponseMessage httpResponseMessage)
        {
            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                var reqMsg = httpResponseMessage.RequestMessage!;

                throw new HttpRequestException($"{reqMsg.Method} '{reqMsg.RequestUri}' failed with: {(int)httpResponseMessage.StatusCode} ({httpResponseMessage.StatusCode})", inner: null, httpResponseMessage.StatusCode);
            }
        }
    }
}
