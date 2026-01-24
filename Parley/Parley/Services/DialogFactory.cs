using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using System.Threading.Tasks;

namespace DialogEditor.Services
{
    /// <summary>
    /// Factory for creating common dialog windows.
    /// Extracted from MainWindow.axaml.cs (#524) to make dialogs reusable and testable.
    /// </summary>
    public class DialogFactory
    {
        private readonly Window _owner;

        public DialogFactory(Window owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// Apply modal dialog styling for better visibility (#453).
        /// Adds distinct border and ensures theme-aware background.
        /// </summary>
        private static void ApplyModalStyling(Window dialog)
        {
            // Get theme colors from application resources
            var app = Application.Current;
            if (app?.Resources != null)
            {
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
        }

        /// <summary>
        /// Shows a Yes/No confirmation dialog.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Message to display</param>
        /// <param name="showDontAskAgain">If true, shows "Don't show this again" checkbox (Issue #14)</param>
        /// <returns>True if user clicked Yes, false if No</returns>
        public async Task<bool> ShowConfirmDialogAsync(string title, string message, bool showDontAskAgain = false)
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

            // Apply modal styling for better visibility (#453)
            ApplyModalStyling(dialog);

            var panel = new StackPanel { Margin = new Thickness(20) };

            panel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 560, // MaxWidth - margins
                Margin = new Thickness(0, 0, 0, 20)
            });

            // "Don't show this again" checkbox (Issue #14)
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

            var result = false;

            var yesButton = new Button { Content = "Yes", Width = 80 };
            yesButton.Click += (s, e) =>
            {
                result = true;
                // Save "don't ask again" preference if checkbox is checked (Issue #14)
                if (dontAskCheckBox?.IsChecked == true)
                {
                    SettingsService.Instance.ShowDeleteConfirmation = false;
                }
                dialog.Close();
            };

            var noButton = new Button { Content = "No", Width = 80 };
            noButton.Click += (s, e) => { result = false; dialog.Close(); };

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);

            panel.Children.Add(buttonPanel);
            dialog.Content = panel;

            await dialog.ShowDialog(_owner);
            return result;
        }

        /// <summary>
        /// Issue #8: Shows error dialog with option to Save As when save fails.
        /// </summary>
        /// <param name="errorMessage">Error message to display</param>
        /// <returns>True if user wants to Save As, false to cancel</returns>
        public async Task<bool> ShowSaveErrorDialogAsync(string errorMessage)
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

            // Apply modal styling for better visibility (#453)
            ApplyModalStyling(dialog);

            var panel = new StackPanel { Margin = new Thickness(20) };

            panel.Children.Add(new TextBlock
            {
                Text = errorMessage,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 460,
                Margin = new Thickness(0, 0, 0, 20)
            });

            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Spacing = 10
            };

            var result = false;

            var saveAsButton = new Button { Content = "Save As...", Width = 100 };
            saveAsButton.Click += (s, e) => { result = true; dialog.Close(); };

            var cancelButton = new Button { Content = "Cancel", Width = 80 };
            cancelButton.Click += (s, e) => { result = false; dialog.Close(); };

            buttonPanel.Children.Add(saveAsButton);
            buttonPanel.Children.Add(cancelButton);

            panel.Children.Add(buttonPanel);
            dialog.Content = panel;

            await dialog.ShowDialog(_owner);
            return result;
        }
    }
}
