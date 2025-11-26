using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DialogEditor.Services;
using DialogEditor.Utils;

namespace DialogEditor.Views
{
    public partial class ScriptBrowserWindow : Window
    {
        private readonly ScriptService _scriptService;
        private List<string> _allScripts;
        private string? _selectedScript;
        private string? _overridePath;
        private readonly string? _dialogFilePath;

        public string? SelectedScript => _selectedScript;

        // Parameterless constructor for XAML designer/runtime loader
        public ScriptBrowserWindow() : this(null)
        {
        }

        public ScriptBrowserWindow(string? dialogFilePath)
        {
            InitializeComponent();
            _scriptService = ScriptService.Instance;
            _allScripts = new List<string>();
            _dialogFilePath = dialogFilePath;

            UpdateLocationDisplay();
            LoadScripts();
        }

        private void UpdateLocationDisplay()
        {
            if (!string.IsNullOrEmpty(_overridePath))
            {
                // Show sanitized override path
                LocationPathLabel.Text = UnifiedLogger.SanitizePath(_overridePath);
                LocationPathLabel.Foreground = new SolidColorBrush(Colors.White);
                ResetLocationButton.IsVisible = true;
            }
            else
            {
                // Default: use dialog file's directory
                var dialogDir = GetDialogDirectory();
                if (!string.IsNullOrEmpty(dialogDir))
                {
                    LocationPathLabel.Text = UnifiedLogger.SanitizePath(dialogDir);
                    LocationPathLabel.Foreground = new SolidColorBrush(Colors.LightGray);
                }
                else
                {
                    LocationPathLabel.Text = "(no dialog loaded - use browse...)";
                    LocationPathLabel.Foreground = new SolidColorBrush(Colors.Orange);
                }
                ResetLocationButton.IsVisible = false;
            }
        }

        private string? GetDialogDirectory()
        {
            if (!string.IsNullOrEmpty(_dialogFilePath))
            {
                var dir = Path.GetDirectoryName(_dialogFilePath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    return dir;
            }
            return null;
        }

        private async void OnBrowseLocationClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Default to parent of dialog directory (one level up from .dlg)
                IStorageFolder? suggestedStart = null;
                var dialogDir = GetDialogDirectory();
                if (!string.IsNullOrEmpty(dialogDir))
                {
                    var parentDir = Path.GetDirectoryName(dialogDir);
                    if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                    {
                        suggestedStart = await StorageProvider.TryGetFolderFromPathAsync(parentDir);
                    }
                }

                var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Script Location",
                    AllowMultiple = false,
                    SuggestedStartLocation = suggestedStart
                });

