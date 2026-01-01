using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.Utils;
using DialogEditor.ViewModels;
using DialogEditor.Views;
using Radoub.Formats.Logging;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Manages parameter browser UI interactions for MainWindow.
    /// Extracted from ScriptBrowserController for single responsibility.
    ///
    /// Handles:
    /// 1. Parameter browser window lifecycle
    /// 2. Parameter declaration loading and caching
    /// 3. Extracting existing parameters from UI panels
    /// </summary>
    public class ParameterBrowserController
    {
        private readonly SafeControlFinder _controls;
        private readonly Func<MainViewModel> _getViewModel;
        private readonly Func<TreeViewSafeNode?> _getSelectedNode;
        private readonly ScriptParameterUIManager _parameterUIManager;

        // Track active browser window
        private ParameterBrowserWindow? _activeParameterBrowserWindow;

        // Parameter autocomplete: Cache of script parameter declarations
        private ScriptParameterDeclarations? _currentConditionDeclarations;
        private ScriptParameterDeclarations? _currentActionDeclarations;

        public ParameterBrowserController(
            SafeControlFinder controls,
            Func<MainViewModel> getViewModel,
            Func<TreeViewSafeNode?> getSelectedNode,
            ScriptParameterUIManager parameterUIManager)
        {
            _controls = controls ?? throw new ArgumentNullException(nameof(controls));
            _getViewModel = getViewModel ?? throw new ArgumentNullException(nameof(getViewModel));
            _getSelectedNode = getSelectedNode ?? throw new ArgumentNullException(nameof(getSelectedNode));
            _parameterUIManager = parameterUIManager ?? throw new ArgumentNullException(nameof(parameterUIManager));
        }

        private MainViewModel ViewModel => _getViewModel();
        private TreeViewSafeNode? SelectedNode => _getSelectedNode();

        #region Parameter Browser

        /// <summary>
        /// Opens parameter browser for conditional parameters.
        /// Always reloads declarations from current textbox to avoid caching wrong script.
        /// </summary>
        public async Task OnSuggestConditionsParamClickAsync()
        {
            var scriptTextBox = _controls.Get<TextBox>("ScriptAppearsTextBox");
            var currentScript = scriptTextBox?.Text?.Trim();

            if (!string.IsNullOrWhiteSpace(currentScript))
            {
                await LoadParameterDeclarationsAsync(currentScript, true);
            }

            ShowParameterBrowser(_currentConditionDeclarations, true);
        }

        /// <summary>
        /// Opens parameter browser for action parameters.
        /// Always reloads declarations from current textbox to avoid caching wrong script.
        /// </summary>
        public async Task OnSuggestActionsParamClickAsync()
        {
            var scriptTextBox = _controls.Get<TextBox>("ScriptActionTextBox");
            var currentScript = scriptTextBox?.Text?.Trim();

            if (!string.IsNullOrWhiteSpace(currentScript))
            {
                await LoadParameterDeclarationsAsync(currentScript, false);
            }

            ShowParameterBrowser(_currentActionDeclarations, false);
        }

        /// <summary>
        /// Extracts existing parameters from the UI panel for dependency resolution.
        /// Returns a dictionary of key-value pairs currently in the parameter panel.
        /// </summary>
        private Dictionary<string, string> GetExistingParametersFromPanel(bool isCondition)
        {
            var parameters = new Dictionary<string, string>();

            try
            {
                var panelName = isCondition ? "ConditionsParametersPanel" : "ActionsParametersPanel";
                var panel = _controls.Get<StackPanel>(panelName);

                if (panel == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"GetExistingParametersFromPanel: {panelName} not found");
                    return parameters;
                }

                foreach (var child in panel.Children)
                {
                    if (child is Grid paramGrid)
                    {
                        var textBoxes = paramGrid.Children.OfType<TextBox>().ToList();
                        if (textBoxes.Count >= 2)
                        {
                            var keyTextBox = textBoxes[0];
                            var valueTextBox = textBoxes[1];

                            if (!string.IsNullOrWhiteSpace(keyTextBox.Text))
                            {
                                string key = keyTextBox.Text.Trim();
                                string value = (valueTextBox.Text ?? "").Trim();

                                if (!parameters.ContainsKey(key))
                                {
                                    parameters[key] = value;
                                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                                        $"GetExistingParametersFromPanel: Found parameter '{key}' = '{value}'");
                                }
                            }
                        }
                    }
                }

                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"GetExistingParametersFromPanel: Extracted {parameters.Count} existing parameters");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"GetExistingParametersFromPanel: Error extracting parameters - {ex.Message}");
            }

            return parameters;
        }

        /// <summary>
        /// Shows parameter browser window for selecting parameters (modeless - Issue #20).
        /// </summary>
        private void ShowParameterBrowser(ScriptParameterDeclarations? declarations, bool isCondition)
        {
            try
            {
                // Get script name from the appropriate textbox
                string scriptName = "";
                if (isCondition)
                {
                    var scriptTextBox = _controls.Get<TextBox>("ScriptAppearsTextBox");
                    scriptName = scriptTextBox?.Text ?? "";
                }
                else
                {
                    var scriptTextBox = _controls.Get<TextBox>("ScriptActionTextBox");
                    scriptName = scriptTextBox?.Text ?? "";
                }

                // Get existing parameters from the node for dependency resolution
                var existingParameters = GetExistingParametersFromPanel(isCondition);

                // Close existing browser if one is already open
                CloseActiveParameterBrowser();

                // Create and show modeless browser window (Issue #20)
                var browser = new ParameterBrowserWindow();
                browser.SetDeclarations(declarations, scriptName, isCondition, existingParameters);

                // Track the window and handle cleanup when it closes
                _activeParameterBrowserWindow = browser;
                browser.Closed += (s, e) =>
                {
                    _activeParameterBrowserWindow = null;

                    if (browser.DialogResult && !string.IsNullOrEmpty(browser.SelectedKey))
                    {
                        // Add the parameter to the appropriate panel
                        var key = browser.SelectedKey;
                        var value = browser.SelectedValue ?? "";

                        // Find the appropriate panel
                        var panelName = isCondition ? "ConditionsParametersPanel" : "ActionsParametersPanel";
                        var panel = _controls.Get<StackPanel>(panelName);

                        if (panel != null)
                        {
                            // User-initiated add from browser - focus the new row
                            _parameterUIManager.AddParameterRow(panel, key, value, isCondition, focusNewRow: true);
                            OnParameterChanged(isCondition);

                            var paramType = isCondition ? "condition" : "action";
                            ViewModel.StatusMessage = $"Added {paramType} parameter: {key}={value}";
                            UnifiedLogger.LogApplication(LogLevel.INFO,
                                $"Added parameter from browser - Type: {paramType}, Key: '{key}', Value: '{value}'");
                        }
                    }
                };

                browser.Show();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Error showing parameter browser: {ex.Message}");
                ViewModel.StatusMessage = "Error showing parameter browser";
            }
        }

        /// <summary>
        /// Closes any active parameter browser window.
        /// </summary>
        public void CloseActiveParameterBrowser()
        {
            if (_activeParameterBrowserWindow != null)
            {
                _activeParameterBrowserWindow.Close();
                _activeParameterBrowserWindow = null;
            }
        }

        /// <summary>
        /// Called when parameter values change in the UI.
        /// Delegates to ScriptParameterUIManager for model updates.
        /// </summary>
        public void OnParameterChanged(bool isCondition)
        {
            _parameterUIManager.OnParameterChanged(isCondition, SelectedNode);
        }

        #endregion

        #region Parameter Declarations

        /// <summary>
        /// Loads parameter declarations for a script asynchronously.
        /// Caches declarations for use by parameter browser.
        /// </summary>
        public async Task LoadParameterDeclarationsAsync(string scriptName, bool isCondition)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                if (isCondition)
                {
                    _currentConditionDeclarations = null;
                }
                else
                {
                    _currentActionDeclarations = null;
                }
                return;
            }

            try
            {
                var declarations = await ScriptService.Instance.GetParameterDeclarationsAsync(scriptName);

                if (isCondition)
                {
                    _currentConditionDeclarations = declarations;
                }
                else
                {
                    _currentActionDeclarations = declarations;
                }

                if (declarations.HasDeclarations)
                {
                    var totalValues = declarations.ValuesByKey.Values.Sum(list => list.Count);
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Loaded parameter declarations for {(isCondition ? "condition" : "action")} script '{scriptName}': {declarations.Keys.Count} keys, {totalValues} values");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Failed to load parameter declarations for '{scriptName}': {ex.Message}");
            }
        }

        #endregion
    }
}
