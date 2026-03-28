using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PD2Shared.Helpers
{
    using PD2Shared.Models;

    public interface IRemoteConfig
    {
        Task<UpdateEndpoints?> TryFetchAsync(CancellationToken ct);
    }

    public sealed class RemoteConfig : IRemoteConfig
    {
        private readonly HttpClient _http;
        private readonly string _configUrl; // empty disables remote override

        public RemoteConfig(HttpClient http, string configUrl)
        {
            _http = http;
            _configUrl = configUrl ?? "";
        }

        public async Task<UpdateEndpoints?> TryFetchAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_configUrl)) return null;

            using var resp = await _http.GetAsync(_configUrl, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync(ct);
            var dto = JsonSerializer.Deserialize<UpdateEndpoints>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return dto is null ? null : EndpointMigration.Migrate(dto);
        }
    }
}

