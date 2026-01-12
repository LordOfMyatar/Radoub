using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DialogEditor.Models;
using DialogEditor.Utils;
using DialogEditor.ViewModels;
using System;
using Radoub.Formats.Logging;
using System.IO;
using System.Threading.Tasks;

namespace DialogEditor.Services
{
    /// <summary>
    /// Handles debug and logging operations including log export, folder access, and scrap management.
    /// Extracted from MainWindow.axaml.cs to separate debug concerns from UI coordination.
    /// </summary>
    public class DebugAndLoggingHandler
    {
        private readonly MainViewModel _viewModel;
        private readonly Func<string, Control?> _findControl;
        private readonly Func<IStorageProvider?> _getStorageProvider;
        private readonly Action<string> _setStatusMessage;

        public DebugAndLoggingHandler(
            MainViewModel viewModel,
            Func<string, Control?> findControl,
            Func<IStorageProvider?> getStorageProvider,
            Action<string> setStatusMessage)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _findControl = findControl ?? throw new ArgumentNullException(nameof(findControl));
            _getStorageProvider = getStorageProvider ?? throw new ArgumentNullException(nameof(getStorageProvider));
            _setStatusMessage = setStatusMessage ?? throw new ArgumentNullException(nameof(setStatusMessage));
        }

        /// <summary>
        /// Opens the log folder in Windows Explorer
        /// </summary>
        public void OpenLogFolder()
        {
            try
            {
                // New location: ~/Radoub/Parley/Logs (matches toolset structure)
                var logFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Radoub", "Parley", "Logs");

                if (!Directory.Exists(logFolder))
                {
                    _setStatusMessage("Log folder does not exist yet");
                    return;
                }

                // Open folder in explorer
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = logFolder,
                    UseShellExecute = true
                });

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Opened log folder: {logFolder}");
            }
            catch (Exception ex)
            {
                _setStatusMessage($"Failed to open log folder: {ex.Message}");
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open log folder: {ex.Message}");
            }
        }

        /// <summary>
        /// Exports logs to a ZIP file with user-selected location
        /// </summary>
        public async Task<string?> ExportLogsAsync(Window owner)
        {
            try
            {
                // New location: ~/Radoub/Parley/Logs (matches toolset structure)
                var logFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Radoub", "Parley", "Logs");

                if (!Directory.Exists(logFolder))
                {
                    _setStatusMessage("No logs to export");
                    return null;
                }

                var storageProvider = _getStorageProvider();
                if (storageProvider == null)
                {
                    _setStatusMessage("Storage provider not available");
                    return null;
                }

                // Show save dialog for zip file
                var options = new FilePickerSaveOptions
                {
                    Title = "Export Logs for Support",
                    SuggestedFileName = $"Parley_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.zip",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("ZIP Archive")
                        {
                            Patterns = new[] { "*.zip" }
                        }
                    }
                };

                var file = await storageProvider.SaveFilePickerAsync(options);
                if (file == null) return null;

                var result = file.Path.LocalPath;

                // Create zip archive
                if (File.Exists(result))
                {
                    File.Delete(result);
                }

                System.IO.Compression.ZipFile.CreateFromDirectory(logFolder, result);

                _setStatusMessage($"Logs exported to: {result}");
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Exported logs to: {result}");

                // Offer to open folder
                ShowOpenFolderDialog(owner, result);

                return result;
            }
            catch (Exception ex)
            {
                _setStatusMessage($"Failed to export logs: {ex.Message}");
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to export logs: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Shows a dialog offering to open the folder containing the exported file
        /// </summary>
        private void ShowOpenFolderDialog(Window owner, string filePath)
        {
            var openFolderWindow = new Window
            {
                Title = "Logs Exported",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Logs exported successfully. Open the folder?",
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 20)
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Children =
                            {
                                new Button
                                {
                                    Content = "Yes",
                                    Padding = new Thickness(15, 5),
                                    Margin = new Thickness(0, 0, 10, 0)
                                },
                                new Button
                                {
                                    Content = "No",
                                    Padding = new Thickness(15, 5)
                                }
                            }
                        }
                    }
                }
            };

            var yesButton = ((StackPanel)((StackPanel)openFolderWindow.Content).Children[1]).Children[0] as Button;
            var noButton = ((StackPanel)((StackPanel)openFolderWindow.Content).Children[1]).Children[1] as Button;

            yesButton!.Click += (s, args) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Path.GetDirectoryName(filePath)!,
                    UseShellExecute = true
                });
                openFolderWindow.Close();
            };

            noButton!.Click += (s, args) => openFolderWindow.Close();

            openFolderWindow.Show();
        }

        /// <summary>
        /// Restores a node from scrap to the selected position
        /// </summary>
        public void RestoreFromScrap(TreeViewSafeNode? selectedNode)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "OnRestoreScrapClick called");

            if (_viewModel.SelectedScrapEntry == null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "No scrap entry selected");
                return;
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Selected scrap entry: {_viewModel.SelectedScrapEntry.NodeText}");
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Selected tree node: {selectedNode?.DisplayText ?? "null"} (Type: {selectedNode?.GetType().Name ?? "null"})");

            var restored = _viewModel.RestoreFromScrap(_viewModel.SelectedScrapEntry.Id, selectedNode);
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Restore result: {restored}");

            if (!restored)
            {
                // The RestoreFromScrap method will set an appropriate status message
            }
        }

        /// <summary>
        /// Swap NPC/PC roles for the selected scrap entry and its children.
        /// Entries become Replies and vice versa.
        /// </summary>
        public void SwapScrapRoles()
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "SwapScrapRoles called");

            if (_viewModel.SelectedScrapEntry == null)
            {
                _viewModel.StatusMessage = "Select a scrap entry to swap roles";
                return;
            }

            var swapped = _viewModel.SwapScrapRoles();
            if (swapped)
            {
                _viewModel.StatusMessage = "Swapped NPC/PC roles - ready to restore";
            }
            else
            {
                _viewModel.StatusMessage = "Failed to swap roles";
            }
        }

        /// <summary>
        /// Shows confirmation dialog and clears all scrap entries if confirmed
        /// </summary>
        public async Task ClearScrapAsync(Window owner)
        {
            var messageBox = new Window
            {
                Title = "Clear All Scrap",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Are you sure you want to clear all scrap entries?",
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 20)
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Children =
                            {
                                new Button
                                {
                                    Content = "Yes",
                                    Width = 80,
                                    Margin = new Thickness(0, 0, 10, 0),
                                    Name = "YesButton"
                                },
                                new Button
                                {
                                    Content = "No",
                                    Width = 80,
                                    IsDefault = true,
                                    Name = "NoButton"
                                }
                            }
                        }
                    }
                }
            };

            var stackPanel = messageBox.Content as StackPanel;
            var buttonPanel = stackPanel?.Children[1] as StackPanel;
            var yesButton = buttonPanel?.Children[0] as Button;
            var noButton = buttonPanel?.Children[1] as Button;

            if (yesButton != null)
            {
                yesButton.Click += (s, args) =>
                {
                    _viewModel.ClearAllScrap();
                    messageBox.Close();
                };
            }

            if (noButton != null)
            {
                noButton.Click += (s, args) => messageBox.Close();
            }

            await messageBox.ShowDialog(owner);
        }

        /// <summary>
        /// Adds a debug message to the debug console
        /// </summary>
        public void AddDebugMessage(string message)
        {
            _viewModel.AddDebugMessage(message);
        }
    }
}
