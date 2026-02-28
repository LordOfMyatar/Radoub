using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// FileMenuController partial: Dialog helpers and filename validation.
    /// Split from FileMenuController.cs (#1540).
    /// </summary>
    public partial class FileMenuController
    {
        #region Dialog Helpers

        private void ShowDuplicateKeysWarning()
        {
            var msgBox = new Window
            {
                Title = "Cannot Save",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 15,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Duplicate parameter keys detected.\n\nFix the duplicate keys (shown with red borders) before saving.",
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap
                        },
                        new Button
                        {
                            Content = "OK",
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
                        }
                    }
                }
            };

            var okButton = ((StackPanel)msgBox.Content).Children.OfType<Button>().First();
            okButton.Click += (s, args) => msgBox.Close();
            msgBox.Show(_window);
        }

        /// <summary>
        /// Show warning about unsupported characters in dialog text.
        /// Non-blocking - save proceeds after user acknowledges.
        /// Issue #152: Warn users about characters that won't render in NWN.
        /// </summary>
        private void ShowUnsupportedCharactersWarning(TextValidationResult validation)
        {
            var grouped = validation.GroupByNode();
            var summaryText = $"Found {validation.TotalCharacterCount} unsupported character(s) in {validation.AffectedNodeCount} node(s).\n\n" +
                              "These characters may not display correctly in Neverwinter Nights:\n\n";

            // Show first few examples
            var examples = validation.Warnings.Take(5).ToList();
            foreach (var warning in examples)
            {
                summaryText += $"• {warning.NodeType}[{warning.NodeIndex}]: '{warning.Character.Character}' ({warning.Character.Description})\n";
            }

            if (validation.Warnings.Count > 5)
            {
                summaryText += $"\n...and {validation.Warnings.Count - 5} more.\n";
            }

            summaryText += "\nThe file will still be saved, but affected text may appear as boxes or ? in-game.";

            var msgBox = new Window
            {
                Title = "⚠️ Unsupported Characters",
                Width = 500,
                MinHeight = 200,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 15,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = summaryText,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap
                        },
                        new Button
                        {
                            Content = "OK",
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
                        }
                    }
                }
            };

            var okButton = ((StackPanel)msgBox.Content).Children.OfType<Button>().First();
            okButton.Click += (s, args) => msgBox.Close();
            msgBox.Show(_window);
        }

        /// <summary>
        /// Show confirmation dialog and return user's choice.
        /// </summary>
        private async Task<bool> ShowConfirmDialog(string title, string message)
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

            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 560,
                Margin = new Thickness(0, 0, 0, 20)
            });

            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Spacing = 10
            };

            var result = false;

            var yesButton = new Button { Content = "Yes", Width = 80 };
            yesButton.Click += (s, e) => { result = true; dialog.Close(); };

            var noButton = new Button { Content = "No", Width = 80 };
            noButton.Click += (s, e) => { result = false; dialog.Close(); };

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);
            panel.Children.Add(buttonPanel);
            dialog.Content = panel;

            await dialog.ShowDialog(_window);
            return result;
        }

        /// <summary>
        /// Show save error dialog with Save As option.
        /// </summary>
        private async Task<bool> ShowSaveErrorDialog(string errorMessage)
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

            await dialog.ShowDialog(_window);
            return result;
        }

        #endregion

        #region Filename Validation (#826)

        /// <summary>
        /// Validates filename length for Aurora Engine compatibility.
        /// Returns true if valid, false if blocked.
        /// Shows error dialog if filename exceeds 16 characters.
        /// </summary>
        public Task<bool> ValidateFilenameAsync(string filePath)
        {
            var filename = Path.GetFileNameWithoutExtension(filePath);
            if (filename.Length <= MaxAuroraFilenameLength)
            {
                return Task.FromResult(true);
            }

            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Filename '{filename}' is {filename.Length} characters, exceeds Aurora Engine limit of {MaxAuroraFilenameLength}");

            ShowFilenameTooLongError(filename);
            return Task.FromResult(false);
        }

        /// <summary>
        /// Shows error dialog for filename exceeding Aurora Engine limit (non-modal).
        /// </summary>
        private void ShowFilenameTooLongError(string filename)
        {
            var dialog = new Window
            {
                Title = "Filename Too Long",
                MinWidth = 450,
                MaxWidth = 550,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            panel.Children.Add(new TextBlock
            {
                Text = $"Filename '{filename}' is {filename.Length} characters.\n\n" +
                       $"Aurora Engine maximum is {MaxAuroraFilenameLength} characters.\n\n" +
                       "The game cannot load files with longer names.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 510,
                Margin = new Thickness(0, 0, 0, 20)
            });

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            okButton.Click += (s, e) => dialog.Close();
            panel.Children.Add(okButton);

            dialog.Content = panel;
            dialog.Show(_window);  // Non-modal info dialog
        }

        #endregion
    }
}
