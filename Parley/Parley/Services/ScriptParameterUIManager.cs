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
        /// Issue #287: Added duplicate key validation
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

            // Issue #287: Validate for duplicate keys on text change and focus lost
            keyTextBox.TextChanged += (s, e) =>
            {
                // Re-validate on every change to clear red border when duplicate is resolved
                ValidateDuplicateKeys(parent, keyTextBox, isCondition);
            };
            keyTextBox.LostFocus += (s, e) =>
            {
                ValidateDuplicateKeys(parent, keyTextBox, isCondition);
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

        /// <summary>
        /// Validates that a key is not duplicated in the parameter panel.
        /// Issue #287: Prevents duplicate keys which would cause data loss.
        /// Red border stays until the duplicate is corrected.
        /// </summary>
        private void ValidateDuplicateKeys(StackPanel parent, TextBox currentKeyTextBox, bool isCondition)
        {
            string currentKey = currentKeyTextBox.Text?.Trim() ?? "";

            // If key is empty, clear any warning state
            if (string.IsNullOrWhiteSpace(currentKey))
            {
                ClearDuplicateWarning(currentKeyTextBox);
                return;
            }

            int duplicateCount = 0;
            var allKeyTextBoxes = new List<TextBox>();

            foreach (var child in parent.Children)
            {
                if (child is Grid paramGrid)
                {
                    var textBoxes = paramGrid.Children.OfType<TextBox>().ToList();
                    if (textBoxes.Count >= 1)
                    {
                        var keyTextBox = textBoxes[0];
                        string key = keyTextBox.Text?.Trim() ?? "";

                        if (key.Equals(currentKey, StringComparison.Ordinal))
                        {
                            duplicateCount++;
                            allKeyTextBoxes.Add(keyTextBox);
                        }
                    }
                }
            }

            if (duplicateCount > 1)
            {
                // Show warning - duplicate key detected (stays red until corrected)
                currentKeyTextBox.BorderBrush = Brushes.Red;
                currentKeyTextBox.BorderThickness = new Thickness(2);

                // Also mark all other textboxes with the same key
                foreach (var tb in allKeyTextBoxes)
                {
                    tb.BorderBrush = Brushes.Red;
                    tb.BorderThickness = new Thickness(2);
                }

                _setStatusMessage($"⚠️ Duplicate key '{currentKey}' - only one value will be saved!");

                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Duplicate key detected: '{currentKey}' appears {duplicateCount} times in {(isCondition ? "condition" : "action")} parameters");
            }
            else
            {
                // No duplicate - clear any warning state on this textbox
                ClearDuplicateWarning(currentKeyTextBox);
            }
        }

        /// <summary>
        /// Clears the duplicate key warning visual state from a TextBox.
        /// </summary>
        private void ClearDuplicateWarning(TextBox textBox)
        {
            // Only clear if currently showing red border (duplicate warning)
            if (textBox.BorderBrush == Brushes.Red)
            {
                textBox.BorderBrush = null; // Reset to default theme
                textBox.BorderThickness = new Thickness(1);
            }
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
        /// Issue #287: Fixed to properly find TextBox children regardless of Grid column assignment
        /// </summary>
        private void ProcessParameterPanel(StackPanel panel, Dictionary<string, string> paramDict, bool autoTrim, string? scriptName)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"ProcessParameterPanel: Processing {panel.Children.Count} children");

            foreach (var child in panel.Children)
            {
                if (child is Grid paramGrid)
                {
                    // Issue #287: Find TextBox children by type, not by index
                    // Grid children order is not guaranteed to match visual column order
                    var textBoxes = paramGrid.Children.OfType<TextBox>().ToList();

                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"ProcessParameterPanel: Grid has {paramGrid.Children.Count} children, {textBoxes.Count} TextBoxes");

                    if (textBoxes.Count >= 2)
                    {
                        // First TextBox is key (column 0), second is value (column 2)
                        var keyTextBox = textBoxes[0];
                        var valueTextBox = textBoxes[1];

                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"ProcessParameterPanel: keyText='{keyTextBox.Text}', valueText='{valueTextBox.Text}'");

                        if (!string.IsNullOrWhiteSpace(keyTextBox.Text))
                        {
                            string key = keyTextBox.Text;
                            string value = valueTextBox.Text ?? "";

                            // Apply trimming if Auto-Trim is enabled
                            if (autoTrim)
                            {
                                string originalKey = key;
                                string originalValue = value;

                                key = key.Trim();
                                value = value.Trim();

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

                            // Issue #287: Check for duplicate keys
                            if (paramDict.ContainsKey(key))
                            {
                                UnifiedLogger.LogApplication(LogLevel.WARN,
                                    $"ProcessParameterPanel: Duplicate key '{key}' - overwriting previous value '{paramDict[key]}' with '{value}'");
                            }

                            paramDict[key] = value;
                            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"ProcessParameterPanel: Added param '{key}' = '{value}'");

                            // Cache parameter value if script name is known
                            if (!string.IsNullOrWhiteSpace(scriptName) && !string.IsNullOrWhiteSpace(value))
                            {
                                ParameterCacheService.Instance.AddValue(scriptName, key, value);
                            }
                        }
                        else
                        {
                            UnifiedLogger.LogApplication(LogLevel.DEBUG, "ProcessParameterPanel: Skipping row with empty key");
                        }
                    }
                    else
                    {
                        UnifiedLogger.LogApplication(LogLevel.WARN,
                            $"ProcessParameterPanel: Grid has {textBoxes.Count} TextBoxes (expected 2+)");
                    }
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"ProcessParameterPanel: Child is not Grid, type={child.GetType().Name}");
                }
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"ProcessParameterPanel: Finished with {paramDict.Count} parameters");
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
