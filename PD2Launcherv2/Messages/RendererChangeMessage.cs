namespace PD2Launcherv2.Messages
{
    public class RendererChangeMessage
    {
        // <!> This should really be a common enum and not a bool
        public bool UseD2GL { get; init; }
        public bool CncDdrawUsesOGL { get; init; }
    }
}
