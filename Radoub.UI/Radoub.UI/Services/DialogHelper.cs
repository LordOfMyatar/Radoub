using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Threading.Tasks;

namespace Radoub.UI.Services;

/// <summary>
/// Shared dialog helper for creating consistent modal dialogs across all Radoub tools.
/// Provides theme-aware styling and common dialog patterns.
///
/// Usage:
///   await DialogHelper.ShowConfirmAsync(parent, "Delete?", "Are you sure?");
///   var result = await DialogHelper.ShowUnsavedChangesAsync(parent);
///   DialogHelper.ShowMessage(parent, "Success", "Operation completed.");
///   DialogHelper.ShowError(parent, "Error", "Something went wrong.");
/// </summary>
public static class DialogHelper
{
    /// <summary>
    /// Apply modal dialog styling for better visibility against the main window.
    /// Uses ThemeSidebar background and ThemeBorder accent border.
    /// </summary>
    public static void ApplyModalStyling(Window dialog)
    {
        var app = Application.Current;
        if (app?.Resources == null) return;

        // Use sidebar color for dialog background (slightly different from main window)
        if (app.Resources.TryGetResource("ThemeSidebar", app.ActualThemeVariant, out var sidebarObj)
            && sidebarObj is SolidColorBrush sidebarBrush)
        {
            dialog.Background = sidebarBrush;
        }
        else if (app.Resources.TryGetResource("ThemeBackground", app.ActualThemeVariant, out var bgObj)
            && bgObj is SolidColorBrush bgBrush)
        {
            dialog.Background = bgBrush;
        }

        // Add accent-colored border for visual distinction
        if (app.Resources.TryGetResource("ThemeBorder", app.ActualThemeVariant, out var borderObj)
            && borderObj is SolidColorBrush borderBrush)
        {
            dialog.BorderBrush = borderBrush;
            dialog.BorderThickness = new Thickness(2);
        }
        else if (app.Resources.TryGetResource("SystemAccentColor", app.ActualThemeVariant, out var accentObj)
            && accentObj is Color accentColor)
        {
            dialog.BorderBrush = new SolidColorBrush(accentColor);
            dialog.BorderThickness = new Thickness(2);
        }
    }

    #region Confirmation Dialogs (Modal)

    /// <summary>
    /// Shows a Yes/No confirmation dialog.
    /// </summary>
    /// <param name="parent">Parent window for centering</param>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Message to display</param>
    /// <param name="showDontAskAgain">If true, shows "Don't show this again" checkbox</param>
    /// <param name="onDontAskAgain">Callback when user checks "Don't ask again" and clicks Yes</param>
    /// <returns>True if user clicked Yes, false if No</returns>
    public static async Task<bool> ShowConfirmAsync(
        Window parent,
        string title,
        string message,
        bool showDontAskAgain = false,
        Action? onDontAskAgain = null)
    {
        var dialog = new Window
        {
            Title = title,
            MinWidth = 400,
            MaxWidth = 600,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        ApplyModalStyling(dialog);

        var result = false;

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 560,
            Margin = new Thickness(0, 0, 0, 20)
        });

