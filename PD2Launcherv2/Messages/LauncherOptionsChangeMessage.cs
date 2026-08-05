namespace PD2Launcherv2.Messages
{
    public class LauncherOptionsChangeMessage
    {
        public bool ForceSoftwareRenderer { get; init; }
        public bool UseHttp2 { get; init; }
        public bool DisableAutoUpdate { get; init; }
    }
}
