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
using Radoub.Formats.Common;

namespace DialogEditor.Views
{
    /// <summary>
    /// Represents a script entry in the browser with source information.
    /// </summary>
    public class ScriptEntry
    {
        public string Name { get; set; } = "";
        public bool IsBuiltIn { get; set; }
        public string Source { get; set; } = ""; // "Module", "Override", "BIF: filename"

        public string DisplayName => IsBuiltIn ? $"🎮 {Name}" : Name;

        public override string ToString() => DisplayName;
    }

    public partial class ScriptBrowserWindow : Window
    {
        private readonly ScriptService _scriptService;
        private List<ScriptEntry> _moduleScripts;
        private List<ScriptEntry> _builtInScripts;
        private string? _selectedScript;
        private bool _selectedIsBuiltIn;
        private string? _overridePath;
        private readonly string? _dialogFilePath;
        private bool _showBuiltIn;
        private bool _builtInScriptsLoaded;

        public string? SelectedScript => _selectedScript;

        // Parameterless constructor for XAML designer/runtime loader
        public ScriptBrowserWindow() : this(null)
        {
        }

        public ScriptBrowserWindow(string? dialogFilePath)
        {
            InitializeComponent();
            _scriptService = ScriptService.Instance;
            _moduleScripts = new List<ScriptEntry>();
            _builtInScripts = new List<ScriptEntry>();
            _dialogFilePath = dialogFilePath;

            // Check if game resources are available for built-in scripts
            var gameResourceAvailable = GameResourceService.Instance.IsAvailable;
            ShowBuiltInCheckBox.IsEnabled = gameResourceAvailable;
            if (!gameResourceAvailable)
            {
                ToolTip.SetTip(ShowBuiltInCheckBox, "Game path not configured in Settings. Cannot load built-in scripts.");
            }

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

        private async void OnShowBuiltInChanged(object? sender, RoutedEventArgs e)
        {
            _showBuiltIn = ShowBuiltInCheckBox.IsChecked == true;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Script browser: Show built-in scripts = {_showBuiltIn}");

            // Load built-in scripts on first toggle (lazy load)
            if (_showBuiltIn && !_builtInScriptsLoaded)
            {
                await LoadBuiltInScriptsAsync();
            }

            UpdateScriptList();
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
                    _moduleScripts = await LoadScriptsFromPathAsync(_overridePath);
                }
                else
                {
                    // Default: use dialog file's directory
                    var dialogDir = GetDialogDirectory();
                    if (!string.IsNullOrEmpty(dialogDir))
                    {
                        _moduleScripts = await LoadScriptsFromPathAsync(dialogDir);
                    }
                    else
                    {
                        _moduleScripts = new List<ScriptEntry>();
                    }
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

        private async System.Threading.Tasks.Task LoadBuiltInScriptsAsync()
        {
            try
            {
                _builtInScripts = new List<ScriptEntry>();

                var builtInResources = GameResourceService.Instance.ListBuiltInScripts();
                foreach (var resource in builtInResources)
                {
                    // Skip if we already have a module script with same name (module overrides built-in)
                    if (_moduleScripts.Any(s => s.Name.Equals(resource.ResRef, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    _builtInScripts.Add(new ScriptEntry
                    {
                        Name = resource.ResRef,
                        IsBuiltIn = true,
                        Source = $"BIF: {Path.GetFileName(resource.SourcePath)}"
                    });
                }

                _builtInScripts = _builtInScripts.OrderBy(s => s.Name).ToList();
                _builtInScriptsLoaded = true;

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Script Browser: Loaded {_builtInScripts.Count} built-in scripts from game");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load built-in scripts: {ex.Message}");
            }

            await System.Threading.Tasks.Task.CompletedTask;
        }

        private System.Threading.Tasks.Task<List<ScriptEntry>> LoadScriptsFromPathAsync(string path)
        {
            var scripts = new List<ScriptEntry>();

            try
            {
                if (Directory.Exists(path))
                {
                    var scriptFiles = Directory.GetFiles(path, "*.nss", SearchOption.AllDirectories);

                    foreach (var scriptFile in scriptFiles)
                    {
                        var scriptName = Path.GetFileNameWithoutExtension(scriptFile);
                        if (!scripts.Any(s => s.Name.Equals(scriptName, StringComparison.OrdinalIgnoreCase)))
                        {
                            scripts.Add(new ScriptEntry
                            {
                                Name = scriptName,
                                IsBuiltIn = false,
                                Source = "Module"
                            });
                        }
                    }

                    scripts = scripts.OrderBy(s => s.Name).ToList();
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Script Browser: Found {scripts.Count} module scripts");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning path for scripts: {ex.Message}");
            }

            return System.Threading.Tasks.Task.FromResult(scripts);
        }

        private void UpdateScriptList()
        {
            ScriptListBox.Items.Clear();

            var searchText = SearchBox?.Text?.ToLowerInvariant();

            // Combine module and built-in scripts
            var allScripts = _moduleScripts.ToList();
            if (_showBuiltIn)
            {
                allScripts.AddRange(_builtInScripts);
            }

            var scriptsToDisplay = allScripts;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                scriptsToDisplay = allScripts
                    .Where(s => s.Name.ToLowerInvariant().Contains(searchText))
                    .ToList();
            }

            // Sort: module scripts first, then built-in, both alphabetical
            scriptsToDisplay = scriptsToDisplay
                .OrderBy(s => s.IsBuiltIn)
                .ThenBy(s => s.Name)
                .ToList();

            foreach (var script in scriptsToDisplay)
            {
                ScriptListBox.Items.Add(script);
            }

            // Update count label
            var moduleCount = scriptsToDisplay.Count(s => !s.IsBuiltIn);
            var builtInCount = scriptsToDisplay.Count(s => s.IsBuiltIn);

            if (scriptsToDisplay.Count == 0)
            {
                if (!string.IsNullOrEmpty(_overridePath))
                {
                    ScriptCountLabel.Text = "⚠ No scripts found in selected folder";
                }
                else if (string.IsNullOrEmpty(GetDialogDirectory()))
                {
                    ScriptCountLabel.Text = "⚠ No scripts found - use browse... to select folder";
                }
                else
                {
                    ScriptCountLabel.Text = "⚠ No matching scripts";
                }
                ScriptCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
            }
            else
            {
                var countText = $"{moduleCount} module";
                if (builtInCount > 0)
                {
                    countText += $" + {builtInCount} 🎮 built-in";
                }
                ScriptCountLabel.Text = countText;
                ScriptCountLabel.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        {
            UpdateScriptList();
        }

        private async void OnScriptSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (ScriptListBox.SelectedItem is ScriptEntry scriptEntry)
            {
                _selectedScript = scriptEntry.Name;
                _selectedIsBuiltIn = scriptEntry.IsBuiltIn;
                SelectedScriptLabel.Text = scriptEntry.DisplayName;

                // Disable "Open in Editor" for built-in scripts (they're in BIF, not files)
                OpenInEditorButton.IsEnabled = !scriptEntry.IsBuiltIn;

                // Load script preview
                try
                {
                    PreviewHeaderLabel.Text = $"Preview: {scriptEntry.Name}.nss";
                    PreviewTextBox.Text = "Loading...";

                    string? scriptContent = null;

                    if (scriptEntry.IsBuiltIn)
                    {
                        // Built-in scripts are compiled (.ncs), no source available
                        scriptContent = $"// Built-in game script: {scriptEntry.Name}\n" +
                                        $"// Source: {scriptEntry.Source}\n" +
                                        "//\n" +
                                        "// Built-in scripts are pre-compiled in the game files.\n" +
                                        "// Source code (.nss) is not available for preview.\n" +
                                        "//\n" +
                                        "// This script can still be referenced in dialogs.\n" +
                                        "// For documentation, see NWN Lexicon or community wikis.";
                    }
                    else
                    {
                        // If override path is set, load from there first
                        if (!string.IsNullOrEmpty(_overridePath))
                        {
                            scriptContent = await LoadScriptContentFromPathAsync(scriptEntry.Name, _overridePath);
                        }
                        else
                        {
                            // Try dialog directory
                            var dialogDir = GetDialogDirectory();
                            if (!string.IsNullOrEmpty(dialogDir))
                            {
                                scriptContent = await LoadScriptContentFromPathAsync(scriptEntry.Name, dialogDir);
                            }
                        }

                        // Fall back to service if still not found
                        if (string.IsNullOrEmpty(scriptContent))
                        {
                            scriptContent = await _scriptService.GetScriptContentAsync(scriptEntry.Name);
                        }
                    }

                    if (!string.IsNullOrEmpty(scriptContent))
                    {
                        PreviewTextBox.Text = scriptContent;
                    }
                    else
                    {
                        PreviewTextBox.Text = $"// Script '{scriptEntry.Name}.nss' not found or could not be loaded.\n" +
                                              "// This may be a compiled game resource (.ncs) without source available.\n" +
                                              "// Use nwnnsscomp to decompile .ncs files: github.com/niv/neverwinter.nim\n" +
                                              "// The script name will still be saved to the dialog.";
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
                _selectedIsBuiltIn = false;
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
                    // Use case-insensitive file matching (required for Linux compatibility)
                    var matchingFile = Directory.EnumerateFiles(basePath, "*.nss", SearchOption.AllDirectories)
                        .FirstOrDefault(f => Path.GetFileName(f).Equals(scriptFileName, StringComparison.OrdinalIgnoreCase));

                    if (matchingFile != null)
                    {
                        return await File.ReadAllTextAsync(matchingFile);
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error loading script from path: {ex.Message}");
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
            if (string.IsNullOrWhiteSpace(_selectedScript) || _selectedIsBuiltIn)
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
