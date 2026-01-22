using System;
using Avalonia.Controls;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.ViewModels;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.UI.Views;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Manages script browsing UI interactions for MainWindow.
    /// Extracted from MainWindow to reduce method size and improve maintainability (Epic #457, Sprint 3).
    ///
    /// Handles:
    /// 1. Script browser dialog handlers (conditional and action scripts)
    /// 2. Script editor launching (external editor integration)
    /// 3. Conversation-level script browsing
    ///
    /// Related controllers:
    /// - ParameterBrowserController: Parameter browser and suggestion logic
    /// - ScriptPreviewService: Script preview loading
    /// </summary>
    public class ScriptBrowserController
    {
        private readonly Window _window;
        private readonly SafeControlFinder _controls;
        private readonly Func<MainViewModel> _getViewModel;
        private readonly Func<TreeViewSafeNode?> _getSelectedNode;
        private readonly Action<string> _autoSaveProperty;
        private readonly IGameDataService? _gameDataService;

        // Track active script browser window
        private ScriptBrowserWindow? _activeScriptBrowserWindow;

        public ScriptBrowserController(
            Window window,
            SafeControlFinder controls,
            Func<MainViewModel> getViewModel,
            Func<TreeViewSafeNode?> getSelectedNode,
            Action<string> autoSaveProperty,
            IGameDataService? gameDataService = null)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _controls = controls ?? throw new ArgumentNullException(nameof(controls));
            _getViewModel = getViewModel ?? throw new ArgumentNullException(nameof(getViewModel));
            _getSelectedNode = getSelectedNode ?? throw new ArgumentNullException(nameof(getSelectedNode));
            _autoSaveProperty = autoSaveProperty ?? throw new ArgumentNullException(nameof(autoSaveProperty));
            _gameDataService = gameDataService;
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
                var context = new ParleyScriptBrowserContext(ViewModel.CurrentFileName, _gameDataService);
                var scriptBrowser = new ScriptBrowserWindow(context);

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
                var context = new ParleyScriptBrowserContext(ViewModel.CurrentFileName, _gameDataService);
                var scriptBrowser = new ScriptBrowserWindow(context);

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
        public void CloseActiveScriptBrowser()
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

                var context = new ParleyScriptBrowserContext(ViewModel.CurrentFileName, _gameDataService);
                var scriptBrowser = new ScriptBrowserWindow(context);

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
    }
}
