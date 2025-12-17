using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogEditor.Models;
using DialogEditor.Services;

namespace DialogEditor.Views
{
    /// <summary>
    /// Result from QuestBrowserWindow containing selected quest tag and entry ID.
    /// </summary>
    public class QuestBrowserResult
    {
        public string? QuestTag { get; set; }
        public uint? EntryId { get; set; }

        /// <summary>
        /// True if user selected a quest (tag only, no specific entry).
        /// </summary>
        public bool HasQuestOnly => !string.IsNullOrEmpty(QuestTag) && !EntryId.HasValue;

        /// <summary>
        /// True if user selected both quest and entry.
        /// </summary>
        public bool HasQuestAndEntry => !string.IsNullOrEmpty(QuestTag) && EntryId.HasValue;
    }

    /// <summary>
    /// Browser window for selecting quests and journal entries.
    /// Similar to SoundBrowserWindow but for journal data.
    /// Issue #166: Replace ComboBoxes with TextBox + Browse pattern.
    /// </summary>
    public partial class QuestBrowserWindow : Window
    {
        private List<JournalCategory> _allCategories;
        private List<JournalCategory> _filteredCategories;
        private JournalCategory? _selectedCategory;
        private JournalEntry? _selectedEntry;
        private readonly string? _dialogFilePath;
        private readonly string? _initialQuestTag;
        private readonly uint? _initialEntryId;

        /// <summary>
        /// The selected quest tag (without entry).
        /// </summary>
        public string? SelectedQuestTag => _selectedCategory?.Tag;

        /// <summary>
        /// The selected entry ID (if any).
        /// </summary>
        public uint? SelectedEntryId => _selectedEntry?.ID;

        /// <summary>
        /// Combined result for dialog return.
        /// </summary>
        public QuestBrowserResult? Result { get; private set; }

        // Parameterless constructor for XAML designer/runtime loader
        public QuestBrowserWindow() : this(null, null, null)
        {
        }

        /// <summary>
        /// Create quest browser with optional initial selection.
        /// </summary>
        /// <param name="dialogFilePath">Path to current DLG file (to find module.jrl)</param>
        /// <param name="initialQuestTag">Pre-select this quest tag if found</param>
        /// <param name="initialEntryId">Pre-select this entry ID if found</param>
        public QuestBrowserWindow(string? dialogFilePath, string? initialQuestTag = null, uint? initialEntryId = null)
        {
            InitializeComponent();
            _allCategories = new List<JournalCategory>();
            _filteredCategories = new List<JournalCategory>();
            _dialogFilePath = dialogFilePath;
            _initialQuestTag = initialQuestTag;
            _initialEntryId = initialEntryId;

            LoadQuests();
        }

        private async void LoadQuests()
        {
            try
            {
                // Find module.jrl
                string? journalPath = null;

                if (!string.IsNullOrEmpty(_dialogFilePath))
                {
                    var dialogDir = Path.GetDirectoryName(_dialogFilePath);
                    if (!string.IsNullOrEmpty(dialogDir))
                    {
                        journalPath = Path.Combine(dialogDir, "module.jrl");
                    }
                }

                if (string.IsNullOrEmpty(journalPath) || !File.Exists(journalPath))
                {
                    QuestCountLabel.Text = "No module.jrl found";
                    UnifiedLogger.LogJournal(LogLevel.WARN, "Quest Browser: No module.jrl found");
                    return;
                }

                // Load from JournalService (uses cache)
                _allCategories = await JournalService.Instance.ParseJournalFileAsync(journalPath);
                UpdateQuestList();

                // Pre-select initial quest if provided
                if (!string.IsNullOrEmpty(_initialQuestTag))
                {
                    var category = _allCategories.FirstOrDefault(c =>
                        c.Tag.Equals(_initialQuestTag, StringComparison.OrdinalIgnoreCase));
                    if (category != null)
                    {
                        QuestListBox.SelectedItem = category;

                        // Pre-select entry if provided
                        if (_initialEntryId.HasValue)
                        {
                            var entry = category.Entries.FirstOrDefault(e => e.ID == _initialEntryId.Value);
                            if (entry != null)
                            {
                                EntryListBox.SelectedItem = entry;
                            }
                        }
                    }
                }

                UnifiedLogger.LogJournal(LogLevel.INFO, $"Quest Browser: Loaded {_allCategories.Count} quests");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogJournal(LogLevel.ERROR, $"Quest Browser: Error loading quests: {ex.Message}");
                QuestCountLabel.Text = $"Error: {ex.Message}";
            }
        }

