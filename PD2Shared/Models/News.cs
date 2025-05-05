using Newtonsoft.Json;

namespace PD2Shared.Models
{
    public class News
    {
        public string ETag { get; set; } = "null";
        public List<NewsItem> news { get; set; }
    }

    public class NewsItem
    {
        [JsonProperty("date")]
        public string Date { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("image")]
        public string Image { get; set; }

        [JsonProperty("link")]
        public string Link { get; set; }
    }
}