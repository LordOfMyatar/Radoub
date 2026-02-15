using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Ifo;

namespace RadoubLauncher.ViewModels;

// Version edit command and version compatibility dialog
public partial class ModuleEditorViewModel
{
    [RelayCommand]
    private async Task EditVersionAsync()
    {
        if (IsVersionUnlocked)
        {
            // Lock the version field
            IsVersionUnlocked = false;
            return;
        }

        // Check for EE-specific fields that have values
        if (_ifoFile == null)
        {
            IsVersionUnlocked = true;
            return;
        }

        // Build current IFO state from ViewModel for accurate check
        UpdateIfoFromViewModel();

        var eeFields = IfoVersionRequirements.GetPopulatedEeFields(_ifoFile);

        if (eeFields.Count == 0)
        {
            // No EE fields populated, just unlock
            IsVersionUnlocked = true;
            StatusText = "Version field unlocked. No EE-specific fields detected.";
            return;
        }

        // Show warning dialog with list of EE fields
        var fieldList = string.Join("\n", eeFields.Select(f =>
            $"  - {f.DisplayName}: \"{f.Value}\" (requires {f.MinVersion}+)"));

        var requiredVersion = IfoVersionRequirements.GetRequiredVersion(_ifoFile);

        var message = $"This module contains NWN:EE-specific data:\n\n{fieldList}\n\n" +
            $"Minimum required version: {requiredVersion}\n\n" +
            "If you set the minimum version lower than required:\n" +
            "- The game will still load the module (unknown fields are ignored)\n" +
            "- EE-only scripts will not fire on older versions\n" +
            "- Opening in the 1.69 Aurora Toolset and saving will permanently remove these fields\n\n" +
            "Do you want to unlock the version field?";

        if (_parentWindow == null) return;

        // Build the dialog content
        var messageText = new Avalonia.Controls.TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var unlockButton = new Avalonia.Controls.Button
        {
            Content = "Unlock Version",
            Margin = new Avalonia.Thickness(8, 0, 0, 0),
            Padding = new Avalonia.Thickness(16, 6)
        };

        var cancelButton = new Avalonia.Controls.Button
        {
            Content = "Cancel",
            Padding = new Avalonia.Thickness(16, 6)
        };

        var buttonPanel = new Avalonia.Controls.StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 16, 0, 0),
            Children = { cancelButton, unlockButton }
        };

        var contentPanel = new Avalonia.Controls.StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Children =
            {
                new Avalonia.Controls.ScrollViewer
                {
                    MaxHeight = 300,
                    Content = messageText
                },
                buttonPanel
            }
        };

        var dialog = new Avalonia.Controls.Window
        {
            Title = "Version Compatibility Warning",
            Width = 500,
            SizeToContent = Avalonia.Controls.SizeToContent.Height,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = contentPanel
        };

        var result = false;
        unlockButton.Click += (_, _) => { result = true; dialog.Close(); };
        cancelButton.Click += (_, _) => { result = false; dialog.Close(); };

        await dialog.ShowDialog(_parentWindow);

        if (result)
        {
            IsVersionUnlocked = true;
            StatusText = $"Version unlocked. Current data requires {requiredVersion}+";
        }
    }
}
