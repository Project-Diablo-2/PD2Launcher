using Newtonsoft.Json;

namespace PD2Shared.Models
{
    public class FilterDefinitionsDocument
    {
        [JsonProperty("filter_info")]
        public Dictionary<string, FilterDefinitionEntry> FilterInfo { get; set; } =
            new();
    }

    public class FilterDefinitionEntry
    {
        [JsonProperty("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("file_name")]
        public string FileName { get; set; } = string.Empty;

        [JsonProperty("file_name_beta")]
        public string FileNameBeta { get; set; } = string.Empty;

        public string ResolveFileName(bool isBeta)
        {
            return isBeta && !string.IsNullOrWhiteSpace(FileNameBeta)
                ? FileNameBeta
                : FileName;
        }
    }
}
