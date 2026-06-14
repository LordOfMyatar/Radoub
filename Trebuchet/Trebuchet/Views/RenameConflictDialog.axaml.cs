using Avalonia.Controls;
using Avalonia.Interactivity;
using RadoubLauncher.Services;

namespace RadoubLauncher.Views;

/// <summary>
/// Consolidated pre-execute conflict dialog for filename/ResRef rename (#2179,
/// #2182). Replaces the old per-collision AutoSuffixCollisionDialog sequence:
/// all conflicts (auto-suffixed collisions + validator-skipped names) are shown
/// once, with the "why" inline. Continue applies the clean + auto-suffixed
/// renames; skipped entries are advisory (already excluded from execution).
/// </summary>
public partial class RenameConflictDialog : Window
{
    /// <summary>True when the user chose Continue; false on cancel/close.</summary>
    public bool Confirmed { get; private set; }

    public RenameConflictDialog()
    {
        InitializeComponent();
    }

    public RenameConflictDialog(RenameConflictSummary summary) : this()
    {
        WillRenameList.ItemsSource = summary.WillRename;
        AutoSuffixedList.ItemsSource = summary.AutoSuffixed;
        SkippedList.ItemsSource = summary.Skipped;

        WillRenameSection.IsVisible = summary.WillRename.Count > 0;
        AutoSuffixedSection.IsVisible = summary.AutoSuffixed.Count > 0;
        SkippedSection.IsVisible = summary.Skipped.Count > 0;
    }

    private void OnContinueClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}
