
namespace PD2Shared.Models
{
    public class ResetInfo
    {
        public string ETag { get; set; } = "null";
        public ResetItem ResetData { get; set; } = null;
    }

    public class ResetItem
    {
        public string ResetTitle { get; set; } = null;
        public DateTime ResetTime { get; set; }
        public string ResetSummary { get; set; } = null;
        public string ResetContent { get; set; } = null;
        public string ResetLink { get; set; } = null;
    }
}
