using Radoub.UI.Controls;
using Xunit;

namespace Radoub.UI.Tests.Controls;

/// <summary>
/// Pure decision logic for the shared model-preview drag mode (#2430). Extracted from
/// Quartermaster's AppearancePanel so the rotate-vs-pan rule is unit-testable without FlaUI.
/// Rule (mirrors QM): middle button OR (left + Shift) → Pan; left alone → Rotate; else None.
/// </summary>
public class PreviewDragModeDeciderTests
{
    [Fact]
    public void LeftButtonAlone_Rotates()
    {
        Assert.Equal(PreviewDragMode.Rotate,
            PreviewDragModeDecider.Decide(isLeft: true, isMiddle: false, shift: false));
    }

    [Fact]
    public void MiddleButton_Pans()
    {
        Assert.Equal(PreviewDragMode.Pan,
            PreviewDragModeDecider.Decide(isLeft: false, isMiddle: true, shift: false));
    }

    [Fact]
    public void LeftWithShift_Pans()
    {
        Assert.Equal(PreviewDragMode.Pan,
            PreviewDragModeDecider.Decide(isLeft: true, isMiddle: false, shift: true));
    }

    [Fact]
    public void MiddleTakesPrecedenceOverLeft()
    {
        Assert.Equal(PreviewDragMode.Pan,
            PreviewDragModeDecider.Decide(isLeft: true, isMiddle: true, shift: false));
    }

    [Fact]
    public void NoButton_IsNone()
    {
        Assert.Equal(PreviewDragMode.None,
            PreviewDragModeDecider.Decide(isLeft: false, isMiddle: false, shift: false));
    }

    [Fact]
    public void ShiftWithoutButton_IsNone()
    {
        Assert.Equal(PreviewDragMode.None,
            PreviewDragModeDecider.Decide(isLeft: false, isMiddle: false, shift: true));
    }
}
