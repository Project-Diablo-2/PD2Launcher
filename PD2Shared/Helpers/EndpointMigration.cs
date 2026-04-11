using System;

namespace PD2Shared.Helpers
{
    using PD2Shared.Models;

    public static class UrlTools
    {
        public static string Normalize(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            url = url.Trim();
            return url.EndsWith("/") ? url : url + "/";
        }
    }

    public static class EndpointMigration
    {
        public static UpdateEndpoints Migrate(UpdateEndpoints incoming)
        {
            // One-time GCP -> AWS/CDN fixups
            var client = incoming.ClientBaseUrl;
            if (string.Equals(client, "https://storage.googleapis.com/storage/v1/b/pd2-client-files/o", StringComparison.OrdinalIgnoreCase))
                client = "https://pd2-client-files.projectdiablo2.com/";
            else if (string.Equals(client, "https://storage.googleapis.com/storage/v1/b/pd2-beta-client-files/o", StringComparison.OrdinalIgnoreCase))
                client = "https://pd2-beta-client-files.projectdiablo2.com/";

            var launcher = incoming.LauncherApiBase;
            var env = string.IsNullOrWhiteSpace(incoming.EnvironmentName)
                ? UpdateDefaults.DefaultEnv
                : incoming.EnvironmentName.Trim();

            return new UpdateEndpoints(
                UrlTools.Normalize(client),
                UrlTools.Normalize(launcher),
                env
            );
        }
    }
}

