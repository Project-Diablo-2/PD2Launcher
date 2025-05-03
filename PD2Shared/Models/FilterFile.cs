using Newtonsoft.Json;

namespace PD2Shared.Models
{
    public class FilterFile
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "null";

        [JsonProperty("download_url")]
        public string DownloadUrl { get; set; } = "null";

        [JsonProperty("path")]
        public string Path { get; set; } = "null";

        [JsonProperty("sha")]
        public string Sha { get; set; } = "null";

        [JsonProperty("size")]
        public int Size { get; set; } = 1;

        [JsonProperty("url")]
        public string Url { get; set; } = "null";

        [JsonProperty("html_url")]
        public string HtmlUrl { get; set; } = "null";

        [JsonProperty("git_url")]
        public string GitUrl { get; set; } = "null";

        [JsonProperty("type")]
        public string Type { get; set; } = "null";
    }
}