                if (folders.Count > 0)
                {
                    var folder = folders[0];
                    _overridePath = folder.Path.LocalPath;
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Script browser: Override path set to {UnifiedLogger.SanitizePath(_overridePath)}");

                    UpdateLocationDisplay();
                    await LoadScriptsAsync();
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error selecting folder: {ex.Message}");
            }
        }

        private async void OnResetLocationClick(object? sender, RoutedEventArgs e)
        {
            _overridePath = null;
            UnifiedLogger.LogApplication(LogLevel.INFO, "Script browser: Reset to auto-detected paths");
            UpdateLocationDisplay();
            await LoadScriptsAsync();
        }

        private async void LoadScripts()
        {
            await LoadScriptsAsync();
        }

        private async System.Threading.Tasks.Task LoadScriptsAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(_overridePath))
                {
                    // Use override path only
                    _allScripts = await LoadScriptsFromPathAsync(_overridePath);
                }
                else
                {
                    // Default: use dialog file's directory
                    var dialogDir = GetDialogDirectory();
                    if (!string.IsNullOrEmpty(dialogDir))
                    {
                        _allScripts = await LoadScriptsFromPathAsync(dialogDir);
                    }
                    else
                    {
                        _allScripts = new List<string>();
                    }
                }

                if (_allScripts.Count == 0)
                {
                    if (!string.IsNullOrEmpty(_overridePath))
                    {
                        ScriptCountLabel.Text = "⚠ No scripts found in selected folder";
                    }
                    else
                    {
                        ScriptCountLabel.Text = "⚠ No scripts found - use browse... to select folder";
                    }
                    ScriptCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Script Browser: No scripts found");
                    return;
                }

                UpdateScriptList();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load scripts: {ex.Message}");
                ScriptCountLabel.Text = $"❌ Error loading scripts: {ex.Message}";
                ScriptCountLabel.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        private System.Threading.Tasks.Task<List<string>> LoadScriptsFromPathAsync(string path)
        {
            var scripts = new List<string>();

            try
            {
                if (Directory.Exists(path))
                {
                    var scriptFiles = Directory.GetFiles(path, "*.nss", SearchOption.AllDirectories);

                    foreach (var scriptFile in scriptFiles)
                    {
                        var scriptName = Path.GetFileNameWithoutExtension(scriptFile);
                        if (!scripts.Contains(scriptName))
                            scripts.Add(scriptName);
                    }

                    scripts.Sort();
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Script Browser: Found {scripts.Count} scripts in override path");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning override path for scripts: {ex.Message}");
            }

            return System.Threading.Tasks.Task.FromResult(scripts);
        }

        private void UpdateScriptList()
        {
            ScriptListBox.Items.Clear();

            var searchText = SearchBox?.Text?.ToLowerInvariant();
            var scriptsToDisplay = _allScripts;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                scriptsToDisplay = _allScripts
                    .Where(s => s.ToLowerInvariant().Contains(searchText))
                    .ToList();
            }

            foreach (var script in scriptsToDisplay)
            {
                ScriptListBox.Items.Add(script);
            }

            ScriptCountLabel.Text = $"{scriptsToDisplay.Count} script{(scriptsToDisplay.Count == 1 ? "" : "s")}";
            ScriptCountLabel.Foreground = new SolidColorBrush(Colors.White);
        }

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateScriptList();
        }

        private async void OnScriptSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (ScriptListBox.SelectedItem is string scriptName)
            {
                _selectedScript = scriptName;
                SelectedScriptLabel.Text = scriptName;
                OpenInEditorButton.IsEnabled = true;

                // Load script preview
                try
                {
                    PreviewHeaderLabel.Text = $"Preview: {scriptName}.nss";
                    PreviewTextBox.Text = "Loading...";

                    string? scriptContent = null;

                    // If override path is set, load from there first
                    if (!string.IsNullOrEmpty(_overridePath))
                    {
                        scriptContent = await LoadScriptContentFromPathAsync(scriptName, _overridePath);
                    }
                    else
                    {
                        // Try dialog directory
                        var dialogDir = GetDialogDirectory();
                        if (!string.IsNullOrEmpty(dialogDir))
                        {
                            scriptContent = await LoadScriptContentFromPathAsync(scriptName, dialogDir);
                        }
                    }

                    // Fall back to service if still not found
                    if (string.IsNullOrEmpty(scriptContent))
                    {
                        scriptContent = await _scriptService.GetScriptContentAsync(scriptName);
                    }

                    if (!string.IsNullOrEmpty(scriptContent))
                    {
                        PreviewTextBox.Text = scriptContent;
                    }
                    else
                    {
                        PreviewTextBox.Text = $"// Script '{scriptName}.nss' not found or could not be loaded.\n" +
                                              "// The script name will still be saved to the dialog.\n" +
                                              "// Make sure the .nss file exists in your module directory.";
                    }
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error loading script preview: {ex.Message}");
                    PreviewTextBox.Text = $"// Error loading script preview: {ex.Message}";
                }
            }
            else
            {
                _selectedScript = null;
                SelectedScriptLabel.Text = "(none)";
                PreviewHeaderLabel.Text = "Script Preview";
                PreviewTextBox.Text = "";
                OpenInEditorButton.IsEnabled = false;
            }
        }

        private async System.Threading.Tasks.Task<string?> LoadScriptContentFromPathAsync(string scriptName, string basePath)
        {
            try
            {
                var scriptFileName = scriptName.EndsWith(".nss", StringComparison.OrdinalIgnoreCase)
                    ? scriptName
                    : $"{scriptName}.nss";

                if (Directory.Exists(basePath))
                {
                    var scriptFiles = Directory.GetFiles(basePath, scriptFileName, SearchOption.AllDirectories);
                    if (scriptFiles.Length > 0)
                    {
                        return await File.ReadAllTextAsync(scriptFiles[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error loading script from override path: {ex.Message}");
            }

            return null;
        }

        private void OnScriptDoubleClicked(object? sender, RoutedEventArgs e)
        {
            // Double-click selects and closes (modeless - Issue #20)
            if (_selectedScript != null)
            {
                Close();
            }
        }

        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            // Modeless: Just close, Closed event handler will read SelectedScript (Issue #20)
            Close();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            // Modeless: Clear selection before closing (Issue #20)
            _selectedScript = null;
            Close();
        }

        private void OnOpenInEditorClick(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_selectedScript))
                return;

            try
            {
                var scriptPath = _scriptService.GetScriptFilePath(_selectedScript);
                if (scriptPath == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Could not locate script file: {_selectedScript}.nss");
                    return;
                }

                var editorPath = SettingsService.Instance.ExternalEditorPath;

                if (string.IsNullOrWhiteSpace(editorPath))
                {
                    // No editor configured - open with default system editor
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = scriptPath,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Opened script in default editor: {UnifiedLogger.SanitizePath(scriptPath)}");
                }
                else
                {
                    // Open with configured external editor
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = editorPath,
                        Arguments = $"\"{scriptPath}\"",
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Opened script in external editor: {UnifiedLogger.SanitizePath(scriptPath)}");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open script in editor: {ex.Message}");
            }
        }
    }
}
