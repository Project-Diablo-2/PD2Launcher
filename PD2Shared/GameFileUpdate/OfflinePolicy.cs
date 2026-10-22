namespace PD2Shared.GameFileUpdate
{
    public enum OfflinePolicy
    {
        AllowOffline,

        ForceOffline,
        ForceOnline
    }

    // Sneaking in an extension class for convenience
    public static class OfflinePolicyEx
    {
        public static bool AllowOffline(this OfflinePolicy offlinePolicy)
        {
            return offlinePolicy == OfflinePolicy.AllowOffline;
        }

        public static bool ForceOffline(this OfflinePolicy offlinePolicy)
        {
            return offlinePolicy == OfflinePolicy.ForceOffline;
        }

        public static bool ForceOnline(this OfflinePolicy offlinePolicy)
        {
            return offlinePolicy == OfflinePolicy.ForceOnline;
        }
    }
}
