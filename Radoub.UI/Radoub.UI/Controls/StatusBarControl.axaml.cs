using Avalonia;
using Avalonia.Controls;

namespace Radoub.UI.Controls;

/// <summary>
/// Shared status bar control for Radoub tools.
/// Provides consistent styling and layout for status information.
/// </summary>
/// <remarks>
/// Usage:
/// <code>
/// &lt;radoub:StatusBarControl
///     PrimaryText="{Binding StatusText}"
///     SecondaryText="{Binding ItemCount}"
///     TertiaryText="{Binding TlkStatus}"
///     FilePath="{Binding CurrentFilePath}"
///     ShowProgress="{Binding IsLoading}" /&gt;
/// </code>
/// </remarks>
public partial class StatusBarControl : UserControl
{
    /// <summary>
    /// Primary status text displayed on the left (e.g., "Ready", "Loading...").
    /// </summary>
    public static readonly StyledProperty<string> PrimaryTextProperty =
        AvaloniaProperty.Register<StatusBarControl, string>(nameof(PrimaryText), "Ready");

    /// <summary>
    /// Secondary text displayed after primary (e.g., item count, selection info).
    /// </summary>
    public static readonly StyledProperty<string?> SecondaryTextProperty =
        AvaloniaProperty.Register<StatusBarControl, string?>(nameof(SecondaryText));

    /// <summary>
    /// Tertiary text displayed on the right (e.g., TLK status).
    /// </summary>
    public static readonly StyledProperty<string?> TertiaryTextProperty =
        AvaloniaProperty.Register<StatusBarControl, string?>(nameof(TertiaryText));

    /// <summary>
    /// File path displayed on the far right (automatically trimmed if too long).
    /// </summary>
    public static readonly StyledProperty<string?> FilePathProperty =
        AvaloniaProperty.Register<StatusBarControl, string?>(nameof(FilePath));

    /// <summary>
    /// Whether to show the indeterminate progress indicator.
    /// </summary>
    public static readonly StyledProperty<bool> ShowProgressProperty =
        AvaloniaProperty.Register<StatusBarControl, bool>(nameof(ShowProgress), false);

    public StatusBarControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Primary status text displayed on the left.
    /// </summary>
    public string PrimaryText
    {
        get => GetValue(PrimaryTextProperty);
        set => SetValue(PrimaryTextProperty, value);
    }

    /// <summary>
    /// Secondary text displayed after primary.
    /// </summary>
    public string? SecondaryText
    {
        get => GetValue(SecondaryTextProperty);
        set => SetValue(SecondaryTextProperty, value);
    }

    /// <summary>
    /// Tertiary text displayed on the right.
    /// </summary>
    public string? TertiaryText
    {
        get => GetValue(TertiaryTextProperty);
        set => SetValue(TertiaryTextProperty, value);
    }

    /// <summary>
    /// File path displayed on the far right.
    /// </summary>
    public string? FilePath
    {
        get => GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    /// <summary>
    /// Whether to show the progress indicator.
    /// </summary>
    public bool ShowProgress
    {
        get => GetValue(ShowProgressProperty);
        set => SetValue(ShowProgressProperty, value);
    }
}
