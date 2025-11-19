using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using DialogEditor.Models;
using DialogEditor.Services;
using Parley.Models;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Helper class for populating the Properties Panel in MainWindow.
    /// Extracted from MainWindow to reduce method size and improve maintainability.
    ///
    /// Handles:
    /// 1. Conversation-level settings (PreventZoom, ScriptEnd, ScriptAbort)
    /// 2. Node-specific properties (Speaker, Text, Animation, Scripts, etc.)
    /// 3. Quest integration (Quest tag, entry selection)
    /// 4. Script parameters
    /// 5. Clearing/disabling controls when no selection
    /// </summary>
    public class PropertyPanelPopulator
    {
        private readonly Window _window;

        public PropertyPanelPopulator(Window window)
        {
            _window = window;
        }

        /// <summary>
        /// Populates conversation-level settings (always visible regardless of node selection).
        /// </summary>
        public void PopulateConversationSettings(Dialog? dialog)
        {
            if (dialog == null) return;

            var preventZoomCheckBox = _window.FindControl<CheckBox>("PreventZoomCheckBox");
            if (preventZoomCheckBox != null)
            {
                preventZoomCheckBox.IsChecked = dialog.PreventZoom;
            }

            var scriptEndTextBox = _window.FindControl<TextBox>("ScriptEndTextBox");
            if (scriptEndTextBox != null)
            {
                scriptEndTextBox.Text = dialog.ScriptEnd ?? "";
            }

            var scriptAbortTextBox = _window.FindControl<TextBox>("ScriptAbortTextBox");
            if (scriptAbortTextBox != null)
            {
                scriptAbortTextBox.Text = dialog.ScriptAbort ?? "";
            }
        }

        /// <summary>
        /// Populates node type display (NPC/PC with speaker info).
        /// </summary>
        public void PopulateNodeType(DialogNode dialogNode)
        {
            var nodeTypeTextBlock = _window.FindControl<TextBlock>("NodeTypeTextBlock");
            if (nodeTypeTextBlock != null)
            {
                if (dialogNode.Type == DialogNodeType.Entry)
                {
                    // Entry node = NPC speaking
                    if (!string.IsNullOrWhiteSpace(dialogNode.Speaker))
                    {
                        nodeTypeTextBlock.Text = $"NPC ({dialogNode.Speaker})";
                    }
                    else
                    {
                        nodeTypeTextBlock.Text = "NPC (Owner)";
                    }
                }
                else // Reply node - always PC
                {
                    nodeTypeTextBlock.Text = "PC";
                }
            }
        }

        /// <summary>
        /// Populates speaker field and related controls.
        /// PC nodes have read-only speaker field.
        /// </summary>
        public void PopulateSpeaker(DialogNode dialogNode)
        {
            var speakerTextBox = _window.FindControl<TextBox>("SpeakerTextBox");
            var recentCreatureComboBox = _window.FindControl<ComboBox>("RecentCreatureTagsComboBox");
            var browseCreatureButton = _window.FindControl<Button>("BrowseCreatureButton");

            bool isPC = (dialogNode.Type == DialogNodeType.Reply);

            if (speakerTextBox != null)
            {
                speakerTextBox.Text = dialogNode.Speaker ?? "";
                speakerTextBox.IsReadOnly = isPC;

                if (isPC)
                {
                    speakerTextBox.Watermark = "PC (player character)";
                }
                else
                {
                    speakerTextBox.Watermark = "Character tag or empty for Owner";
                }
            }

            // Disable Speaker dropdown and Browse button for PC nodes
            if (recentCreatureComboBox != null)
            {
                recentCreatureComboBox.IsEnabled = !isPC;
            }

            if (browseCreatureButton != null)
            {
                browseCreatureButton.IsEnabled = !isPC;
            }
        }

        /// <summary>
        /// Populates basic text properties (Text, Sound, Comment, Delay).
        /// </summary>
        public void PopulateBasicProperties(DialogNode dialogNode)
        {
            var textTextBox = _window.FindControl<TextBox>("TextTextBox");
            if (textTextBox != null)
            {
                textTextBox.Text = dialogNode.Text?.GetDefault() ?? "";
                textTextBox.IsReadOnly = false;
            }

            var soundTextBox = _window.FindControl<TextBox>("SoundTextBox");
            if (soundTextBox != null)
            {
                soundTextBox.Text = dialogNode.Sound ?? "";
                soundTextBox.IsReadOnly = false;
            }

            var commentTextBox = _window.FindControl<TextBox>("CommentTextBox");
            if (commentTextBox != null)
            {
                commentTextBox.Text = dialogNode.Comment ?? "";
                commentTextBox.IsReadOnly = false;
            }

            var delayTextBox = _window.FindControl<TextBox>("DelayTextBox");
            if (delayTextBox != null)
            {
                // Display Delay as empty if it's the default value (uint.MaxValue)
                delayTextBox.Text = dialogNode.Delay == uint.MaxValue ? "" : dialogNode.Delay.ToString();
                delayTextBox.IsReadOnly = false;
            }
        }

        /// <summary>
        /// Populates animation properties (Animation selection and Loop checkbox).
        /// </summary>
        public void PopulateAnimation(DialogNode dialogNode)
        {
            var animationComboBox = _window.FindControl<ComboBox>("AnimationComboBox");
            if (animationComboBox != null)
            {
                animationComboBox.SelectedItem = dialogNode.Animation;
                animationComboBox.IsEnabled = true;
            }

            var animationLoopCheckBox = _window.FindControl<CheckBox>("AnimationLoopCheckBox");
            if (animationLoopCheckBox != null)
            {
                animationLoopCheckBox.IsChecked = dialogNode.AnimationLoop;
                animationLoopCheckBox.IsEnabled = true;
            }
        }

        /// <summary>
        /// Populates IsChild warning indicator.
        /// </summary>
        public void PopulateIsChildIndicator(TreeViewSafeNode node)
        {
            var isChildTextBlock = _window.FindControl<TextBlock>("IsChildTextBlock");
            if (isChildTextBlock != null)
            {
                if (node.IsChild)
                {
                    isChildTextBlock.Text = "⚠ This is a Child/Link (appears under multiple parents)";
                }
                else
                {
                    isChildTextBlock.Text = "";
                }
            }
        }

        /// <summary>
        /// Populates script fields with callbacks for parameter loading.
        /// </summary>
        public void PopulateScripts(DialogNode dialogNode, TreeViewSafeNode node,
            System.Action<string, bool> loadParameterDeclarations,
            System.Action<string, bool> loadScriptPreview,
            System.Action<bool> clearScriptPreview)
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
        /// Populates quest-related fields (tag, entry, preview).
        /// </summary>
        public void PopulateQuest(DialogNode dialogNode)
        {
            if (!string.IsNullOrEmpty(dialogNode.Quest))
            {
                var questTagComboBox = _window.FindControl<ComboBox>("QuestTagComboBox");
                if (questTagComboBox?.ItemsSource is List<JournalCategory> categories)
                {
                    var matchingCategory = categories.FirstOrDefault(c => c.Tag == dialogNode.Quest);
                    questTagComboBox.SelectedItem = matchingCategory;

                    // Populate quest name display
                    var questNameTextBlock = _window.FindControl<TextBlock>("QuestNameTextBlock");
                    if (questNameTextBlock != null && matchingCategory != null)
                    {
                        var questName = matchingCategory.Name?.GetDefault();
                        questNameTextBlock.Text = string.IsNullOrEmpty(questName)
                            ? ""
                            : $"Quest: {questName}";
                    }

                    // Populate quest entry dropdown
                    if (matchingCategory != null)
                    {
                        var questEntryComboBox = _window.FindControl<ComboBox>("QuestEntryComboBox");
                        if (questEntryComboBox != null)
                        {
                            questEntryComboBox.ItemsSource = matchingCategory.Entries;

                            if (dialogNode.QuestEntry != uint.MaxValue)
                            {
                                var matchingEntry = matchingCategory.Entries.FirstOrDefault(e => e.ID == dialogNode.QuestEntry);
                                questEntryComboBox.SelectedItem = matchingEntry;

                                if (matchingEntry != null)
                                {
                                    var questEntryPreviewTextBlock = _window.FindControl<TextBlock>("QuestEntryPreviewTextBlock");
                                    if (questEntryPreviewTextBlock != null)
                                    {
                                        questEntryPreviewTextBlock.Text = matchingEntry.TextPreview;
                                    }

                                    var questEntryEndTextBlock = _window.FindControl<TextBlock>("QuestEntryEndTextBlock");
                                    if (questEntryEndTextBlock != null)
                                    {
                                        questEntryEndTextBlock.Text = matchingEntry.End ? "✓ Quest Complete" : "";
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                ClearQuest();
            }
        }

        /// <summary>
        /// Clears quest selection fields.
        /// </summary>
        public void ClearQuest()
        {
            var questTagComboBox = _window.FindControl<ComboBox>("QuestTagComboBox");
            if (questTagComboBox != null)
                questTagComboBox.SelectedIndex = -1;

            var questNameTextBlock = _window.FindControl<TextBlock>("QuestNameTextBlock");
            if (questNameTextBlock != null)
                questNameTextBlock.Text = "";

            var questEntryComboBox = _window.FindControl<ComboBox>("QuestEntryComboBox");
            if (questEntryComboBox != null)
                questEntryComboBox.SelectedIndex = -1;

            var questEntryPreviewTextBlock = _window.FindControl<TextBlock>("QuestEntryPreviewTextBlock");
            if (questEntryPreviewTextBlock != null)
                questEntryPreviewTextBlock.Text = "";

            var questEntryEndTextBlock = _window.FindControl<TextBlock>("QuestEntryEndTextBlock");
            if (questEntryEndTextBlock != null)
                questEntryEndTextBlock.Text = "";
        }

        /// <summary>
        /// Populates script parameter grids.
        /// </summary>
        public void PopulateParameterGrids(DialogNode node, DialogPtr? ptr, System.Action<StackPanel, string, string, bool> addParameterRow)
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
        /// Clears all property panel fields and disables editable controls.
        /// </summary>
        public void ClearAllFields()
        {
            var nodeTypeTextBlock = _window.FindControl<TextBlock>("NodeTypeTextBlock");
            if (nodeTypeTextBlock != null) nodeTypeTextBlock.Text = "";

            var speakerTextBox = _window.FindControl<TextBox>("SpeakerTextBox");
            if (speakerTextBox != null)
            {
                speakerTextBox.Clear();
                speakerTextBox.IsReadOnly = true;
            }

            var textTextBox = _window.FindControl<TextBox>("TextTextBox");
            if (textTextBox != null)
            {
                textTextBox.Clear();
                textTextBox.IsReadOnly = true;
            }

            var soundTextBox = _window.FindControl<TextBox>("SoundTextBox");
            if (soundTextBox != null)
            {
                soundTextBox.Clear();
                soundTextBox.IsReadOnly = true;
            }

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

            var commentTextBox = _window.FindControl<TextBox>("CommentTextBox");
            if (commentTextBox != null)
            {
                commentTextBox.Clear();
                commentTextBox.IsReadOnly = true;
            }

            var delayTextBox = _window.FindControl<TextBox>("DelayTextBox");
            if (delayTextBox != null)
            {
                delayTextBox.Clear();
                delayTextBox.IsReadOnly = true;
            }

            var animationComboBox = _window.FindControl<ComboBox>("AnimationComboBox");
            if (animationComboBox != null)
            {
                animationComboBox.SelectedIndex = -1;
                animationComboBox.IsEnabled = false;
            }

            var animationLoopCheckBox = _window.FindControl<CheckBox>("AnimationLoopCheckBox");
            if (animationLoopCheckBox != null)
            {
                animationLoopCheckBox.IsChecked = false;
                animationLoopCheckBox.IsEnabled = false;
            }

            ClearQuest();

            var conditionsPanel = _window.FindControl<StackPanel>("ConditionsParametersPanel");
            conditionsPanel?.Children.Clear();

            var actionsPanel = _window.FindControl<StackPanel>("ActionsParametersPanel");
            actionsPanel?.Children.Clear();

            var isChildTextBlock = _window.FindControl<TextBlock>("IsChildTextBlock");
            if (isChildTextBlock != null)
                isChildTextBlock.Text = "";
        }
    }
}
