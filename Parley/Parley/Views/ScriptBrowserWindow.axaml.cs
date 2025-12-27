using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using DialogEditor.Utils;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;

namespace DialogEditor.Views
{
    /// <summary>
    /// Represents a script entry in the browser with source information.
    /// </summary>
    public class ScriptEntry
    {
        public string Name { get; set; } = "";
        public bool IsBuiltIn { get; set; }
        public string Source { get; set; } = ""; // "Module", "Override", "BIF: filename", "HAK: filename"

        /// <summary>
        /// If from HAK, the path to the HAK file.
        /// </summary>
        public string? HakPath { get; set; }

        /// <summary>
        /// If from HAK, the ERF resource entry for extraction.
        /// </summary>
        public ErfResourceEntry? ErfEntry { get; set; }

        /// <summary>
        /// True if this script comes from a HAK file (requires extraction for preview).
        /// </summary>
        public bool IsFromHak => HakPath != null && ErfEntry != null;

        /// <summary>
        /// Full file path for filesystem scripts.
        /// </summary>
        public string? FilePath { get; set; }

        public string DisplayName => IsBuiltIn ? $"🎮 {Name}" : IsFromHak ? $"📦 {Name}" : Name;

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Cached HAK file script data to avoid re-scanning on each browser open.
    /// </summary>
    internal class ScriptHakCacheEntry
    {
        public string HakPath { get; set; } = "";
        public DateTime LastModified { get; set; }
        public List<ScriptEntry> Scripts { get; set; } = new();
    }

    public partial class ScriptBrowserWindow : Window
    {
        private readonly ScriptService _scriptService;
        private List<ScriptEntry> _moduleScripts;
        private List<ScriptEntry> _builtInScripts;
        private List<ScriptEntry> _hakScripts;
        private string? _selectedScript;
        private bool _selectedIsBuiltIn;
        private string? _overridePath;
        private readonly string? _dialogFilePath;
        private bool _showBuiltIn;
        private bool _showHakScripts;
        private bool _builtInScriptsLoaded;
        private bool _hakScriptsLoaded;
        private ScriptEntry? _selectedScriptEntry;

        // Static cache for HAK file contents - persists across window instances
        private static readonly Dictionary<string, ScriptHakCacheEntry> _hakCache = new();

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
            _hakScripts = new List<ScriptEntry>();
            _dialogFilePath = dialogFilePath;

            // Check if game resources are available for built-in scripts
            var gameResourceAvailable = GameResourceService.Instance.IsAvailable;
            ShowBuiltInCheckBox.IsEnabled = gameResourceAvailable;
            if (!gameResourceAvailable)
            {
                ToolTip.SetTip(ShowBuiltInCheckBox, "Game path not configured in Settings. Cannot load built-in scripts.");
            }

            // Enable HAK checkbox - HAKs can be found in dialog directory
            ShowHakCheckBox.IsEnabled = true;

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

        private async void OnShowHakChanged(object? sender, RoutedEventArgs e)
        {
            _showHakScripts = ShowHakCheckBox.IsChecked == true;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Script browser: Show HAK scripts = {_showHakScripts}");

            // Load HAK scripts on first toggle (lazy load)
            if (_showHakScripts && !_hakScriptsLoaded)
            {
                await LoadHakScriptsAsync();
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

            await Task.CompletedTask;
        }

        private async Task LoadHakScriptsAsync()
        {
            try
            {
                _hakScripts = new List<ScriptEntry>();

                // Get HAK paths to scan
                var hakPaths = new List<string>();

                // 1. Dialog file directory (highest priority for module HAKs)
                var dialogDir = GetDialogDirectory();
                if (!string.IsNullOrEmpty(dialogDir))
                {
                    hakPaths.AddRange(GetHakFilesFromPath(dialogDir));
                }

                // 2. Override path if set
                if (!string.IsNullOrEmpty(_overridePath))
                {
                    hakPaths.AddRange(GetHakFilesFromPath(_overridePath));
                }

                // 3. NWN user hak folder
                var userPath = SettingsService.Instance.NeverwinterNightsPath;
                if (!string.IsNullOrEmpty(userPath) && Directory.Exists(userPath))
                {
                    var hakFolder = Path.Combine(userPath, "hak");
                    if (Directory.Exists(hakFolder))
                    {
                        hakPaths.AddRange(GetHakFilesFromPath(hakFolder));
                    }
                }

                // Deduplicate HAK paths (same file might be found in multiple locations)
                hakPaths = hakPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                if (hakPaths.Count == 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, "Script Browser: No HAK files found to scan");
                    _hakScriptsLoaded = true;
                    return;
                }

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Script Browser: Scanning {hakPaths.Count} HAK files for scripts");

                // Scan HAKs for .nss scripts
                for (int i = 0; i < hakPaths.Count; i++)
                {
                    var hakPath = hakPaths[i];
                    var hakName = Path.GetFileName(hakPath);
                    ScriptCountLabel.Text = $"Loading HAK {i + 1}/{hakPaths.Count}: {hakName}...";

                    await Task.Run(() => ScanHakForScripts(hakPath));
                }

                _hakScripts = _hakScripts.OrderBy(s => s.Name).ToList();
                _hakScriptsLoaded = true;

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Script Browser: Loaded {_hakScripts.Count} scripts from HAK files");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load HAK scripts: {ex.Message}");
            }
        }

