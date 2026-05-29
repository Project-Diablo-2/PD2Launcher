namespace PD2Launcherv2.Messages
{
    public class LauncherOptionsChangeMessage
    {
        public bool ForceSoftwareRenderer { get; set; }
        public bool UseHttp2 { get; set; }
        public bool DisableAutoUpdate { get; set; }
    }
}
