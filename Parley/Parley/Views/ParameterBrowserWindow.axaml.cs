using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DialogEditor.Models;
using DialogEditor.Parsers;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.Views
{
    public partial class ParameterBrowserWindow : Window
    {
        private ScriptParameterDeclarations? _declarations;
        private string? _selectedKey;
        private string? _selectedValue;
        private string? _currentScriptName; // For refresh functionality
        private Dictionary<string, string>? _existingParameters; // Existing node parameters for dependency resolution

        public string? SelectedKey => _selectedKey;
        public string? SelectedValue => _selectedValue;
        public bool DialogResult { get; private set; }

        public ParameterBrowserWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Set up double-click handlers
            KeysList.DoubleTapped += OnKeysListDoubleTapped;
            ValuesList.DoubleTapped += OnValuesListDoubleTapped;

            // Initialize enable cache checkbox to current setting
            var enableCacheCheckBox = this.FindControl<CheckBox>("EnableCacheCheckBox");
            if (enableCacheCheckBox != null)
            {
                enableCacheCheckBox.IsChecked = ParameterCacheService.Instance.EnableCaching;
            }
        }

        /// <summary>
        /// Sets the parameter declarations to display
        /// </summary>
        /// <param name="declarations">Parsed parameter declarations from script</param>
        /// <param name="scriptName">Name of the script</param>
        /// <param name="isConditional">True if condition parameters, false if action parameters</param>
        /// <param name="existingParameters">Existing parameters on the node for dependency resolution</param>
        public void SetDeclarations(ScriptParameterDeclarations? declarations, string scriptName, bool isConditional, Dictionary<string, string>? existingParameters = null)
        {
            _declarations = declarations;
            _currentScriptName = scriptName; // Save for refresh
            _existingParameters = existingParameters; // Save for dependency resolution

            // Update header text
            HeaderText.Text = isConditional ? "Available Condition Parameters" : "Available Action Parameters";

            // Update script name
            if (!string.IsNullOrEmpty(scriptName))
            {
                ScriptNameText.Text = $"Script: {scriptName}.nss";
            }
            else
            {
                ScriptNameText.Text = "No script loaded";
            }

            // Populate keys list - merge declaration keys + cached keys
            var noKeysMessage = this.FindControl<TextBlock>("NoKeysMessage");
            var allKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add keys from script declarations
            if (declarations != null && declarations.Keys.Count > 0)
            {
                foreach (var key in declarations.Keys)
                {
                    allKeys.Add(key);
                }
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"ParameterBrowserWindow: Found {declarations.Keys.Count} declaration keys for {scriptName}");
            }

            // Add keys from cache
            if (!string.IsNullOrWhiteSpace(scriptName))
            {
                var cachedKeys = ParameterCacheService.Instance.GetParameterKeys(scriptName);
                foreach (var key in cachedKeys)
                {
                    allKeys.Add(key);
                }
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"ParameterBrowserWindow: Found {cachedKeys.Count} cached keys for {scriptName}");
            }

            if (allKeys.Count > 0)
            {
                KeysList.ItemsSource = allKeys.OrderBy(k => k).ToList();
                if (noKeysMessage != null) noKeysMessage.IsVisible = false;
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"ParameterBrowserWindow: Displaying {allKeys.Count} total keys (declarations + cache) for {scriptName}");
            }
            else
            {
                KeysList.ItemsSource = new List<string>();
                if (noKeysMessage != null) noKeysMessage.IsVisible = true;
                ValuesHeaderText.Text = "No parameter keys available";
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"ParameterBrowserWindow: No keys found for {scriptName}");
            }

            // Clear values list initially
            ValuesList.ItemsSource = new List<string>();
            ValueCountText.Text = "";
        }

        /// <summary>
        /// Handles key selection changes
        /// </summary>
        private void OnKeySelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (KeysList.SelectedItem is string selectedKey)
            {
                _selectedKey = selectedKey;
                CopyKeyButton.IsEnabled = true;

                // Check if this key has a dependency (e.g., FROM_JOURNAL_ENTRIES(questKey))
                var values = ResolveValuesForKey(selectedKey);

                if (values.Count == 0 && _declarations?.Values != null && _declarations.Values.Count > 0)
                {
                    // Fall back to legacy Values list if no keyed values
                    values = _declarations.Values;
                    ValuesHeaderText.Text = "Legacy Values (not key-specific)";
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"ParameterBrowserWindow: Using legacy values for key '{selectedKey}'");
                }
                else
                {
                    // Count cached vs declaration values
                    var cachedCount = 0;
                    var declCount = 0;
                    if (!string.IsNullOrWhiteSpace(_currentScriptName))
                    {
                        cachedCount = ParameterCacheService.Instance.GetValues(_currentScriptName, selectedKey).Count;
                    }
                    var declValues = _declarations?.GetValuesForKey(selectedKey) ?? new List<string>();
                    declCount = declValues.Count;

                    ValuesHeaderText.Text = $"Values for '{selectedKey}' (🔵 {cachedCount} recent, 📋 {declCount} declared)";
                }

                if (values.Count > 0)
                {
                    // Keep MRU order (cached first, then declarations)
                    ValuesList.ItemsSource = values;
                    ValueCountText.Text = $"{values.Count} total values";
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"ParameterBrowserWindow: Loaded {values.Count} values for key '{selectedKey}'");
                }
                else
                {
                    ValuesList.ItemsSource = new List<string>();
                    ValueCountText.Text = "No values available";
                    ValuesHeaderText.Text = $"No values for '{selectedKey}'";
                }

                // Reset value selection
                _selectedValue = null;
                CopyValueButton.IsEnabled = false;
                AddParameterButton.IsEnabled = false;
            }
            else
            {
                _selectedKey = null;
                CopyKeyButton.IsEnabled = false;
                AddParameterButton.IsEnabled = false;
            }
        }

        /// <summary>
        /// Resolves values for a key, handling dependencies on other parameters.
        /// Merges script declarations with cached values (MRU priority).
        /// If the key depends on another parameter (e.g., FROM_JOURNAL_ENTRIES(sQuest)),
        /// filters the values based on the existing parameter value.
        /// </summary>
        private List<string> ResolveValuesForKey(string key)
        {
            // Get base values from script declarations
            var declarationValues = _declarations?.GetValuesForKey(key) ?? new List<string>();

            // Check if this key has a dependency
            if (_declarations?.Dependencies != null && _declarations.Dependencies.ContainsKey(key))
            {
                string dependsOnKey = _declarations.Dependencies[key];
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"ParameterBrowserWindow: Key '{key}' depends on '{dependsOnKey}'");

                // Check if the dependency parameter exists in the node's existing parameters
                if (_existingParameters != null && _existingParameters.ContainsKey(dependsOnKey))
                {
                    string questTag = _existingParameters[dependsOnKey];
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"ParameterBrowserWindow: Resolving journal entries for quest '{questTag}'");

                    // Get entries specific to this quest from journal cache
                    var entries = JournalService.Instance.GetEntriesForQuest(questTag);
                    var entryIds = entries.Select(e => e.ID.ToString()).Distinct().OrderBy(id => int.Parse(id)).ToList();

                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"ParameterBrowserWindow: Found {entryIds.Count} entries for quest '{questTag}'");

                    return entryIds;
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"ParameterBrowserWindow: Dependency '{dependsOnKey}' not found in existing parameters - showing all entries");
                    // Fall back to showing all values (already loaded in declarationValues)
                }
            }

            // Get cached values for this script and parameter (MRU order)
            var cachedValues = new List<string>();
            if (!string.IsNullOrWhiteSpace(_currentScriptName) && ParameterCacheService.Instance.EnableCaching)
            {
                cachedValues = ParameterCacheService.Instance.GetValues(_currentScriptName, key);
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"ParameterBrowserWindow: Found {cachedValues.Count} cached values for '{_currentScriptName}.{key}'");
            }

            // Merge script declarations (priority - less likely to have typos) + cached values (secondary - may have typos)
            // Declaration values come first, then cached values that aren't already in declarations
            var mergedValues = new List<string>();

            // Add script declaration values first
            mergedValues.AddRange(declarationValues);

            // Add cached values that aren't in declarations (mark with 🔵 to indicate cached-only)
            foreach (var cachedValue in cachedValues)
            {
                if (!declarationValues.Contains(cachedValue, StringComparer.OrdinalIgnoreCase))
                {
                    // Mark cached-only values with visual indicator
                    mergedValues.Add($"🔵 {cachedValue}");
                }
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"ParameterBrowserWindow: Merged {declarationValues.Count} declaration + {cachedValues.Count} cached values = {mergedValues.Count} total");

            return mergedValues;
        }

        /// <summary>
        /// Handles value selection changes
        /// </summary>
        private void OnValueSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (ValuesList.SelectedItem is string selectedValue)
            {
                // Strip the 🔵 marker if present (cached-only values)
                _selectedValue = selectedValue.StartsWith("🔵 ") ? selectedValue.Substring(2) : selectedValue;
                CopyValueButton.IsEnabled = true;
                AddParameterButton.IsEnabled = !string.IsNullOrEmpty(_selectedKey);
            }
            else
            {
                _selectedValue = null;
                CopyValueButton.IsEnabled = false;
                AddParameterButton.IsEnabled = false;
            }
        }

        /// <summary>
        /// Copies selected key to clipboard
        /// </summary>
        private async void OnCopyKeyClick(object? sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedKey))
            {
                try
                {
                    if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                    {
                        await clipboard.SetTextAsync(_selectedKey);
                        UnifiedLogger.LogApplication(LogLevel.INFO,
                            $"ParameterBrowserWindow: Copied key '{_selectedKey}' to clipboard");
                    }
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR,
                        $"ParameterBrowserWindow: Error copying key to clipboard - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Copies selected value to clipboard
        /// </summary>
        private async void OnCopyValueClick(object? sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedValue))
            {
                try
                {
                    if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                    {
                        await clipboard.SetTextAsync(_selectedValue);
                        UnifiedLogger.LogApplication(LogLevel.INFO,
                            $"ParameterBrowserWindow: Copied value '{_selectedValue}' to clipboard");
                    }
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR,
                        $"ParameterBrowserWindow: Error copying value to clipboard - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Adds selected parameter and closes window
        /// </summary>
        private void OnAddParameterClick(object? sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedKey))
            {
                DialogResult = true;
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"ParameterBrowserWindow: Adding parameter - Key: '{_selectedKey}', Value: '{_selectedValue ?? ""}'");
                Close();
            }
        }

        /// <summary>
        /// Refreshes journal cache and reloads current script parameters
        /// </summary>
        private async void OnRefreshJournalClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "Refreshing journal cache...");

                // Clear journal cache
                JournalService.Instance.ClearCache();

                // Find module.jrl file in current dialog's directory
                var settingsService = SettingsService.Instance;
                var modulePath = settingsService.CurrentModulePath;

                if (string.IsNullOrWhiteSpace(modulePath))
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "No module path configured");
                    return;
                }

                var jrlPath = Path.Combine(modulePath, "module.jrl");
                if (!File.Exists(jrlPath))
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"module.jrl not found: {jrlPath}");
                    return;
                }

                // Re-parse journal file (this will write new cache)
                await JournalService.Instance.ParseJournalFileAsync(jrlPath);

                // Reload current script's parameters
                if (_currentScriptName != null)
                {
                    var parser = new ScriptParameterParser();
                    var scriptService = ScriptService.Instance;
                    var scriptContent = await scriptService.GetScriptContentAsync(_currentScriptName);

                    if (!string.IsNullOrEmpty(scriptContent))
                    {
                        _declarations = parser.Parse(scriptContent);

                        // Refresh the key/value lists
                        var noKeysMessage = this.FindControl<TextBlock>("NoKeysMessage");
                        if (_declarations != null && _declarations.Keys.Count > 0)
                        {
                            KeysList.ItemsSource = _declarations.Keys.OrderBy(k => k).ToList();
                            if (noKeysMessage != null) noKeysMessage.IsVisible = false;
                            ValuesList.ItemsSource = new List<string>();
                            ValueCountText.Text = "";
                        }
                        else
                        {
                            KeysList.ItemsSource = new List<string>();
                            if (noKeysMessage != null) noKeysMessage.IsVisible = true;
                        }

                        UnifiedLogger.LogApplication(LogLevel.INFO, "Journal cache refreshed and parameters reloaded");
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error refreshing journal cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Closes window without adding parameter
        /// </summary>
        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Handles double-click on keys list to quickly select
        /// </summary>
        private void OnKeysListDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedKey) && string.IsNullOrEmpty(_selectedValue))
            {
                // If key selected but no value, add just the key
                DialogResult = true;
                Close();
            }
        }

        /// <summary>
        /// Handles double-click on values list to quickly add parameter
        /// </summary>
        private void OnValuesListDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedKey) && !string.IsNullOrEmpty(_selectedValue))
            {
                DialogResult = true;
                Close();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // ESC to close
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
            // Enter to add parameter if both key and value selected
            else if (e.Key == Key.Enter && !string.IsNullOrEmpty(_selectedKey))
            {
                DialogResult = true;
                Close();
            }
        }

        /// <summary>
        /// Handles enable/disable caching checkbox changes
        /// </summary>
        private void OnEnableCacheChanged(object? sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.IsChecked.HasValue)
            {
                ParameterCacheService.Instance.EnableCaching = checkBox.IsChecked.Value;
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Parameter caching {(checkBox.IsChecked.Value ? "enabled" : "disabled")} from browser");

                // Refresh the keys list to show/hide cached keys
                RefreshKeysList();

                // Refresh the values list if a key is selected
                if (_selectedKey != null)
                {
                    RefreshValuesListForSelectedKey();
                }
            }
        }

        /// <summary>
        /// Refreshes the keys list with current declarations and cache state
        /// </summary>
        private void RefreshKeysList()
        {
            var noKeysMessage = this.FindControl<TextBlock>("NoKeysMessage");
            var allKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add keys from script declarations
            if (_declarations != null && _declarations.Keys.Count > 0)
            {
                foreach (var key in _declarations.Keys)
                {
                    allKeys.Add(key);
                }
            }

            // Add keys from cache (only if caching enabled)
            if (!string.IsNullOrWhiteSpace(_currentScriptName) && ParameterCacheService.Instance.EnableCaching)
            {
                var cachedKeys = ParameterCacheService.Instance.GetParameterKeys(_currentScriptName);
                foreach (var key in cachedKeys)
                {
                    allKeys.Add(key);
                }
            }

            if (allKeys.Count > 0)
            {
                KeysList.ItemsSource = allKeys.OrderBy(k => k).ToList();
                if (noKeysMessage != null) noKeysMessage.IsVisible = false;
            }
            else
            {
                KeysList.ItemsSource = new List<string>();
                if (noKeysMessage != null) noKeysMessage.IsVisible = true;
            }
        }

        /// <summary>
        /// Refreshes the values list for the currently selected key
        /// </summary>
        private void RefreshValuesListForSelectedKey()
        {
            if (_selectedKey == null) return;

            var values = ResolveValuesForKey(_selectedKey);

            // Update header with counts
            var cachedCount = 0;
            var declCount = 0;
            if (!string.IsNullOrWhiteSpace(_currentScriptName) && ParameterCacheService.Instance.EnableCaching)
            {
                cachedCount = ParameterCacheService.Instance.GetValues(_currentScriptName, _selectedKey).Count;
            }
            var declValues = _declarations?.GetValuesForKey(_selectedKey) ?? new List<string>();
            declCount = declValues.Count;

            ValuesHeaderText.Text = $"Values for '{_selectedKey}' (🔵 {cachedCount} cached, 📋 {declCount} declared)";

            // Update list
            if (values.Count > 0)
            {
                ValuesList.ItemsSource = values;
                ValueCountText.Text = $"{values.Count} total values";
            }
            else
            {
                ValuesList.ItemsSource = new List<string>();
                ValueCountText.Text = "No values available";
                ValuesHeaderText.Text = $"No values for '{_selectedKey}'";
            }
        }

        /// <summary>
        /// Clears parameter cache for current script
        /// </summary>
        private void OnClearCacheClick(object? sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_currentScriptName))
            {
                ParameterCacheService.Instance.ClearScriptCache(_currentScriptName);
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Cleared parameter cache for script: {_currentScriptName}");

                // Refresh both keys and values lists
                RefreshKeysList();

                if (_selectedKey != null)
                {
                    RefreshValuesListForSelectedKey();
                }
            }
        }

        #region Title Bar Events

        private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }

        private void OnTitleBarDoubleTapped(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        #endregion
    }
}