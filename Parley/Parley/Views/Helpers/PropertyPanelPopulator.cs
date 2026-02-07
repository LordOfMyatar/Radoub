using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using DialogEditor.Utils;
using Parley.Models;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Helper class for populating the Properties Panel in MainWindow.
    /// Extracted from MainWindow to reduce method size and improve maintainability.
    ///
    /// Handles:
    /// 1. Conversation-level settings (PreventZoom, ScriptEnd, ScriptAbort)
    /// 2. Node-specific properties (Text, Animation, Scripts, etc.)
    /// 3. Quest integration (Quest tag, entry selection)
    /// 4. Script parameters
    /// 5. Clearing/disabling controls when no selection
    ///
    /// Speaker/portrait/soundset population delegated to SpeakerPropertiesPopulator (Epic #1219, Sprint 2.1 #1226).
    /// </summary>
    public class PropertyPanelPopulator
    {
        private readonly Window _window;
        private readonly SpeakerPropertiesPopulator _speakerPopulator;

        public PropertyPanelPopulator(Window window)
        {
            _window = window;
            _speakerPopulator = new SpeakerPropertiesPopulator(window);
        }

        /// <summary>
        /// Gets the speaker properties populator for direct access from MainWindow (#1226).
        /// </summary>
        public SpeakerPropertiesPopulator SpeakerPopulator => _speakerPopulator;

        /// <summary>
        /// Sets the image service for loading portraits from BIF archives (#916).
        /// </summary>
        public void SetImageService(IImageService imageService)
        {
            _speakerPopulator.SetImageService(imageService);
        }

        /// <summary>
        /// Sets the game data service for 2DA lookups from BIF archives (#916).
        /// </summary>
        public void SetGameDataService(IGameDataService gameDataService)
        {
            _speakerPopulator.SetGameDataService(gameDataService);
        }

        /// <summary>
        /// Populates conversation-level settings (always visible regardless of node selection).
        /// </summary>
        public void PopulateConversationSettings(Dialog? dialog, string? filePath = null)
        {
            if (dialog == null) return;

            // Update dialog name from file path (#675)
            var dialogNameTextBox = _window.FindControl<TextBox>("DialogNameTextBox");
            if (dialogNameTextBox != null)
            {
                dialogNameTextBox.Text = string.IsNullOrEmpty(filePath)
                    ? ""
                    : Path.GetFileNameWithoutExtension(filePath);
            }

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
        /// Delegates to SpeakerPropertiesPopulator (#1226).
        /// </summary>
        public void PopulateSpeaker(DialogNode dialogNode, CreatureService? creatureService = null)
        {
            _speakerPopulator.PopulateSpeaker(dialogNode, creatureService);
        }

        /// <summary>
        /// Callback to set the current soundset ID in MainWindow for play button (#916).
        /// Delegates to SpeakerPropertiesPopulator (#1226).
        /// </summary>
        public Action<ushort>? SetCurrentSoundsetId
        {
            get => _speakerPopulator.SetCurrentSoundsetId;
            set => _speakerPopulator.SetCurrentSoundsetId = value;
        }

        /// <summary>
        /// Populates shape/color ComboBoxes based on speaker tag preferences.
        /// Delegates to SpeakerPropertiesPopulator (#1226).
        /// </summary>
        public void PopulateSpeakerVisualPreferences(DialogNode dialogNode)
        {
            _speakerPopulator.PopulateSpeakerVisualPreferences(dialogNode);
        }

        /// <summary>
        /// Populates basic text properties (Text, Sound, Comment, Delay).
        /// Issue #12: For link nodes, shows LinkComment instead of original node's Comment.
        /// </summary>
        public void PopulateBasicProperties(DialogNode dialogNode, TreeViewSafeNode node)
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
                // Issue #12: For link nodes (IsChild=true), show LinkComment from pointer
                // instead of the original node's Comment
                bool isChildCheck = node.IsChild;
                bool hasSourcePointer = node.SourcePointer != null;

                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"📝 Comment field: IsChild={isChildCheck}, HasSourcePointer={hasSourcePointer}, " +
                    $"NodeType={node.GetType().Name}, DisplayText='{node.DisplayText}'");

                if (isChildCheck && hasSourcePointer)
                {
                    string linkComment = node.SourcePointer!.LinkComment ?? "";
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"📝 Using LINK comment: '{linkComment}' (from SourcePointer.LinkComment)");
                    commentTextBox.Text = linkComment;
                }
                else
                {
                    string nodeComment = dialogNode.Comment ?? "";
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"📝 Using NODE comment: '{nodeComment}' (from DialogNode.Comment)");
                    commentTextBox.Text = nodeComment;
                }
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
        /// Issue #166: Updated for TextBox-based quest selection.
        /// </summary>
        public void PopulateQuest(DialogNode dialogNode)
        {
            // Populate quest tag TextBox
            var questTagTextBox = _window.FindControl<TextBox>("QuestTagTextBox");
            if (questTagTextBox != null)
            {
                questTagTextBox.Text = dialogNode.Quest ?? "";
            }

            // Populate quest name display by looking up in journal
            var questNameTextBlock = _window.FindControl<TextBlock>("QuestNameTextBlock");
            if (questNameTextBlock != null)
            {
                if (!string.IsNullOrEmpty(dialogNode.Quest))
                {
                    var category = JournalService.Instance.GetCategory(dialogNode.Quest);
                    if (category != null)
                    {
                        var questName = category.Name?.GetDefault();
                        questNameTextBlock.Text = string.IsNullOrEmpty(questName)
                            ? ""
                            : $"Quest: {questName}";
                    }
                    else
                    {
                        questNameTextBlock.Text = "(quest not found in journal)";
                    }
                }
                else
                {
                    questNameTextBlock.Text = "";
                }
            }

            // Populate quest entry TextBox
            var questEntryTextBox = _window.FindControl<TextBox>("QuestEntryTextBox");
            if (questEntryTextBox != null)
            {
                questEntryTextBox.Text = dialogNode.QuestEntry != uint.MaxValue
                    ? dialogNode.QuestEntry.ToString()
                    : "";
            }

            // Populate entry preview and end status
            var questEntryPreviewTextBlock = _window.FindControl<TextBlock>("QuestEntryPreviewTextBlock");
            var questEntryEndTextBlock = _window.FindControl<TextBlock>("QuestEntryEndTextBlock");

            if (dialogNode.QuestEntry != uint.MaxValue && !string.IsNullOrEmpty(dialogNode.Quest))
            {
                var entries = JournalService.Instance.GetEntriesForQuest(dialogNode.Quest);
                var matchingEntry = entries.FirstOrDefault(e => e.ID == dialogNode.QuestEntry);

                if (matchingEntry != null)
                {
                    if (questEntryPreviewTextBlock != null)
                        questEntryPreviewTextBlock.Text = matchingEntry.TextPreview;
                    if (questEntryEndTextBlock != null)
                        questEntryEndTextBlock.Text = matchingEntry.End ? "✓ Quest Complete" : "";
                }
                else
                {
                    if (questEntryPreviewTextBlock != null)
                        questEntryPreviewTextBlock.Text = "(entry not found)";
                    if (questEntryEndTextBlock != null)
                        questEntryEndTextBlock.Text = "";
                }
            }
            else
            {
                if (questEntryPreviewTextBlock != null)
                    questEntryPreviewTextBlock.Text = "";
                if (questEntryEndTextBlock != null)
                    questEntryEndTextBlock.Text = "";
            }
        }

        /// <summary>
        /// Clears quest selection fields.
        /// Issue #166: Updated for TextBox-based quest selection.
        /// </summary>
        public void ClearQuest()
        {
            var questTagTextBox = _window.FindControl<TextBox>("QuestTagTextBox");
            if (questTagTextBox != null)
                questTagTextBox.Text = "";

            var questNameTextBlock = _window.FindControl<TextBlock>("QuestNameTextBlock");
            if (questNameTextBlock != null)
                questNameTextBlock.Text = "";

            var questEntryTextBox = _window.FindControl<TextBox>("QuestEntryTextBox");
            if (questEntryTextBox != null)
                questEntryTextBox.Text = "";

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

            // Issue #786, #915: Clear soundset info and portrait (#1226)
            _speakerPopulator.ClearSpeakerFields();

            // Issue #178: Clear script preview TextBoxes
            var conditionalPreview = _window.FindControl<TextBox>("ConditionalScriptPreviewTextBox");
            if (conditionalPreview != null)
                conditionalPreview.Text = "// Conditional script preview will appear here";

            var actionPreview = _window.FindControl<TextBox>("ActionScriptPreviewTextBox");
            if (actionPreview != null)
                actionPreview.Text = "// Action script preview will appear here";
        }

    }
}
