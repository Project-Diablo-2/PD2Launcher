using System.Threading;
using System.Threading.Tasks;

namespace PD2Shared.Helpers
{
    using PD2Shared.Interfaces;
    using PD2Shared.Models;

    public static class UpdateConfigBootstrapper
    {
        // Produces your existing FileUpdateModel, stored under StorageKey.FileUpdateModel
        public static async Task<FileUpdateModel> LoadAsync(
            ILocalStorage storage,
            IRemoteConfig remote,
            CancellationToken ct)
        {
            var stored = storage.LoadSection<FileUpdateModel>(StorageKey.FileUpdateModel);
            var baseEndpoints = stored is not null
                ? new UpdateEndpoints(stored.Client ?? "", stored.Launcher ?? "", stored.FilePath ?? "")
                : UpdateDefaults.Create();

            var migrated = EndpointMigration.Migrate(baseEndpoints);

            // Optional remote override (ignored if no URL or non-200)
            var remoteCfg = await remote.TryFetchAsync(ct);
            var final = remoteCfg ?? migrated;

            var model = new FileUpdateModel
            {
                Client = UrlTools.Normalize(final.ClientBaseUrl),
                Launcher = UrlTools.Normalize(final.LauncherApiBase),
                FilePath = string.IsNullOrWhiteSpace(final.EnvironmentName) ? UpdateDefaults.DefaultEnv : final.EnvironmentName
            };

            storage.Update(StorageKey.FileUpdateModel, model);
            return model;
        }
    }
}

