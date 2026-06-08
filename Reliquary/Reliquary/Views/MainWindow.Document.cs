using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PlaceableEditor.Views;

/// <summary>
/// Document dirty-tracking + save-prompt flow for Reliquary's MainWindow. The editor binds panels
/// two-way to <see cref="ViewModels.PlaceableViewModel"/>, so every edit raises the VM's
/// PropertyChanged — the host marks the document dirty there (guarded by <c>_isLoading</c> so the
/// load-time rebind does not count as an edit). Open / browser-select / window-close all gate on
/// the dirty flag and prompt Save / Don't Save / Cancel (Relique pattern). Save prompt is a modal
/// confirmation dialog — the one modal allowed for destructive/data-loss actions.
/// </summary>
public partial class MainWindow
{
    /// <summary>Mark the document dirty unless a load is in progress.</summary>
    private void MarkDirty()
    {
        if (_isLoading) return;
        _documentState.MarkDirty();
    }

    /// <summary>Refresh the title bar from document state (ClearDirty only fires on dirty→clean).</summary>
    private void UpdateTitle() => Title = _documentState.GetTitle();

    /// <summary>Subscribe the just-loaded VM's edits to dirty-tracking (called from LoadPlaceable).</summary>
    private void TrackPlaceableEdits(ViewModels.PlaceableViewModel vm)
    {
        vm.PropertyChanged += (_, _) => MarkDirty();
    }

    /// <summary>
    /// Clear the loading guard only after pending UI events drain. Combo SelectionChanged from
    /// populating the appearance/faction/category combos on load can dispatch on a later UI tick;
    /// resetting <c>_isLoading</c> synchronously in the load finally lets those deferred events slip
    /// past <see cref="MarkDirty"/> and mark a freshly-opened document dirty (#2416 follow-up). Post
    /// the reset at Background priority so it runs after those events, which still see _isLoading=true.
    /// </summary>
    private void ScheduleLoadingReset()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(
            () => _isLoading = false,
            Avalonia.Threading.DispatcherPriority.Background);
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        SaveWindowPosition(); // persist window size/position every close (#window-size)

        if (!_isDirty) return;

        // Cancel the close, prompt, then close programmatically on Save/Don't Save.
        e.Cancel = true;
        var result = await PromptSaveChangesAsync();
        if (result == SavePromptResult.Cancel) return;
        if (result == SavePromptResult.Save)
        {
            if (!await SavePlaceableAsync()) return; // save failed/cancelled — keep the window open
        }

        _documentState.ClearDirty();
        Close();
    }

    /// <summary>
    /// Returns true if it is safe to discard the current document (clean, or the user chose
    /// Save/Don't Save). Saves on Save. Returns false on Cancel or a failed save.
    /// </summary>
    private async Task<bool> ConfirmDiscardAsync()
    {
        if (!_isDirty) return true;

        var result = await PromptSaveChangesAsync();
        if (result == SavePromptResult.Cancel) return false;
        if (result == SavePromptResult.Save)
            return await SavePlaceableAsync(); // false if the save failed/was cancelled
        return true; // Don't Save
    }

    /// <summary>
    /// Prompt for a new placeable's name before creating it (#2426). Returns the trimmed name, or
    /// null if the user cancels / leaves it blank. The caller derives Tag/ResRef and saves the file
    /// immediately so the new placeable is backed by disk from the start (no pre-first-save data loss).
    /// </summary>
    private async Task<string?> PromptNewPlaceableNameAsync()
    {
        var dialog = new Window
        {
            Title = "New Placeable",
            Width = 360,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = this.FindResource("ThemeBackground") as IBrush
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "Name for the new placeable:", TextWrapping = TextWrapping.Wrap });
        var nameBox = new TextBox { Watermark = "e.g. Iron Chest" };
        panel.Children.Add(nameBox);

        string? result = null;
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        var okBtn = new Button { Content = "Create", IsDefault = true };
        okBtn.Click += (_, _) =>
        {
            var n = nameBox.Text?.Trim();
            if (string.IsNullOrEmpty(n)) return; // require a name; keep dialog open
            result = n;
            dialog.Close();
        };
        var cancelBtn = new Button { Content = "Cancel", IsCancel = true };
        cancelBtn.Click += (_, _) => { result = null; dialog.Close(); };
        buttons.Children.Add(okBtn);
        buttons.Children.Add(cancelBtn);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        nameBox.AttachedToVisualTree += (_, _) => nameBox.Focus();
        await dialog.ShowDialog(this);
        return result;
    }

    private async Task<SavePromptResult> PromptSaveChangesAsync()
    {
        var dialog = new Window
        {
            Title = "Save Changes?",
            Width = 360,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = this.FindResource("ThemeBackground") as IBrush
        };

        var result = SavePromptResult.Cancel;

        var panel = new StackPanel { Margin = new Avalonia.Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = "Save changes to the current placeable before continuing?",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, 0, 0, 16)
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        var saveBtn = new Button { Content = "Save" };
        saveBtn.Click += (_, _) => { result = SavePromptResult.Save; dialog.Close(); };
        var dontSaveBtn = new Button { Content = "Don't Save" };
        dontSaveBtn.Click += (_, _) => { result = SavePromptResult.DontSave; dialog.Close(); };
        var cancelBtn = new Button { Content = "Cancel" };
        cancelBtn.Click += (_, _) => { result = SavePromptResult.Cancel; dialog.Close(); };

        buttons.Children.Add(saveBtn);
        buttons.Children.Add(dontSaveBtn);
        buttons.Children.Add(cancelBtn);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        await dialog.ShowDialog(this);
        return result;
    }
}

internal enum SavePromptResult
{
    Save,
    DontSave,
    Cancel
}
