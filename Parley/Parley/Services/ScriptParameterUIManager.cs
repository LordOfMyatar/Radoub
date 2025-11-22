using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using DialogEditor.Models;
using DialogEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages script parameter UI components and synchronization with dialog model.
    /// Handles both conditional (DialogPtr) and action (DialogNode) parameters.
    /// Extracted from MainWindow.axaml.cs to reduce duplication and improve testability.
    /// </summary>
    public class ScriptParameterUIManager
    {
        private readonly Func<string, Control?> _findControl;
        private readonly Action<string> _setStatusMessage;
        private readonly Action _triggerAutoSave;
        private readonly Func<bool> _isPopulatingProperties;
        private readonly Func<TreeViewSafeNode?> _getSelectedNode;

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
        }

        /// <summary>
        /// Adds a parameter row button click handler for conditional parameters
        /// </summary>
        public void OnAddConditionsParamClick()
        {
            var panel = _findControl("ConditionsParametersPanel") as StackPanel;
            if (panel != null)
            {
                AddParameterRow(panel, "", "", true);
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
                AddParameterRow(panel, "", "", false);
                _setStatusMessage("Added action parameter - enter key and value, then click elsewhere to save");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, "OnAddActionsParamClick: ActionsParametersPanel NOT FOUND");
            }
        }

        /// <summary>
        /// Adds a parameter row to the UI
        /// </summary>
        public void AddParameterRow(StackPanel parent, string key, string value, bool isCondition)
        {
            // Create grid: [Key TextBox] [=] [Value TextBox] [Delete Button]
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
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
            keyTextBox.LostFocus += (s, e) => OnParameterChanged(isCondition, _getSelectedNode());
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

            // Delete button
            var deleteButton = new Button
            {
                Content = "×",
                Width = 25,
                Height = 25,
                FontSize = 16,
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
        }

        /// <summary>
        /// Called when parameter values change in the UI
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

            var dialogNode = selectedNode.OriginalNode;
            var sourcePtr = selectedNode.SourcePointer;

            if (isCondition)
            {
                // Conditional parameters are on the DialogPtr
                if (sourcePtr != null)
                {
                    UpdateConditionParamsFromUI(sourcePtr);
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
                UpdateActionParamsFromUI(dialogNode);
                _setStatusMessage("Action parameters updated");
            }

            _triggerAutoSave();
        }

        /// <summary>
        /// Updates conditional parameters from UI to model
        /// </summary>
        public void UpdateConditionParamsFromUI(DialogPtr ptr, string? scriptName = null)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"UpdateConditionParamsFromUI: ENTRY - ptr has {ptr.ConditionParams.Count} existing params");

            ptr.ConditionParams.Clear();
            var panel = _findControl("ConditionsParametersPanel") as StackPanel;
            if (panel == null)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    "UpdateConditionParamsFromUI: ConditionsParametersPanel NOT FOUND - parameters will be empty!");
                return;
            }

            // Get script name from UI if not provided
            if (string.IsNullOrEmpty(scriptName))
            {
                var scriptTextBox = _findControl("ScriptAppearsTextBox") as TextBox;
                scriptName = scriptTextBox?.Text;
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"UpdateConditionParamsFromUI: Found ConditionsParametersPanel with {panel.Children.Count} children");

            var autoTrimCheckBox = _findControl("AutoTrimConditionsCheckBox") as CheckBox;
            bool autoTrim = autoTrimCheckBox?.IsChecked ?? true;

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"UpdateConditionParamsFromUI: Auto-Trim = {autoTrim}");

            ProcessParameterPanel(panel, ptr.ConditionParams, autoTrim, scriptName);

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"UpdateConditionParamsFromUI: EXIT - ptr now has {ptr.ConditionParams.Count} params");
        }

        /// <summary>
        /// Updates action parameters from UI to model
        /// </summary>
        public void UpdateActionParamsFromUI(DialogNode node, string? scriptName = null)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"UpdateActionParamsFromUI: ENTRY - node '{node.DisplayText}' has {node.ActionParams.Count} existing params");

            node.ActionParams.Clear();
            var panel = _findControl("ActionsParametersPanel") as StackPanel;
            if (panel == null)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    "UpdateActionParamsFromUI: ActionsParametersPanel NOT FOUND - parameters will be empty!");
                return;
            }

            // Get script name from UI if not provided
            if (string.IsNullOrEmpty(scriptName))
            {
                var scriptTextBox = _findControl("ScriptActionTextBox") as TextBox;
                scriptName = scriptTextBox?.Text;
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"UpdateActionParamsFromUI: Found ActionsParametersPanel with {panel.Children.Count} children");

            var autoTrimCheckBox = _findControl("AutoTrimActionsCheckBox") as CheckBox;
            bool autoTrim = autoTrimCheckBox?.IsChecked ?? true;

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"UpdateActionParamsFromUI: Auto-Trim = {autoTrim}");

            ProcessParameterPanel(panel, node.ActionParams, autoTrim, scriptName);

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"UpdateActionParamsFromUI: EXIT - node '{node.DisplayText}' now has {node.ActionParams.Count} params");
        }

        /// <summary>
        /// Processes a parameter panel and updates the parameter dictionary
        /// </summary>
        private void ProcessParameterPanel(StackPanel panel, Dictionary<string, string> paramDict, bool autoTrim, string? scriptName)
        {
            foreach (var child in panel.Children)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"ProcessParameterPanel: Child type={child.GetType().Name}");

                if (child is Grid paramGrid)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"ProcessParameterPanel: Grid has {paramGrid.Children.Count} children");

                    if (paramGrid.Children.Count >= 3)
                    {
                        var keyTextBox = paramGrid.Children[0] as TextBox;
                        var valueTextBox = paramGrid.Children[1] as TextBox;

                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"ProcessParameterPanel: Examining param row - keyText='{keyTextBox?.Text ?? "null"}', valueText='{valueTextBox?.Text ?? "null"}'");

                        if (keyTextBox != null && valueTextBox != null &&
                            !string.IsNullOrWhiteSpace(keyTextBox.Text))
                        {
                            string key = keyTextBox.Text;
                            string value = valueTextBox.Text ?? "";

                            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Before trim: key='{key}', value='{value}'");

                            // Apply trimming if Auto-Trim is enabled
                            if (autoTrim)
                            {
                                string originalKey = key;
                                string originalValue = value;

                                key = key.Trim();
                                value = value.Trim();

                                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"After trim: key='{key}', value='{value}'");

                                // Update the UI textboxes to show trimmed values
                                keyTextBox.Text = key;
                                valueTextBox.Text = value;

                                // Show visual feedback if text was actually trimmed
                                if (originalKey != key)
                                {
                                    _ = ShowTrimFeedbackAsync(keyTextBox);
                                }
                                if (originalValue != value)
                                {
                                    _ = ShowTrimFeedbackAsync(valueTextBox);
                                }
                            }

                            paramDict[key] = value;
                            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"ProcessParameterPanel: Added param '{key}' = '{value}'");

                            // Cache parameter value if script name is known
                            if (!string.IsNullOrWhiteSpace(scriptName) && !string.IsNullOrWhiteSpace(value))
                            {
                                ParameterCacheService.Instance.AddValue(scriptName, key, value);
                            }
                        }
                    }
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"ProcessParameterPanel: Child is not Grid, type={child.GetType().Name}");
                }
            }
        }

        /// <summary>
        /// Shows visual feedback when parameter text is trimmed.
        /// Briefly flashes the TextBox border to indicate successful trim operation.
        /// </summary>
        private async Task ShowTrimFeedbackAsync(TextBox textBox)
        {
            // Store original border properties
            var originalBrush = textBox.BorderBrush;
            var originalThickness = textBox.BorderThickness;

            try
            {
                // Flash green border to indicate trim occurred
                textBox.BorderBrush = Brushes.LightGreen;
                textBox.BorderThickness = new Thickness(2);

                // Wait briefly for visual feedback
                await Task.Delay(300);

                // Restore original appearance
                textBox.BorderBrush = originalBrush;
                textBox.BorderThickness = originalThickness;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"ShowTrimFeedback: Error showing visual feedback - {ex.Message}");
                // Ensure we restore original state even if error occurs
                textBox.BorderBrush = originalBrush;
                textBox.BorderThickness = originalThickness;
            }
        }
    }
}
