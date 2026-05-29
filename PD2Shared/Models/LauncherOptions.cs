namespace PD2Shared.Models
{
    public class LauncherOptions
    {
        public bool ForceSoftwareRenderer { get; set; } = false;
        public bool UseHttp2 { get; set; } = false;
        public bool DisableAutoUpdate { get; set; } = false;
    }
}
