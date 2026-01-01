using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace Radoub.UI.Views;

public partial class ScriptBrowserWindow : Window
{
    private readonly IScriptBrowserContext? _context;
    private readonly HakScriptScanner _hakScanner = new();
    private readonly ScriptPreviewLoader _previewLoader;
    private List<ScriptEntry> _moduleScripts;
    private List<ScriptEntry> _builtInScripts;
    private List<ScriptEntry> _hakScripts;
    private string? _selectedScript;
    private bool _selectedIsBuiltIn;
    private string? _overridePath;
    private bool _showBuiltIn;
    private bool _showHakScripts;
    private bool _builtInScriptsLoaded;
    private bool _hakScriptsLoaded;
    private ScriptEntry? _selectedScriptEntry;
    private bool _confirmed;

    /// <summary>
    /// Gets the selected script name. Only valid if Confirmed is true.
    /// </summary>
    public string? SelectedScript => _confirmed ? _selectedScript : null;

    // Parameterless constructor for XAML designer/runtime loader
    public ScriptBrowserWindow() : this(null)
    {
    }

    public ScriptBrowserWindow(IScriptBrowserContext? context)
    {
        InitializeComponent();
        _context = context;
        _previewLoader = new ScriptPreviewLoader(context, _hakScanner);
        _moduleScripts = new List<ScriptEntry>();
        _builtInScripts = new List<ScriptEntry>();
        _hakScripts = new List<ScriptEntry>();

        // Check if game resources are available for built-in scripts
        var gameResourceAvailable = _context?.GameResourcesAvailable ?? false;
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
            // Default: use context's current file directory
            var currentDir = _context?.CurrentFileDirectory;
            if (!string.IsNullOrEmpty(currentDir))
            {
                LocationPathLabel.Text = UnifiedLogger.SanitizePath(currentDir);
                LocationPathLabel.Foreground = new SolidColorBrush(Colors.LightGray);
            }
            else
            {
                LocationPathLabel.Text = "(no file loaded - use browse...)";
                LocationPathLabel.Foreground = new SolidColorBrush(Colors.Orange);
            }
            ResetLocationButton.IsVisible = false;
        }
    }

    private string? GetCurrentDirectory()
    {
        return _context?.CurrentFileDirectory;
    }

    private async void OnBrowseLocationClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Default to parent of current directory (one level up)
            IStorageFolder? suggestedStart = null;
            var currentDir = GetCurrentDirectory();
            if (!string.IsNullOrEmpty(currentDir))
            {
                var parentDir = Path.GetDirectoryName(currentDir);
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

    private async Task LoadScriptsAsync()
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
                // Default: use context's current file directory
                var currentDir = GetCurrentDirectory();
                if (!string.IsNullOrEmpty(currentDir))
                {
                    _moduleScripts = await LoadScriptsFromPathAsync(currentDir);
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

    private async Task LoadBuiltInScriptsAsync()
    {
        try
        {
            _builtInScripts = new List<ScriptEntry>();

            if (_context == null) return;

            var builtInResources = _context.ListBuiltInScripts();
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
            // Collect HAK paths from all relevant locations
            var hakPaths = HakScriptScanner.CollectHakPaths(
                GetCurrentDirectory(),
                _overridePath,
                _context?.NeverwinterNightsPath);

            if (hakPaths.Count == 0)
            {
                _hakScriptsLoaded = true;
                return;
            }

            // Scan HAKs using the scanner service
            _hakScripts = await _hakScanner.ScanHaksForScriptsAsync(
                hakPaths,
                _moduleScripts,
                (current, total, hakName) =>
                {
                    ScriptCountLabel.Text = $"Loading HAK {current}/{total}: {hakName}...";
                });

            _hakScriptsLoaded = true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load HAK scripts: {ex.Message}");
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

        return Task.FromResult(scripts);
    }

    private void UpdateScriptList()
    {
        ScriptListBox.Items.Clear();

        var result = ScriptListManager.CombineAndFilter(
            _moduleScripts,
            _hakScripts,
            _builtInScripts,
            _showHakScripts,
            _showBuiltIn,
            SearchBox?.Text);

        foreach (var script in result.Scripts)
        {
            ScriptListBox.Items.Add(script);
        }

        // Update count label
        if (result.TotalCount == 0)
        {
            if (!string.IsNullOrEmpty(_overridePath))
            {
                ScriptCountLabel.Text = "⚠ No scripts found in selected folder";
            }
            else if (string.IsNullOrEmpty(GetCurrentDirectory()))
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
            ScriptCountLabel.Text = ScriptListManager.FormatCountText(result);
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
            PreviewHeaderLabel.Text = $"Preview: {scriptEntry.Name}.nss";
            PreviewTextBox.Text = "Loading...";

            var content = await _previewLoader.LoadScriptContentAsync(scriptEntry, _overridePath);
            PreviewTextBox.Text = content;
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

    private void OnScriptDoubleClicked(object? sender, RoutedEventArgs e)
    {
        // Double-click selects and closes
        if (_selectedScript != null)
        {
            _confirmed = true;
            Close(_selectedScript);
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        _confirmed = true;
        Close(_selectedScript);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        // Don't set _confirmed - SelectedScript will return null
        Close(null);
    }

    private void OnOpenInEditorClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedScript) || _selectedIsBuiltIn || _selectedScriptEntry == null)
            return;

        try
        {
            var scriptPath = _selectedScriptEntry.FilePath;
            if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Could not locate script file: {_selectedScript}.nss");
                return;
            }

            var editorPath = _context?.ExternalEditorPath;

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
                    UseShellExecute = true
                };
                psi.ArgumentList.Add(scriptPath);
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
