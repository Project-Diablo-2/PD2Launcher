using System.Diagnostics;

namespace PD2Shared.Interfaces
{
    public interface ILaunchGameHelpers
    {
        Process LaunchGame(ILocalStorage storage, EventHandler? exitedEventHandler = null);
    }
}
