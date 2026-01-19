using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using System.Threading.Tasks;

namespace Quartermaster.Views.Helpers;

/// <summary>
/// Static helper class for creating common dialogs.
/// Extracted from MainWindow.axaml.cs for reusability (#582).
/// </summary>
public static class DialogHelper
{
    /// <summary>
    /// Shows a dialog asking user what to do with unsaved changes.
    /// </summary>
    /// <param name="parent">Parent window for centering</param>
    /// <returns>"Save", "Discard", or "Cancel"</returns>
    public static async Task<string> ShowUnsavedChangesDialog(Window parent)
    {
        var dialog = new Window
        {
            Title = "Unsaved Changes",
            Width = 350,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var result = "Cancel";

        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 15 };
        panel.Children.Add(new TextBlock { Text = "You have unsaved changes. What would you like to do?" });

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        var saveButton = new Button { Content = "Save" };
        saveButton.Click += (s, e) => { result = "Save"; dialog.Close(); };

        var discardButton = new Button { Content = "Discard" };
        discardButton.Click += (s, e) => { result = "Discard"; dialog.Close(); };

        var cancelButton = new Button { Content = "Cancel" };
        cancelButton.Click += (s, e) => { result = "Cancel"; dialog.Close(); };

        buttonPanel.Children.Add(saveButton);
        buttonPanel.Children.Add(discardButton);
        buttonPanel.Children.Add(cancelButton);
        panel.Children.Add(buttonPanel);

        dialog.Content = panel;
        await dialog.ShowDialog(parent);

        return result;
    }

    /// <summary>
    /// Shows an informational message dialog with a title and message.
    /// Supports longer messages with text wrapping.
    /// </summary>
    public static async Task ShowMessageDialog(Window parent, string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 450,
            SizeToContent = SizeToContent.Height,
            MinHeight = 150,
            MaxHeight = 350,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 15 };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MaxWidth = 400
        });

        var button = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        button.Click += (s, e) => dialog.Close();
        panel.Children.Add(button);

        dialog.Content = panel;
        await dialog.ShowDialog(parent);
    }

    /// <summary>
    /// Shows an error dialog with a title and message.
    /// </summary>
    public static async Task ShowErrorDialog(Window parent, string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 15 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });

        var button = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        button.Click += (s, e) => dialog.Close();
        panel.Children.Add(button);

        dialog.Content = panel;
        await dialog.ShowDialog(parent);
    }

    /// <summary>
    /// Shows the About dialog for Quartermaster.
    /// Uses Show() instead of ShowDialog() for non-blocking behavior.
    /// </summary>
    public static void ShowAboutDialog(Window parent)
    {
        var dialog = new Window
        {
            Title = "About Quartermaster",
            Width = 350,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 10,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        panel.Children.Add(new TextBlock
        {
            Text = "Quartermaster",
            FontSize = GetTitleFontSize(),
            FontWeight = Avalonia.Media.FontWeight.Bold
        });
        panel.Children.Add(new TextBlock { Text = "Creature and Inventory Editor" });
        panel.Children.Add(new TextBlock { Text = "for Neverwinter Nights" });
        panel.Children.Add(new TextBlock { Text = "Version 0.1.3-alpha" });
        panel.Children.Add(new TextBlock
        {
            Text = "Part of the Radoub Toolset",
            Margin = new Avalonia.Thickness(0, 10, 0, 0)
        });

        var button = new Button
        {
            Content = "OK",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 10, 0, 0)
        };
        button.Click += (s, e) => dialog.Close();
        panel.Children.Add(button);

        dialog.Content = panel;
        dialog.Show(parent);
    }

    /// <summary>
    /// Shows an OK/Cancel confirmation dialog with a warning icon.
    /// </summary>
    /// <param name="parent">Parent window for centering</param>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Dialog message</param>
    /// <returns>True if user clicked OK, false otherwise</returns>
    public static async Task<bool> ShowConfirmationDialog(Window parent, string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            SizeToContent = SizeToContent.Height,
            MinHeight = 150,
            MaxHeight = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var result = false;

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 15 };

        // Warning icon + message
        var messagePanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10
        };
        messagePanel.Children.Add(new TextBlock
        {
            Text = "⚠",
            FontSize = 24,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        });
        messagePanel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MaxWidth = 320
        });
        panel.Children.Add(messagePanel);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        var okButton = new Button { Content = "OK", Width = 80 };
        okButton.Click += (s, e) => { result = true; dialog.Close(); };

        var cancelButton = new Button { Content = "Cancel", Width = 80 };
        cancelButton.Click += (s, e) => { result = false; dialog.Close(); };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        panel.Children.Add(buttonPanel);

        dialog.Content = panel;
        await dialog.ShowDialog(parent);

        return result;
    }

    /// <summary>
    /// Gets the title font size from theme resources, with fallback.
    /// </summary>
    private static double GetTitleFontSize()
    {
        var app = Application.Current;
        if (app?.Resources.TryGetResource("FontSizeTitle", ThemeVariant.Default, out var size) == true
            && size is double fontSize)
            return fontSize;
        return 24; // Default fallback
    }
}
