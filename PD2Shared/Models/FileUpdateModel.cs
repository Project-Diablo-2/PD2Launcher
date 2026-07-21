namespace PD2Shared.Models
{
    public class FileUpdateModel
    {
        public string Client { get; set; } = "https://pd2-client-files.projectdiablo2.com/";
        public string FilePath { get; set; } = "Live";

        public string Other { get; set; } = "";
    }
}