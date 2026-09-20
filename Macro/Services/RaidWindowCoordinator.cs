namespace Macro.Services;

// Screen capture and physical mouse input share one desktop.
internal static class RaidWindowCoordinator
{
    internal static readonly SemaphoreSlim Gate = new(1, 1);
}
