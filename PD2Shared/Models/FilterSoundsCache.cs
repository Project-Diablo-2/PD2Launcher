namespace PD2Shared.Models
{
    public sealed class FilterSoundsCache
    {
        // sound name
        public string Name { get; set; }

        // download url + sha
        public Dictionary<string, string> ShaByPath { get; set; } = new();
    }
}
