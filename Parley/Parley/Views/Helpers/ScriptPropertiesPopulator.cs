using System;
using Avalonia.Controls;
using DialogEditor.Models;
using Radoub.Formats.Logging;
using Parley.Models;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Populates script and parameter properties in the Properties Panel.
    /// Extracted from PropertyPanelPopulator to reduce class size (Epic #1219, Sprint 2.2 #1227).
    ///
    /// Handles:
    /// 1. Action script field population (ScriptAction)
    /// 2. Conditional script field population (ScriptAppears from DialogPtr)
    /// 3. Script parameter grid population (condition and action params)
    /// 4. Script preview and parameter declaration loading callbacks
    /// </summary>
    public class ScriptPropertiesPopulator
    {
        private readonly Window _window;

        public ScriptPropertiesPopulator(Window window)
        {
            _window = window;
        }

        /// <summary>
        /// Populates script fields with callbacks for parameter loading.
        /// </summary>
        public void PopulateScripts(DialogNode dialogNode, TreeViewSafeNode node,
            Action<string, bool> loadParameterDeclarations,
            Action<string, bool> loadScriptPreview,
            Action<bool> clearScriptPreview)
        {
            // Action script
            var scriptTextBox = _window.FindControl<TextBox>("ScriptActionTextBox");
            if (scriptTextBox != null)
            {
                scriptTextBox.Text = dialogNode.ScriptAction ?? "";
                scriptTextBox.IsReadOnly = false;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"PopulateProperties: Set Script Action field to '{dialogNode.ScriptAction}' for node '{dialogNode.DisplayText}'");

                if (!string.IsNullOrWhiteSpace(dialogNode.ScriptAction))
                {
                    loadParameterDeclarations(dialogNode.ScriptAction, false);
                    loadScriptPreview(dialogNode.ScriptAction, false);
                }
                else
                {
                    clearScriptPreview(false);
                }
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, "PopulateProperties: ScriptActionTextBox control NOT FOUND!");
            }

            // Conditional script (from DialogPtr)
            var scriptAppearsTextBox = _window.FindControl<TextBox>("ScriptAppearsTextBox");
            if (scriptAppearsTextBox != null)
            {
                if (node.SourcePointer != null)
                {
                    scriptAppearsTextBox.Text = node.SourcePointer.ScriptAppears ?? "";
                    scriptAppearsTextBox.IsReadOnly = false;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"PopulateProperties: Set Conditional Script to '{node.SourcePointer.ScriptAppears}' from SourcePointer");

                    if (!string.IsNullOrWhiteSpace(node.SourcePointer.ScriptAppears))
                    {
                        loadParameterDeclarations(node.SourcePointer.ScriptAppears, true);
                        loadScriptPreview(node.SourcePointer.ScriptAppears, true);
                    }
                    else
                    {
                        clearScriptPreview(true);
                    }
                }
                else
                {
                    scriptAppearsTextBox.Text = "(No pointer context - root level entry)";
                    scriptAppearsTextBox.IsReadOnly = true;
                    clearScriptPreview(true);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "PopulateProperties: No SourcePointer for conditional script");
                }
            }
        }

        /// <summary>
        /// Populates script parameter grids.
        /// </summary>
        public void PopulateParameterGrids(DialogNode node, DialogPtr? ptr, Action<StackPanel, string, string, bool> addParameterRow)
        {
            var conditionsPanel = _window.FindControl<StackPanel>("ConditionsParametersPanel");
            var actionsPanel = _window.FindControl<StackPanel>("ActionsParametersPanel");

            conditionsPanel?.Children.Clear();
            actionsPanel?.Children.Clear();

            if (ptr != null && ptr.ConditionParams.Count > 0)
            {
                foreach (var kvp in ptr.ConditionParams)
                {
                    addParameterRow(conditionsPanel!, kvp.Key, kvp.Value, true);
                }
            }

            if (node.ActionParams.Count > 0)
            {
                foreach (var kvp in node.ActionParams)
                {
                    addParameterRow(actionsPanel!, kvp.Key, kvp.Value, false);
                }
            }
        }

        /// <summary>
        /// Clears script-related fields and sets them to read-only.
        /// </summary>
        public void ClearScriptFields()
        {
            var scriptTextBox = _window.FindControl<TextBox>("ScriptActionTextBox");
            if (scriptTextBox != null)
            {
                scriptTextBox.Clear();
                scriptTextBox.IsReadOnly = true;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "ClearProperties: Cleared Script field");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, "ClearProperties: ScriptActionTextBox control NOT FOUND!");
            }

            var scriptAppearsTextBox = _window.FindControl<TextBox>("ScriptAppearsTextBox");
            if (scriptAppearsTextBox != null)
            {
                scriptAppearsTextBox.Clear();
                scriptAppearsTextBox.IsReadOnly = true;
            }

            var conditionsPanel = _window.FindControl<StackPanel>("ConditionsParametersPanel");
            conditionsPanel?.Children.Clear();

            var actionsPanel = _window.FindControl<StackPanel>("ActionsParametersPanel");
            actionsPanel?.Children.Clear();

            // Clear script preview TextBoxes
            var conditionalPreview = _window.FindControl<TextBox>("ConditionalScriptPreviewTextBox");
            if (conditionalPreview != null)
                conditionalPreview.Text = "// Conditional script preview will appear here";

            var actionPreview = _window.FindControl<TextBox>("ActionScriptPreviewTextBox");
            if (actionPreview != null)
                actionPreview.Text = "// Action script preview will appear here";
        }
    }
}