        private IEnumerable<string> GetHakFilesFromPath(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    return Directory.GetFiles(path, "*.hak", SearchOption.TopDirectoryOnly);
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Error scanning for HAKs in {UnifiedLogger.SanitizePath(path)}: {ex.Message}");
            }
            return Enumerable.Empty<string>();
        }

        private void ScanHakForScripts(string hakPath)
        {
            try
            {
                var hakFileName = Path.GetFileName(hakPath);
                var lastModified = File.GetLastWriteTimeUtc(hakPath);

                // Check cache first
                if (_hakCache.TryGetValue(hakPath, out var cached) && cached.LastModified == lastModified)
                {
                    // Use cached scripts - deep copy to avoid shared state issues
                    foreach (var script in cached.Scripts)
                    {
                        // Skip if we already have this script from module (module overrides HAK)
                        if (_moduleScripts.Any(s => s.Name.Equals(script.Name, StringComparison.OrdinalIgnoreCase)))
                            continue;
                        // Skip if already found in another HAK
                        if (_hakScripts.Any(s => s.Name.Equals(script.Name, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        _hakScripts.Add(new ScriptEntry
                        {
                            Name = script.Name,
                            IsBuiltIn = false,
                            Source = script.Source,
                            HakPath = script.HakPath,
                            ErfEntry = script.ErfEntry
                        });
                    }
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Script Browser: Used cached {cached.Scripts.Count} scripts from {hakFileName}");
                    return;
                }

                // Not cached or outdated - scan HAK
                // Use ReadMetadataOnly to avoid loading entire file into memory (large HAKs can be 800MB+)
                var erf = ErfReader.ReadMetadataOnly(hakPath);
                var nssResources = erf.GetResourcesByType(ResourceTypes.Nss).ToList();
                var newCacheEntry = new ScriptHakCacheEntry
                {
                    HakPath = hakPath,
                    LastModified = lastModified,
                    Scripts = new List<ScriptEntry>()
                };

                foreach (var resource in nssResources)
                {
                    var scriptName = resource.ResRef;
                    var scriptEntry = new ScriptEntry
                    {
                        Name = scriptName,
                        IsBuiltIn = false,
                        Source = $"HAK: {hakFileName}",
                        HakPath = hakPath,
                        ErfEntry = resource
                    };

                    // Add to cache
                    newCacheEntry.Scripts.Add(scriptEntry);

                    // Skip if we already have this script from module (module overrides HAK)
                    if (_moduleScripts.Any(s => s.Name.Equals(scriptName, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    // Skip if already found in another HAK
                    if (_hakScripts.Any(s => s.Name.Equals(scriptName, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    _hakScripts.Add(scriptEntry);
                }

                // Update cache
                _hakCache[hakPath] = newCacheEntry;

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Script Browser: Scanned and cached {nssResources.Count} scripts in {hakFileName}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning HAK {UnifiedLogger.SanitizePath(hakPath)}: {ex.Message}");
            }
        }

        private Task<List<ScriptEntry>> LoadScriptsFromPathAsync(string path)
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
                                Source = "Module",
                                FilePath = scriptFile
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

            // Combine module, HAK, and built-in scripts (in priority order)
            var allScripts = _moduleScripts.ToList();
            if (_showHakScripts)
            {
                // Add HAK scripts that aren't already in module list
                foreach (var hakScript in _hakScripts)
                {
                    if (!allScripts.Any(s => s.Name.Equals(hakScript.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        allScripts.Add(hakScript);
                    }
                }
            }
            if (_showBuiltIn)
            {
                // Add built-in scripts that aren't already in module or HAK list
                foreach (var builtIn in _builtInScripts)
                {
                    if (!allScripts.Any(s => s.Name.Equals(builtIn.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        allScripts.Add(builtIn);
                    }
                }
            }

            var scriptsToDisplay = allScripts;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                scriptsToDisplay = allScripts
                    .Where(s => s.Name.ToLowerInvariant().Contains(searchText))
                    .ToList();
            }

            // Sort: module scripts first, then HAK, then built-in, all alphabetical
            scriptsToDisplay = scriptsToDisplay
                .OrderBy(s => s.IsBuiltIn ? 2 : s.IsFromHak ? 1 : 0)
                .ThenBy(s => s.Name)
                .ToList();

            foreach (var script in scriptsToDisplay)
            {
                ScriptListBox.Items.Add(script);
            }

            // Update count label
            var moduleCount = scriptsToDisplay.Count(s => !s.IsBuiltIn && !s.IsFromHak);
            var hakCount = scriptsToDisplay.Count(s => s.IsFromHak);
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
                if (hakCount > 0)
                {
                    countText += $" + {hakCount} 📦 HAK";
                }
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
                _selectedScriptEntry = scriptEntry;
                SelectedScriptLabel.Text = scriptEntry.DisplayName;

                // Disable "Open in Editor" for built-in scripts and HAK scripts (they're in archives, not files)
                OpenInEditorButton.IsEnabled = !scriptEntry.IsBuiltIn && !scriptEntry.IsFromHak;

                // Load script preview
                try
                {
                    PreviewHeaderLabel.Text = $"Preview: {scriptEntry.Name}.nss";
                    PreviewTextBox.Text = "Loading...";

                    string? scriptContent = null;

                    if (scriptEntry.IsBuiltIn)
                    {
                        // Built-in scripts - extract .nss source from game BIFs
                        scriptContent = await LoadScriptContentFromGameFilesAsync(scriptEntry);
                    }
                    else if (scriptEntry.IsFromHak)
                    {
                        // HAK scripts - extract from archive for preview
                        scriptContent = await LoadScriptContentFromHakAsync(scriptEntry);
                    }
                    else
                    {
                        // Module/filesystem scripts
                        // First try using stored FilePath if available
                        if (!string.IsNullOrEmpty(scriptEntry.FilePath) && File.Exists(scriptEntry.FilePath))
                        {
                            scriptContent = await File.ReadAllTextAsync(scriptEntry.FilePath);
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
                _selectedScriptEntry = null;
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

        /// <summary>
        /// Load script content from a HAK file by extracting the resource.
        /// </summary>
        private async Task<string?> LoadScriptContentFromHakAsync(ScriptEntry scriptEntry)
        {
            if (!scriptEntry.IsFromHak || scriptEntry.HakPath == null || scriptEntry.ErfEntry == null)
                return null;

            try
            {
                // Extract script data from HAK on background thread
                var scriptData = await Task.Run(() =>
                    ErfReader.ExtractResource(scriptEntry.HakPath, scriptEntry.ErfEntry));

                if (scriptData == null || scriptData.Length == 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"Failed to extract script '{scriptEntry.Name}' from HAK: empty data");
                    return null;
                }

                // NWScript source files are plain text
                var content = System.Text.Encoding.UTF8.GetString(scriptData);

                // Add source header for context
                var header = $"// Script from HAK: {Path.GetFileName(scriptEntry.HakPath)}\n" +
                            $"// ResRef: {scriptEntry.Name}\n" +
                            "//\n";

                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Extracted script '{scriptEntry.Name}' from HAK ({scriptData.Length} bytes)");

                return header + content;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Error extracting script from HAK: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Load script content from game BIF files (built-in scripts).
        /// NWN:EE includes .nss source files in nwn_base_scripts.bif.
        /// </summary>
        private async Task<string?> LoadScriptContentFromGameFilesAsync(ScriptEntry scriptEntry)
        {
            if (!scriptEntry.IsBuiltIn)
                return null;

            try
            {
                // Extract script data from game BIFs on background thread
                var scriptData = await Task.Run(() =>
                    GameResourceService.Instance.FindResource(scriptEntry.Name, ResourceTypes.Nss));

                if (scriptData == null || scriptData.Length == 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"No .nss source found for built-in script '{scriptEntry.Name}'");
                    return $"// Built-in game script: {scriptEntry.Name}\n" +
                           $"// Source: {scriptEntry.Source}\n" +
                           "//\n" +
                           "// Source code (.nss) not found in game files.\n" +
                           "// This may be a compiled-only script.\n" +
                           "// The script name can still be referenced in dialogs.";
                }

                // NWScript source files are plain text
                var content = System.Text.Encoding.UTF8.GetString(scriptData);

                // Add source header for context
                var header = $"// Built-in script from: {scriptEntry.Source}\n" +
                            $"// ResRef: {scriptEntry.Name}\n" +
                            "//\n";

                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Extracted built-in script '{scriptEntry.Name}' ({scriptData.Length} bytes)");

                return header + content;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Error extracting built-in script: {ex.Message}");
                return null;
            }
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