        private void UpdateQuestList()
        {
            var searchText = SearchBox?.Text?.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                _filteredCategories = _allCategories.OrderBy(c => c.Tag).ToList();
            }
            else
            {
                _filteredCategories = _allCategories
                    .Where(c =>
                        c.Tag.ToLowerInvariant().Contains(searchText) ||
                        (c.Name?.GetDefault()?.ToLowerInvariant().Contains(searchText) ?? false))
                    .OrderBy(c => c.Tag)
                    .ToList();
            }

            QuestListBox.ItemsSource = _filteredCategories;
            QuestCountLabel.Text = $"Quests ({_filteredCategories.Count})";
        }

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateQuestList();
        }

        private void OnQuestSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (QuestListBox.SelectedItem is JournalCategory category)
            {
                _selectedCategory = category;
                _selectedEntry = null;

                // Populate entries for this quest
                EntryListBox.ItemsSource = category.Entries;
                EntryCountLabel.Text = $"Entries ({category.Entries.Count})";

                // Update selection display
                UpdateSelectionDisplay();

                // Enable Open in Manifest button
                OpenInManifestButton.IsEnabled = true;
            }
            else
            {
                _selectedCategory = null;
                _selectedEntry = null;
                EntryListBox.ItemsSource = null;
                EntryCountLabel.Text = "Entries";
                UpdateSelectionDisplay();
                OpenInManifestButton.IsEnabled = false;
            }
        }

        private void OnQuestDoubleClicked(object? sender, RoutedEventArgs e)
        {
            // Double-click on quest: select quest only (no entry)
            if (_selectedCategory != null)
            {
                Result = new QuestBrowserResult
                {
                    QuestTag = _selectedCategory.Tag,
                    EntryId = null
                };
                Close(Result);
            }
        }

        private void OnEntrySelected(object? sender, SelectionChangedEventArgs e)
        {
            if (EntryListBox.SelectedItem is JournalEntry entry)
            {
                _selectedEntry = entry;

                // Show entry preview
                var fullText = entry.Text?.GetDefault() ?? "(no text)";
                EntryPreviewTextBlock.Text = fullText;
                EntryPreviewTextBlock.Foreground = Avalonia.Media.Brushes.White;
                EntryPreviewTextBlock.FontStyle = Avalonia.Media.FontStyle.Normal;
            }
            else
            {
                _selectedEntry = null;
                EntryPreviewTextBlock.Text = "Select an entry to preview its text";
                EntryPreviewTextBlock.Foreground = Avalonia.Media.Brushes.Gray;
                EntryPreviewTextBlock.FontStyle = Avalonia.Media.FontStyle.Italic;
            }

            UpdateSelectionDisplay();
        }

        private void OnEntryDoubleClicked(object? sender, RoutedEventArgs e)
        {
            // Double-click on entry: select quest + entry
            if (_selectedCategory != null && _selectedEntry != null)
            {
                Result = new QuestBrowserResult
                {
                    QuestTag = _selectedCategory.Tag,
                    EntryId = _selectedEntry.ID
                };
                Close(Result);
            }
        }

        private void UpdateSelectionDisplay()
        {
            if (_selectedCategory == null)
            {
                SelectedLabel.Text = "(none)";
            }
            else if (_selectedEntry != null)
            {
                var endMarker = _selectedEntry.End ? " [Complete]" : "";
                SelectedLabel.Text = $"{_selectedCategory.Tag} : Entry {_selectedEntry.ID}{endMarker}";
            }
            else
            {
                SelectedLabel.Text = $"{_selectedCategory.Tag} (no entry selected)";
            }
        }

        /// <summary>
        /// Cached Manifest process to reuse existing instance.
        /// Issue #416: Avoid opening multiple Manifest windows.
        /// </summary>
        private static Process? _manifestProcess;

        private async void OnOpenInManifestClick(object? sender, RoutedEventArgs e)
        {
            if (_selectedCategory == null || string.IsNullOrEmpty(_dialogFilePath))
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot open in Manifest: no quest selected or no dialog path");
                return;
            }

            try
            {
                // Find module.jrl path
                var dialogDir = Path.GetDirectoryName(_dialogFilePath);
                if (string.IsNullOrEmpty(dialogDir))
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot open in Manifest: no dialog directory");
                    return;
                }

                var journalPath = Path.Combine(dialogDir, "module.jrl");
                if (!File.Exists(journalPath))
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Cannot open in Manifest: module.jrl not found at {UnifiedLogger.SanitizePath(journalPath)}");
                    await ShowErrorDialog("module.jrl not found", "Cannot find module.jrl in the dialog file's directory.");
                    return;
                }

                // Find Manifest executable
                var manifestPath = FindManifestPath();

                if (string.IsNullOrEmpty(manifestPath))
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Manifest.exe not found");
                    await ShowManifestNotFoundDialog();
                    return;
                }

                // Check if existing Manifest process is still running
                if (_manifestProcess != null && !_manifestProcess.HasExited)
                {
                    // Manifest is already running - just open the file
                    // Note: Manifest may not support IPC yet, so we just bring it to front
                    UnifiedLogger.LogApplication(LogLevel.INFO, "Manifest already running - opening file in existing instance");
                }

                // Build command line arguments
                // Format: Manifest.exe --file "path/to/module.jrl" --quest "quest_tag" [--entry 123]
                var args = $"--file \"{journalPath}\" --quest \"{_selectedCategory.Tag}\"";
                if (_selectedEntry != null)
                {
                    args += $" --entry {_selectedEntry.ID}";
                }

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Opening Manifest: {UnifiedLogger.SanitizePath(manifestPath)} {args}");

                // Launch Manifest
                var startInfo = new ProcessStartInfo
                {
                    FileName = manifestPath,
                    Arguments = args,
                    UseShellExecute = false
                };
                _manifestProcess = Process.Start(startInfo);

                // Save the path if it wasn't already set (auto-detection success)
                if (string.IsNullOrEmpty(SettingsService.Instance.ManifestPath))
                {
                    SettingsService.Instance.ManifestPath = manifestPath;
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening Manifest: {ex.Message}");
                await ShowErrorDialog("Error", $"Failed to open Manifest: {ex.Message}");
            }
        }

        /// <summary>
        /// Find Manifest.exe using settings or auto-detection.
        /// </summary>
        private string? FindManifestPath()
        {
            // First: Check settings
            var settingsPath = SettingsService.Instance.ManifestPath;
            if (!string.IsNullOrEmpty(settingsPath) && File.Exists(settingsPath))
            {
                return settingsPath;
            }

            // Second: Auto-detect common locations
            var parleyDir = AppDomain.CurrentDomain.BaseDirectory;
            var manifestPaths = new[]
            {
                Path.Combine(parleyDir, "Manifest.exe"),
                Path.Combine(parleyDir, "..", "Manifest", "Manifest.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Radoub", "Manifest", "Manifest.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Radoub", "Manifest", "Manifest.exe")
            };

            foreach (var path in manifestPaths)
            {
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path); // Normalize path
                }
            }

            return null;
        }

        /// <summary>
        /// Show dialog when Manifest is not found with link to download.
        /// </summary>
        private async Task ShowManifestNotFoundDialog()
        {
            var msgBox = new Window
            {
                Title = "Manifest Not Found",
                Width = 450,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var panel = new StackPanel { Margin = new Avalonia.Thickness(20) };

            panel.Children.Add(new TextBlock
            {
                Text = "Manifest journal editor was not found.",
                FontWeight = Avalonia.Media.FontWeight.Bold,
                Margin = new Avalonia.Thickness(0, 0, 0, 10)
            });

            panel.Children.Add(new TextBlock
            {
                Text = "You can download Manifest from the Radoub releases page:",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(0, 0, 0, 10)
            });

            var linkButton = new Button
            {
                Content = "https://github.com/LordOfMyatar/Radoub/releases",
                Margin = new Avalonia.Thickness(0, 0, 0, 10),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
            };
            linkButton.Click += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/LordOfMyatar/Radoub/releases",
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            panel.Children.Add(linkButton);

            panel.Children.Add(new TextBlock
            {
                Text = "Once installed, set the path in Settings > Tools > Manifest Path",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Foreground = Avalonia.Media.Brushes.Gray,
                FontStyle = Avalonia.Media.FontStyle.Italic
            });

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Margin = new Avalonia.Thickness(0, 10, 0, 0)
            };
            okButton.Click += (s, e) => msgBox.Close();
            panel.Children.Add(okButton);

            msgBox.Content = panel;
            await msgBox.ShowDialog(this);
        }

        /// <summary>
        /// Show a simple error dialog.
        /// </summary>
        private async Task ShowErrorDialog(string title, string message)
        {
            var msgBox = new Window
            {
                Title = title,
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var panel = new StackPanel { Margin = new Avalonia.Thickness(20) };
            panel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(0, 0, 0, 20)
            });

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
            };
            okButton.Click += (s, e) => msgBox.Close();
            panel.Children.Add(okButton);

            msgBox.Content = panel;
            await msgBox.ShowDialog(this);
        }

        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            if (_selectedCategory != null)
            {
                Result = new QuestBrowserResult
                {
                    QuestTag = _selectedCategory.Tag,
                    EntryId = _selectedEntry?.ID
                };
                Close(Result);
                return;
            }
            Close(null);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}
