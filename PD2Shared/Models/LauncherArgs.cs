namespace PD2Shared.Models
{
    public class LauncherArgs
    {
        public bool graphics { get; set; } = false;
        public bool skiptobnet { get; set; } = true;
        public bool sndbkg { get; set; } = false;
        public bool disableAutoUpdate { get; set; } = false;
    }
}