using Avalonia.Threading;

namespace Quartermaster.Services;

/// <summary>
/// Posts a callback at Background dispatcher priority so it runs after queued panel events
/// (panels reset their own loading guard via BasePanelControl.DeferLoadingReset at the same
/// priority). Keeps the MainWindow loading guard up until populate-time change events drain,
/// so opening a file does not mark it dirty (#2459).
/// </summary>
public static class DeferredGuardReset
{
    /// <summary>
    /// The single dispatcher priority used to defer BOTH the panels' own IsLoading resets
    /// (BasePanelControl.DeferLoadingReset + the inline posts in StatsPanel/CharacterPanel/
    /// AppearancePanel) AND the MainWindow loading-guard reset. Both sides MUST reference this
    /// one value: the ordering invariant (window guard drains AFTER panel resets, FIFO within a
    /// priority) only holds if they share it. Changing it here changes both sides at once (#2459).
    /// </summary>
    public static readonly DispatcherPriority LoadingResetPriority = DispatcherPriority.Background;

    /// <summary>Backward-compatible alias for <see cref="LoadingResetPriority"/>.</summary>
    public static DispatcherPriority Priority => LoadingResetPriority;

    public static void Post(System.Action reset)
        => Dispatcher.UIThread.Post(reset, LoadingResetPriority);
}
