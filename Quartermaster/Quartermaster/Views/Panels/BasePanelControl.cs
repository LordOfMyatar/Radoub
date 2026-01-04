using Avalonia.Controls;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Panels;

/// <summary>
/// Base class for Quartermaster panels providing common functionality:
/// - Loading state tracking (prevents event handler re-entrancy during data binding)
/// - Current creature reference
/// - UI helper methods for setting control values
/// </summary>
public abstract class BasePanelControl : UserControl
{
    /// <summary>
    /// The currently loaded creature. Null when no creature is loaded.
    /// </summary>
    protected UtcFile? CurrentCreature { get; set; }

    /// <summary>
    /// True while loading data into controls. Used to suppress change events during population.
    /// </summary>
    protected bool IsLoading { get; set; }

    /// <summary>
    /// Load and display data for the given creature.
    /// </summary>
    public abstract void LoadCreature(UtcFile? creature);

    /// <summary>
    /// Clear all panel controls to default/empty state.
    /// </summary>
    public abstract void ClearPanel();

    /// <summary>
    /// Defer clearing IsLoading until after Avalonia dispatcher processes queued events.
    /// Call this at the end of LoadCreature to prevent event handlers from firing during initial binding.
    /// </summary>
    protected void DeferLoadingReset()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(
            () => IsLoading = false,
            Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// Safely set TextBlock.Text with null check.
    /// </summary>
    protected static void SetText(TextBlock? block, string text)
    {
        if (block != null)
            block.Text = text;
    }

    /// <summary>
    /// Safely set CheckBox.IsChecked with null check.
    /// </summary>
    protected static void SetCheckBox(CheckBox? cb, bool value)
    {
        if (cb != null)
            cb.IsChecked = value;
    }

    /// <summary>
    /// Safely set TextBox.Text with null check.
    /// </summary>
    protected static void SetTextBox(TextBox? textBox, string text)
    {
        if (textBox != null)
            textBox.Text = text;
    }
}
