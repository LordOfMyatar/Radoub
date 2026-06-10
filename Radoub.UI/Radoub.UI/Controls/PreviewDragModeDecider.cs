namespace Radoub.UI.Controls;

/// <summary>Active drag interaction in the shared model preview (#2430).</summary>
public enum PreviewDragMode
{
    None,
    Rotate,
    Pan
}

/// <summary>
/// Pure rotate-vs-pan decision for the model preview's pointer drag (#2430). Extracted from
/// Quartermaster's AppearancePanel so the rule is testable without a live pointer/FlaUI.
/// </summary>
public static class PreviewDragModeDecider
{
    /// <summary>
    /// Middle button or (left + Shift) → Pan; left alone → Rotate; anything else → None.
    /// Middle wins over left so a middle-drag always pans even if left is also reported down.
    /// </summary>
    public static PreviewDragMode Decide(bool isLeft, bool isMiddle, bool shift)
    {
        if (isMiddle || (isLeft && shift)) return PreviewDragMode.Pan;
        if (isLeft) return PreviewDragMode.Rotate;
        return PreviewDragMode.None;
    }
}
