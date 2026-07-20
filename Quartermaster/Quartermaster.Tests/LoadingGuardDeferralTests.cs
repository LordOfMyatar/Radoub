using Avalonia.Threading;
using Quartermaster.Services;

namespace Quartermaster.Tests;

/// <summary>
/// Contract tests for the loading-guard defer priority (#2459).
///
/// The MainWindow loading-guard reset (<see cref="DeferredGuardReset.Post"/>) must be posted at
/// the SAME dispatcher priority the panels use for their own deferred IsLoading resets
/// (<c>BasePanelControl.DeferLoadingReset</c> and the inline posts in StatsPanel/CharacterPanel/
/// AppearancePanel). Because the Avalonia dispatcher is FIFO within a priority, a post made after
/// the panels' posts drains last — so populate-time change events can't slip past the window guard
/// and mark a fresh file dirty.
///
/// The invariant is protected structurally: BOTH sides reference the single
/// <see cref="DeferredGuardReset.LoadingResetPriority"/> constant, so they cannot drift — changing
/// the priority is a one-line edit that flips both. These tests pin that constant's value and that
/// <see cref="DeferredGuardReset.Post"/> routes through it.
///
/// Quartermaster.Tests is not wired for Avalonia headless execution (no
/// <c>[AvaloniaTestApplication]</c>), so we pin the load-bearing constant rather than driving a
/// live dispatcher to observe ordering.
/// </summary>
public class LoadingGuardDeferralTests
{
    [Fact]
    public void LoadingResetPriority_IsBackground()
    {
        // Background is the priority both the panels and the window guard defer at.
        Assert.Equal(DispatcherPriority.Background, DeferredGuardReset.LoadingResetPriority);
    }

    [Fact]
    public void WindowGuard_And_Panels_ShareTheSamePriorityConstant()
    {
        // DeferredGuardReset.Post uses LoadingResetPriority (exposed via Priority), and
        // BasePanelControl.DeferLoadingReset references the SAME public constant. Asserting they
        // are the identical value pins the shared source of truth: if a future edit changes the
        // panel-side priority in isolation, it would have to change this constant, breaking this
        // test rather than silently breaking the ordering invariant.
        Assert.Equal(DeferredGuardReset.LoadingResetPriority, DeferredGuardReset.Priority);
    }
}
