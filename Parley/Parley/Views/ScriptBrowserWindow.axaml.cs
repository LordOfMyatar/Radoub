using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using DialogEditor.Services;
using DialogEditor.Utils;

namespace DialogEditor.Views
{
    public partial class ScriptBrowserWindow : Window
    {
        private readonly ScriptService _scriptService;
        private List<string> _allScripts;
        private string? _selectedScript;

        public string? SelectedScript => _selectedScript;

        public ScriptBrowserWindow()
        {
            InitializeComponent();
            _scriptService = ScriptService.Instance;
            _allScripts = new List<string>();

            LoadScripts();
        }

        private async void LoadScripts()
        {
            try
            {
                _allScripts = await _scriptService.GetAvailableScriptsAsync();

                if (_allScripts.Count == 0)
                {
                    ScriptCountLabel.Text = "⚠ No scripts found - check module path in Settings";
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

                    var scriptContent = await _scriptService.GetScriptContentAsync(scriptName);

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

        private void OnScriptDoubleClicked(object? sender, RoutedEventArgs e)
        {
            // Double-click selects and closes
            if (_selectedScript != null)
            {
                Close(_selectedScript);
            }
        }

        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            Close(_selectedScript);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(null);
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
