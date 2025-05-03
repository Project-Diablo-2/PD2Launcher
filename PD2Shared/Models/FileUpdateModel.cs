namespace PD2Shared.Models
{
    public class FileUpdateModel
    {
        public string Client { get; set; } = "https://storage.googleapis.com/storage/v1/b/pd2-client-files/o";
        public string FilePath { get; set; } = "Live";
        public string Launcher { get; set; } = "https://storage.googleapis.com/storage/v1/b/pd2-launcher-update/o";
        public string Other { get; set; } = "";
    }
}