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

    /// <summary>Subscribe the just-loaded VM's edits to dirty-tracking (called from LoadPlaceable).</summary>
    private void TrackPlaceableEdits(ViewModels.PlaceableViewModel vm)
    {
        vm.PropertyChanged += (_, _) => MarkDirty();
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!_isDirty) return;

        // Cancel the close, prompt, then close programmatically on Save/Don't Save.
        e.Cancel = true;
        var result = await PromptSaveChangesAsync();
        if (result == SavePromptResult.Cancel) return;
        if (result == SavePromptResult.Save)
        {
            SavePlaceable();
            if (_isDirty) return; // save failed — keep the window open
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
        {
            SavePlaceable();
            return !_isDirty; // false if the save failed
        }
        return true; // Don't Save
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
