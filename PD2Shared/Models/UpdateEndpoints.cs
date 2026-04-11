
namespace PD2Shared.Models
{
    public sealed record UpdateEndpoints(
        string ClientBaseUrl,
        string LauncherApiBase,
        string EnvironmentName
    );

    public static class UpdateDefaults
    {
        public const string ClientBaseUrl = "https://pd2-client-files.projectdiablo2.com/";
        public const string LauncherApiBase = "https://storage.googleapis.com/storage/v1/b/pd2-launcher-update/o";
        public const string DefaultEnv = "Live";

        public static UpdateEndpoints Create() =>
            new(ClientBaseUrl, LauncherApiBase, DefaultEnv);
    }
}
