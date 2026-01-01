using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DialogEditor.Models;
using DialogEditor.Utils;
using System;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages script parameter UI components and synchronization with dialog model.
    /// Handles both conditional (DialogPtr) and action (DialogNode) parameters.
    /// Extracted from MainWindow.axaml.cs to reduce duplication and improve testability.
    ///
    /// Refactored in #706: Validation logic extracted to ParameterValidationService,
    /// persistence logic extracted to ParameterPersistenceService.
    /// </summary>
    public class ScriptParameterUIManager
    {
        private readonly Func<string, Control?> _findControl;
        private readonly Action<string> _setStatusMessage;
        private readonly Action _triggerAutoSave;
        private readonly Func<bool> _isPopulatingProperties;
        private readonly Func<TreeViewSafeNode?> _getSelectedNode;

        private readonly ParameterValidationService _validationService;
        private readonly ParameterPersistenceService _persistenceService;

        public ScriptParameterUIManager(
            Func<string, Control?> findControl,
            Action<string> setStatusMessage,
            Action triggerAutoSave,
            Func<bool> isPopulatingProperties,
            Func<TreeViewSafeNode?> getSelectedNode)
        {
            _findControl = findControl ?? throw new ArgumentNullException(nameof(findControl));
            _setStatusMessage = setStatusMessage ?? throw new ArgumentNullException(nameof(setStatusMessage));
            _triggerAutoSave = triggerAutoSave ?? throw new ArgumentNullException(nameof(triggerAutoSave));
            _isPopulatingProperties = isPopulatingProperties ?? throw new ArgumentNullException(nameof(isPopulatingProperties));
            _getSelectedNode = getSelectedNode ?? throw new ArgumentNullException(nameof(getSelectedNode));

            _validationService = new ParameterValidationService(setStatusMessage);
            _persistenceService = new ParameterPersistenceService(findControl, _validationService);
        }

        /// <summary>
        /// Adds a parameter row button click handler for conditional parameters
        /// </summary>
        public void OnAddConditionsParamClick()
        {
            var panel = _findControl("ConditionsParametersPanel") as StackPanel;
            if (panel != null)
            {
                AddParameterRow(panel, "", "", true, focusNewRow: true);
                _setStatusMessage("Added condition parameter - enter key and value, then click elsewhere to save");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, "OnAddConditionsParamClick: ConditionsParametersPanel NOT FOUND");
            }
        }

        /// <summary>
        /// Adds a parameter row button click handler for action parameters
        /// </summary>
        public void OnAddActionsParamClick()
        {
            var panel = _findControl("ActionsParametersPanel") as StackPanel;
            if (panel != null)
            {
                AddParameterRow(panel, "", "", false, focusNewRow: true);
                _setStatusMessage("Added action parameter - enter key and value, then click elsewhere to save");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, "OnAddActionsParamClick: ActionsParametersPanel NOT FOUND");
            }
        }

        /// <summary>
        /// Adds a parameter row to the UI
        /// Issue #287: Added duplicate key validation
        /// Issue #664: Added focusNewRow parameter to prevent focus stealing during population
        /// </summary>
        /// <param name="parent">The panel to add the row to</param>
        /// <param name="key">Parameter key</param>
        /// <param name="value">Parameter value</param>
        /// <param name="isCondition">True for condition params, false for action params</param>
        /// <param name="focusNewRow">True to focus the new row (user-initiated add), false to skip focus (population)</param>
        public void AddParameterRow(StackPanel parent, string key, string value, bool isCondition, bool focusNewRow = false)
        {
            // Create grid: [Key TextBox] [=] [Value TextBox] [Delete Button]
            // Use Auto with MinWidth so fields start compact but can expand with content
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 100 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 100 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Key textbox
            var keyTextBox = new TextBox
            {
                Text = key,
                Watermark = "Parameter key",
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                [Grid.ColumnProperty] = 0
            };

            // Issue #287: Validate for duplicate keys on text change and focus lost
            keyTextBox.TextChanged += (s, e) =>
            {
                // Re-validate on every change to clear red border when duplicate is resolved
                _validationService.ValidateDuplicateKeys(parent, keyTextBox, isCondition);
            };
            keyTextBox.LostFocus += (s, e) =>
            {
                _validationService.ValidateDuplicateKeys(parent, keyTextBox, isCondition);
                OnParameterChanged(isCondition, _getSelectedNode());
            };
            grid.Children.Add(keyTextBox);

            // Value textbox
            var valueTextBox = new TextBox
            {
                Text = value,
                Watermark = "Parameter value",
                FontFamily = new FontFamily("Consolas,Courier New,monospace"),
                [Grid.ColumnProperty] = 2
            };
            valueTextBox.LostFocus += (s, e) => OnParameterChanged(isCondition, _getSelectedNode());
            grid.Children.Add(valueTextBox);

            // Delete button - use "X" for better legibility across fonts
            var deleteButton = new Button
            {
                Content = "X",
                Width = 25,
                Height = 25,
                FontSize = 12,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                Padding = new Thickness(0),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                [Grid.ColumnProperty] = 4
            };
            deleteButton.Click += (s, e) =>
            {
                parent.Children.Remove(grid);
                OnParameterChanged(isCondition, _getSelectedNode());
                _setStatusMessage($"Removed {(isCondition ? "condition" : "action")} parameter");
            };
            grid.Children.Add(deleteButton);

            parent.Children.Add(grid);

            // Issue #287: Auto-scroll to the new row and focus the key textbox
            // Issue #664: Only focus when user explicitly adds a row, not during population
            if (focusNewRow)
            {
                // Use dispatcher to ensure the visual tree is updated before scrolling
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    // Find the parent ScrollViewer
                    var scrollViewerName = isCondition ? "ConditionsParamsScrollViewer" : "ActionsParamsScrollViewer";
                    var scrollViewer = _findControl(scrollViewerName) as ScrollViewer;
                    if (scrollViewer != null)
                    {
                        // Scroll to the bottom where the new row was added
                        scrollViewer.ScrollToEnd();
                    }

                    // Focus the key textbox
                    keyTextBox.Focus();
                }, Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// Issue #289: Checks if either parameter panel (conditions or actions) has duplicate keys.
        /// Called by MainWindow before saving to prevent data corruption.
        /// </summary>
        public bool HasAnyDuplicateKeys()
        {
            var conditionsPanel = _findControl("ConditionsParametersPanel") as StackPanel;
            var actionsPanel = _findControl("ActionsParametersPanel") as StackPanel;

            if (conditionsPanel != null && _validationService.HasDuplicateKeys(conditionsPanel))
                return true;

            if (actionsPanel != null && _validationService.HasDuplicateKeys(actionsPanel))
                return true;

            return false;
        }

        /// <summary>
        /// Called when parameter values change in the UI
        /// Issue #287: Blocks save when duplicate keys exist to prevent data corruption
        /// </summary>
        public void OnParameterChanged(bool isCondition, TreeViewSafeNode? selectedNode = null)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"OnParameterChanged: ENTRY - isCondition={isCondition}, selectedNode={(selectedNode?.OriginalNode.DisplayText ?? "null")}, isPopulating={_isPopulatingProperties()}");

            if (selectedNode == null || _isPopulatingProperties())
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"OnParameterChanged: Early return - selectedNode={(selectedNode == null ? "null" : "not null")}, isPopulating={_isPopulatingProperties()}");
                return;
            }

            // Issue #287: Check for duplicate keys BEFORE saving - block save if duplicates exist
            var panelName = isCondition ? "ConditionsParametersPanel" : "ActionsParametersPanel";
            var panel = _findControl(panelName) as StackPanel;
            if (panel != null && _validationService.HasDuplicateKeys(panel))
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"OnParameterChanged: BLOCKED - Duplicate keys detected in {panelName}. Parameters NOT saved.");
                _setStatusMessage("⛔ Cannot save: Fix duplicate keys first!");
                return; // Don't save, don't trigger autosave
            }

            var dialogNode = selectedNode.OriginalNode;
            var sourcePtr = selectedNode.SourcePointer;

            if (isCondition)
            {
                // Conditional parameters are on the DialogPtr
                if (sourcePtr != null)
                {
                    _persistenceService.UpdateConditionParamsFromUI(sourcePtr);
                    _setStatusMessage("Condition parameters updated");
                }
                else
                {
                    _setStatusMessage("No pointer context for conditional parameters");
                }
            }
            else
            {
                // Action parameters are on the DialogNode
                _persistenceService.UpdateActionParamsFromUI(dialogNode);
                _setStatusMessage("Action parameters updated");
            }

            _triggerAutoSave();
        }

        /// <summary>
        /// Updates conditional parameters from UI to model.
        /// Delegated to ParameterPersistenceService.
        /// </summary>
        public void UpdateConditionParamsFromUI(DialogPtr ptr, string? scriptName = null)
        {
            _persistenceService.UpdateConditionParamsFromUI(ptr, scriptName);
        }

        /// <summary>
        /// Updates action parameters from UI to model.
        /// Delegated to ParameterPersistenceService.
        /// </summary>
        public void UpdateActionParamsFromUI(DialogNode node, string? scriptName = null)
        {
            _persistenceService.UpdateActionParamsFromUI(node, scriptName);
        }
    }
}
