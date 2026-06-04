using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Radoub.Formats.Search.Rename;

namespace RadoubLauncher.Views;

/// <summary>
/// Confirmation dialog shown before Marlinspike executes a filename/resref rename
/// (#2346). Filename rename previously ran with no review step — only the
/// auto-suffix collision dialog appeared, and only on a name collision. This
/// surfaces the full old → new list so a typo in the replacement text can be
/// caught before files are renamed and references rewritten.
/// </summary>
public partial class RenameConfirmDialog : Window
{
    /// <summary>True when the user confirmed the rename; false on cancel/close.</summary>
    public bool Confirmed { get; private set; }

    public RenameConfirmDialog()
    {
        InitializeComponent();
    }

    public RenameConfirmDialog(IReadOnlyList<ResRefRenamePlan> plans)
        : this()
    {
        var count = plans.Count;
        HeaderText.Text = count == 1
            ? "Rename 1 file?"
            : $"Rename {count} files?";

        RenameList.ItemsSource = plans
            .Select(p =>
            {
                var ext = Path.GetExtension(p.SourceFilePath);
                return $"{p.OldName}{ext}  →  {p.NewName}{ext}";
            })
            .ToList();
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
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
