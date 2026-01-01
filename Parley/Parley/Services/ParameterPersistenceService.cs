using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DialogEditor.Models;
using Radoub.Formats.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DialogEditor.Services
{
    /// <summary>
    /// Handles persistence of script parameters between UI and dialog model.
    /// Extracted from ScriptParameterUIManager.cs for maintainability.
    /// Issue #287: Proper TextBox finding regardless of Grid column assignment
    /// </summary>
    public class ParameterPersistenceService
    {
        private readonly Func<string, Control?> _findControl;
        private readonly ParameterValidationService _validationService;

        public ParameterPersistenceService(
            Func<string, Control?> findControl,
            ParameterValidationService validationService)
        {
            _findControl = findControl ?? throw new ArgumentNullException(nameof(findControl));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
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
        public void ProcessParameterPanel(StackPanel panel, Dictionary<string, string> paramDict, bool autoTrim, string? scriptName)
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
        /// Issue #141: Uses theme success color for colorblind accessibility.
        /// </summary>
        public async Task ShowTrimFeedbackAsync(TextBox textBox)
        {
            // Store original border properties
            var originalBrush = textBox.BorderBrush;
            var originalThickness = textBox.BorderThickness;

            try
            {
                // Flash success border to indicate trim occurred (theme-aware for accessibility)
                textBox.BorderBrush = _validationService.GetSuccessBrush();
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