        // "Don't show this again" checkbox
        CheckBox? dontAskCheckBox = null;
        if (showDontAskAgain)
        {
            dontAskCheckBox = new CheckBox
            {
                Content = "Don't show this again",
                Margin = new Thickness(0, 0, 0, 20)
            };
            panel.Children.Add(dontAskCheckBox);
        }

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 10
        };

        var yesButton = new Button { Content = "Yes", Width = 80 };
        yesButton.Click += (s, e) =>
        {
            result = true;
            if (dontAskCheckBox?.IsChecked == true)
            {
                onDontAskAgain?.Invoke();
            }
            dialog.Close();
        };

        var noButton = new Button { Content = "No", Width = 80 };
        noButton.Click += (s, e) => { result = false; dialog.Close(); };

        buttonPanel.Children.Add(yesButton);
        buttonPanel.Children.Add(noButton);
        panel.Children.Add(buttonPanel);

        dialog.Content = panel;
        await dialog.ShowDialog(parent);
        return result;
    }

    /// <summary>
    /// Shows an OK/Cancel confirmation dialog.
    /// </summary>
    /// <param name="parent">Parent window for centering</param>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Message to display</param>
    /// <returns>True if user clicked OK, false if Cancel</returns>
    public static async Task<bool> ShowOkCancelAsync(Window parent, string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            SizeToContent = SizeToContent.Height,
            MinHeight = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        ApplyModalStyling(dialog);

        var result = false;

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 15 };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 360
        });

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 10
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
    /// Shows an OK/Cancel confirmation dialog with a warning icon.
    /// </summary>
    /// <param name="parent">Parent window for centering</param>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Message to display (supports longer text with scrolling)</param>
    /// <returns>True if user clicked OK, false if Cancel</returns>
    public static async Task<bool> ShowWarningConfirmAsync(Window parent, string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 450,
            SizeToContent = SizeToContent.Height,
            MinHeight = 180,
            MaxHeight = 450,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        ApplyModalStyling(dialog);

        var result = false;

        var outerPanel = new DockPanel { Margin = new Thickness(20) };

        // Buttons at bottom
        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 15, 0, 0)
        };
        DockPanel.SetDock(buttonPanel, Dock.Bottom);

        var okButton = new Button { Content = "OK", Width = 80 };
        okButton.Click += (s, e) => { result = true; dialog.Close(); };

        var cancelButton = new Button { Content = "Cancel", Width = 80 };
        cancelButton.Click += (s, e) => { result = false; dialog.Close(); };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        outerPanel.Children.Add(buttonPanel);

        // Scrollable message area
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            MaxHeight = 300
        };

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
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 360
        });

        scrollViewer.Content = messagePanel;
        outerPanel.Children.Add(scrollViewer);

        dialog.Content = outerPanel;
        await dialog.ShowDialog(parent);
        return result;
    }

    /// <summary>
    /// Shows a Save/Discard/Cancel dialog for unsaved changes.
    /// </summary>
    /// <param name="parent">Parent window for centering</param>
    /// <returns>"Save", "Discard", or "Cancel"</returns>
    public static async Task<string> ShowUnsavedChangesAsync(Window parent)
    {
        var dialog = new Window
        {
            Title = "Unsaved Changes",
            Width = 350,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        ApplyModalStyling(dialog);

        var result = "Cancel";

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 15 };
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
    /// Shows an error dialog with Save As option.
    /// Used when save operation fails.
    /// </summary>
    /// <param name="parent">Parent window for centering</param>
    /// <param name="errorMessage">Error message to display</param>
    /// <returns>True if user wants to Save As, false to cancel</returns>
    public static async Task<bool> ShowSaveErrorAsync(Window parent, string errorMessage)
    {
        var dialog = new Window
        {
            Title = "Save Failed",
            MinWidth = 400,
            MaxWidth = 500,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        ApplyModalStyling(dialog);

        var result = false;

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = errorMessage,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 460,
            Margin = new Thickness(0, 0, 0, 20)
        });

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 10
        };

        var saveAsButton = new Button { Content = "Save As...", Width = 100 };
        saveAsButton.Click += (s, e) => { result = true; dialog.Close(); };

        var cancelButton = new Button { Content = "Cancel", Width = 80 };
        cancelButton.Click += (s, e) => { result = false; dialog.Close(); };

        buttonPanel.Children.Add(saveAsButton);
        buttonPanel.Children.Add(cancelButton);
        panel.Children.Add(buttonPanel);

        dialog.Content = panel;
        await dialog.ShowDialog(parent);
        return result;
    }

    #endregion

    #region Message Dialogs (Non-Modal)

    /// <summary>
    /// Shows an informational message dialog (non-modal).
    /// </summary>
    /// <param name="parent">Parent window for positioning</param>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Message to display</param>
    public static void ShowMessage(Window parent, string title, string message)
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

        ApplyModalStyling(dialog);

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 15 };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 400
        });

        var button = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        button.Click += (s, e) => dialog.Close();
        panel.Children.Add(button);

        dialog.Content = panel;
        dialog.Show(parent);
    }

    /// <summary>
    /// Shows an error message dialog (non-modal).
    /// </summary>
    /// <param name="parent">Parent window for positioning</param>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Error message to display</param>
    public static void ShowError(Window parent, string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            SizeToContent = SizeToContent.Height,
            MinHeight = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        ApplyModalStyling(dialog);

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 15 };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 360
        });

        var button = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        button.Click += (s, e) => dialog.Close();
        panel.Children.Add(button);

        dialog.Content = panel;
        dialog.Show(parent);
    }

    #endregion
}
