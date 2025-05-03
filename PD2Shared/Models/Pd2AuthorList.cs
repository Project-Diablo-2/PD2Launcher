namespace PD2Shared.Models
{
    public class Pd2AuthorList
    {
        public string StorageETag { get; set; } = "null";
        public List<FilterAuthor> StorageAuthorList { get; set; }
    }
}