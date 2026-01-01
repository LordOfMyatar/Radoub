using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using DialogEditor.Utils;
using DialogEditor.ViewModels;
using DialogEditor.Views;
using Radoub.UI.Views;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Manages all script browsing UI interactions for MainWindow.
    /// Extracted from MainWindow to reduce method size and improve maintainability (Epic #457, Sprint 3).
    ///
    /// Handles:
    /// 1. Script browser dialog handlers (conditional and action scripts)
    /// 2. Script editor launching (external editor integration)
    /// 3. Parameter browser and suggestion logic
    /// 4. Script preview loading
    /// 5. Parameter declarations caching
    /// </summary>
    public class ScriptBrowserController
    {
        private readonly Window _window;
        private readonly SafeControlFinder _controls;
        private readonly Func<MainViewModel> _getViewModel;
        private readonly Func<TreeViewSafeNode?> _getSelectedNode;
        private readonly Action<string> _autoSaveProperty;
        private readonly Func<bool> _isPopulatingProperties;
        private readonly ScriptParameterUIManager _parameterUIManager;
        private readonly Action _triggerAutoSave;

        // Track active browser windows
        private ParameterBrowserWindow? _activeParameterBrowserWindow;
        private Radoub.UI.Views.ScriptBrowserWindow? _activeScriptBrowserWindow;

        // Parameter autocomplete: Cache of script parameter declarations
        private ScriptParameterDeclarations? _currentConditionDeclarations;
        private ScriptParameterDeclarations? _currentActionDeclarations;

        public ScriptBrowserController(
            Window window,
            SafeControlFinder controls,
            Func<MainViewModel> getViewModel,
            Func<TreeViewSafeNode?> getSelectedNode,
            Action<string> autoSaveProperty,
            Func<bool> isPopulatingProperties,
            ScriptParameterUIManager parameterUIManager,
            Action triggerAutoSave)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _controls = controls ?? throw new ArgumentNullException(nameof(controls));
            _getViewModel = getViewModel ?? throw new ArgumentNullException(nameof(getViewModel));
            _getSelectedNode = getSelectedNode ?? throw new ArgumentNullException(nameof(getSelectedNode));
            _autoSaveProperty = autoSaveProperty ?? throw new ArgumentNullException(nameof(autoSaveProperty));
            _isPopulatingProperties = isPopulatingProperties ?? throw new ArgumentNullException(nameof(isPopulatingProperties));
            _parameterUIManager = parameterUIManager ?? throw new ArgumentNullException(nameof(parameterUIManager));
            _triggerAutoSave = triggerAutoSave ?? throw new ArgumentNullException(nameof(triggerAutoSave));
        }

        private MainViewModel ViewModel => _getViewModel();
        private TreeViewSafeNode? SelectedNode => _getSelectedNode();

        #region Script Browser Handlers

        /// <summary>
        /// Opens script browser for conditional scripts (on DialogPtr).
        /// Core Feature: Conditional scripts only apply to linked nodes.
        /// </summary>
        public void OnBrowseConditionalScriptClick()
        {
            if (SelectedNode == null)
            {
                ViewModel.StatusMessage = "Please select a dialog node first";
                return;
            }

            if (SelectedNode is TreeViewRootNode)
            {
                ViewModel.StatusMessage = "Cannot assign conditional scripts to ROOT. Select a dialog node instead.";
                return;
            }

            // Check if we have a pointer context
            if (SelectedNode.SourcePointer == null)
            {
                ViewModel.StatusMessage = "No pointer context - conditional scripts only apply to linked nodes";
                return;
            }

            try
            {
                // Close existing browser if one is already open
                CloseActiveScriptBrowser();

                // Create and show modeless browser window (Issue #20)
                var context = new ParleyScriptBrowserContext(ViewModel.CurrentFileName);
                var scriptBrowser = new Radoub.UI.Views.ScriptBrowserWindow(context);

                // Track the window and handle cleanup when it closes
                _activeScriptBrowserWindow = scriptBrowser;
                scriptBrowser.Closed += (s, e) =>
                {
                    var result = scriptBrowser.SelectedScript;
                    _activeScriptBrowserWindow = null;

                    if (!string.IsNullOrEmpty(result))
                    {
                        // Update the conditional script field with selected script
                        var scriptTextBox = _controls.Get<TextBox>("ScriptAppearsTextBox");
                        if (scriptTextBox != null)
                        {
                            scriptTextBox.Text = result;
                            // Trigger auto-save
                            _autoSaveProperty("ScriptAppearsTextBox");
                        }
                        ViewModel.StatusMessage = $"Selected conditional script: {result}";
                    }
                };

                scriptBrowser.Show();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening script browser: {ex.Message}");
                ViewModel.StatusMessage = $"Error opening script browser: {ex.Message}";
            }
        }

        /// <summary>
        /// Opens script browser for action scripts (on DialogNode).
        /// </summary>
        public void OnBrowseActionScriptClick()
        {
            if (SelectedNode == null)
            {
                ViewModel.StatusMessage = "Please select a dialog node first";
                return;
            }

            if (SelectedNode is TreeViewRootNode)
            {
                ViewModel.StatusMessage = "Cannot assign scripts to ROOT. Select a dialog node instead.";
                return;
            }

            try
            {
                // Close existing browser if one is already open
                CloseActiveScriptBrowser();

                // Create and show modeless browser window (Issue #20)
                var context = new ParleyScriptBrowserContext(ViewModel.CurrentFileName);
                var scriptBrowser = new Radoub.UI.Views.ScriptBrowserWindow(context);

                // Track the window and handle cleanup when it closes
                _activeScriptBrowserWindow = scriptBrowser;
                scriptBrowser.Closed += (s, e) =>
                {
                    var result = scriptBrowser.SelectedScript;
                    _activeScriptBrowserWindow = null;

                    if (!string.IsNullOrEmpty(result))
                    {
                        // Update the script action field with selected script
                        var scriptTextBox = _controls.Get<TextBox>("ScriptActionTextBox");
                        if (scriptTextBox != null)
                        {
                            scriptTextBox.Text = result;
                            // Trigger auto-save
                            _autoSaveProperty("ScriptActionTextBox");
                        }
                        ViewModel.StatusMessage = $"Selected script: {result}";
                    }
                };

                scriptBrowser.Show();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening script browser: {ex.Message}");
                ViewModel.StatusMessage = $"Error opening script browser: {ex.Message}";
            }
        }

        /// <summary>
        /// Opens conditional script in external editor.
        /// </summary>
        public void OnEditConditionalScriptClick()
        {
            var scriptTextBox = _controls.Get<TextBox>("ScriptAppearsTextBox");
            string? scriptName = scriptTextBox?.Text;

            if (string.IsNullOrWhiteSpace(scriptName))
            {
                ViewModel.StatusMessage = "No conditional script assigned";
                return;
            }

            bool success = ExternalEditorService.Instance.OpenScript(scriptName, ViewModel.CurrentFileName);

            if (success)
            {
                ViewModel.StatusMessage = $"Opened '{scriptName}' in editor";
            }
            else
            {
                ViewModel.StatusMessage = $"Could not find script '{scriptName}.nss'";
            }
        }

        /// <summary>
        /// Opens action script in external editor.
        /// </summary>
        public void OnEditActionScriptClick()
        {
            var scriptTextBox = _controls.Get<TextBox>("ScriptActionTextBox");
            string? scriptName = scriptTextBox?.Text;

            if (string.IsNullOrWhiteSpace(scriptName))
            {
                ViewModel.StatusMessage = "No action script assigned";
                return;
            }

            bool success = ExternalEditorService.Instance.OpenScript(scriptName, ViewModel.CurrentFileName);

            if (success)
            {
                ViewModel.StatusMessage = $"Opened '{scriptName}' in editor";
            }
            else
            {
                ViewModel.StatusMessage = $"Could not find script '{scriptName}.nss'";
            }
        }

        /// <summary>
        /// Closes any active script browser window.
        /// </summary>
        private void CloseActiveScriptBrowser()
        {
            if (_activeScriptBrowserWindow != null)
            {
                _activeScriptBrowserWindow.Close();
                _activeScriptBrowserWindow = null;
            }
        }

        /// <summary>
        /// Opens script browser for conversation-level scripts (ScriptEnd, ScriptAbort).
        /// </summary>
        public void OnBrowseConversationScriptClick(string? fieldName)
        {
            try
            {
                CloseActiveScriptBrowser();

                var context = new ParleyScriptBrowserContext(ViewModel.CurrentFileName);
                var scriptBrowser = new Radoub.UI.Views.ScriptBrowserWindow(context);

                _activeScriptBrowserWindow = scriptBrowser;
                scriptBrowser.Closed += (s, e) =>
                {
                    var result = scriptBrowser.SelectedScript;
                    _activeScriptBrowserWindow = null;

                    if (!string.IsNullOrEmpty(result))
                    {
                        if (fieldName == "ScriptEnd")
                        {
                            var scriptEndTextBox = _controls.Get<TextBox>("ScriptEndTextBox");
                            if (scriptEndTextBox != null)
                            {
                                scriptEndTextBox.Text = result;
                                if (ViewModel.CurrentDialog != null)
                                {
                                    ViewModel.CurrentDialog.ScriptEnd = result;
                                }
                            }
                        }
                        else if (fieldName == "ScriptAbort")
                        {
                            var scriptAbortTextBox = _controls.Get<TextBox>("ScriptAbortTextBox");
                            if (scriptAbortTextBox != null)
                            {
                                scriptAbortTextBox.Text = result;
                                if (ViewModel.CurrentDialog != null)
                                {
                                    ViewModel.CurrentDialog.ScriptAbort = result;
                                }
                            }
                        }

                        UnifiedLogger.LogApplication(LogLevel.INFO, $"Selected conversation script for {fieldName}: {result}");
                        ViewModel.StatusMessage = $"Selected script: {result}";
                    }
                };

                scriptBrowser.Show();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening script browser: {ex.Message}");
                ViewModel.StatusMessage = $"Error opening script browser: {ex.Message}";
            }
        }

        #endregion

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
        private void CloseActiveParameterBrowser()
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

        #region Script Preview

        /// <summary>
        /// Loads script content preview asynchronously.
        /// </summary>
        public async Task LoadScriptPreviewAsync(string scriptName, bool isCondition)
        {
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                ClearScriptPreview(isCondition);
                return;
            }

            try
            {
                var previewTextBox = isCondition
                    ? _controls.Get<TextBox>("ConditionalScriptPreviewTextBox")
                    : _controls.Get<TextBox>("ActionScriptPreviewTextBox");

                if (previewTextBox == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"LoadScriptPreviewAsync: Preview TextBox not found for {(isCondition ? "conditional" : "action")} script");
                    return;
                }

                previewTextBox.Text = "Loading...";

                var scriptContent = await ScriptService.Instance.GetScriptContentAsync(scriptName);

                if (!string.IsNullOrEmpty(scriptContent))
                {
                    previewTextBox.Text = scriptContent;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"LoadScriptPreviewAsync: Loaded preview for {(isCondition ? "conditional" : "action")} script '{scriptName}'");
                }
                else
                {
                    previewTextBox.Text = $"// Script '{scriptName}.nss' not found or could not be loaded.\n" +
                                          "// This may be a compiled game resource (.ncs) without source available.\n" +
                                          "// Use nwnnsscomp to decompile .ncs files: github.com/niv/neverwinter.nim";
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"LoadScriptPreviewAsync: No content for script '{scriptName}'");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"LoadScriptPreviewAsync: Error loading preview for '{scriptName}': {ex.Message}");
                ClearScriptPreview(isCondition);
            }
        }

        /// <summary>
        /// Clears script preview content.
        /// </summary>
        public void ClearScriptPreview(bool isCondition)
        {
            var previewTextBox = isCondition
                ? _controls.Get<TextBox>("ConditionalScriptPreviewTextBox")
                : _controls.Get<TextBox>("ActionScriptPreviewTextBox");

            if (previewTextBox != null)
            {
                previewTextBox.Text = $"// {(isCondition ? "Conditional" : "Action")} script preview will appear here";
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Closes all active browser windows. Call on window close.
        /// </summary>
        public void CloseAllBrowserWindows()
        {
            CloseActiveScriptBrowser();
            CloseActiveParameterBrowser();
        }

        #endregion
    }
}
