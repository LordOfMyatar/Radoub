using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using DialogEditor.Utils;
using DialogEditor.Views;
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
        private IImageService? _imageService;

        public PropertyPanelPopulator(Window window)
        {
            _window = window;
        }

        /// <summary>
        /// Sets the image service for loading portraits from BIF archives (#916).
        /// </summary>
        public void SetImageService(IImageService imageService)
        {
            _imageService = imageService;
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
        public void PopulateSpeaker(DialogNode dialogNode, CreatureService? creatureService = null)
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

            // Populate NPC speaker visual preferences (Issue #16, #36)
            PopulateSpeakerVisualPreferences(dialogNode);

            // Populate soundset info (Issue #786)
            PopulateSoundsetInfo(dialogNode, isPC, creatureService);
        }

        /// <summary>
        /// Populates creature info from tag lookup (#786 soundset, #915 portrait).
        /// Shows portrait image and soundset info for NPC speakers with creatures.
        /// </summary>
        private void PopulateSoundsetInfo(DialogNode dialogNode, bool isPC, CreatureService? creatureService)
        {
            var soundsetInfoTextBlock = _window.FindControl<TextBlock>("SoundsetInfoTextBlock");
            var portraitBorder = _window.FindControl<Border>("PortraitBorder");
            var portraitImage = _window.FindControl<Image>("PortraitImage");

            // Clear portrait for PC nodes or empty speaker
            if (isPC || string.IsNullOrWhiteSpace(dialogNode.Speaker))
            {
                if (soundsetInfoTextBlock != null)
                    soundsetInfoTextBlock.Text = "";
                if (portraitBorder != null)
                    portraitBorder.IsVisible = false;
                return;
            }

            // Try to look up creature by speaker tag
            if (creatureService == null || !creatureService.HasCachedCreatures)
            {
                if (soundsetInfoTextBlock != null)
                    soundsetInfoTextBlock.Text = "";
                if (portraitBorder != null)
                    portraitBorder.IsVisible = false;
                return;
            }

            var creature = creatureService.GetCreatureByTag(dialogNode.Speaker);
            if (creature == null)
            {
                if (soundsetInfoTextBlock != null)
                    soundsetInfoTextBlock.Text = $"Creature '{dialogNode.Speaker}' not found in module";
                if (portraitBorder != null)
                    portraitBorder.IsVisible = false;
                return;
            }

            // Load and display portrait image (#915, #916)
            if (portraitBorder != null && portraitImage != null)
            {
                Bitmap? portrait = null;

                // Try to load portrait by ResRef using ImageService for BIF lookup
                if (!string.IsNullOrEmpty(creature.PortraitResRef) && _imageService != null)
                {
                    var imageData = _imageService.GetPortrait(creature.PortraitResRef);
                    if (imageData != null)
                    {
                        portrait = ImageDataToBitmap(imageData);
                    }
                }

                if (portrait != null)
                {
                    portraitImage.Source = portrait;
                    portraitBorder.IsVisible = true;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Loaded portrait for {creature.Tag}: {creature.PortraitResRef}");
                }
                else
                {
                    portraitBorder.IsVisible = false;
                    if (!string.IsNullOrEmpty(creature.PortraitResRef))
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Portrait not found for {creature.Tag}: {creature.PortraitResRef}");
                    }
                }
            }

            // Build info string with soundset (#786)
            var infoParts = new List<string>();

            // Show creature name
            if (!string.IsNullOrEmpty(creature.DisplayName))
            {
                infoParts.Add(creature.DisplayName);
            }

            // Soundset info (#786)
            if (!string.IsNullOrEmpty(creature.SoundSetSummary))
            {
                infoParts.Add($"Soundset: {creature.SoundSetSummary}");
            }
            else if (creature.SoundSetFile != ushort.MaxValue)
            {
                infoParts.Add($"Soundset ID: {creature.SoundSetFile}");
            }

            // Display combined info
            if (soundsetInfoTextBlock != null)
            {
                if (infoParts.Count > 0)
                {
                    soundsetInfoTextBlock.Text = string.Join(" | ", infoParts);
                }
                else
                {
                    soundsetInfoTextBlock.Text = $"Tag: {creature.Tag}";
                }
            }

            // Setup soundset preview controls (#916)
            var soundsetPreviewPanel = _window.FindControl<StackPanel>("SoundsetPreviewPanel");
            var soundsetTypeCombo = _window.FindControl<ComboBox>("SoundsetTypeComboBox");

            if (soundsetPreviewPanel != null && soundsetTypeCombo != null)
            {
                bool hasSoundset = creature.SoundSetFile != ushort.MaxValue;
                soundsetPreviewPanel.IsVisible = hasSoundset;

                if (hasSoundset)
                {
                    // Store soundset ID for play handler
                    SetCurrentSoundsetId?.Invoke(creature.SoundSetFile);

                    // Populate combo if empty
                    if (soundsetTypeCombo.ItemCount == 0)
                    {
                        soundsetTypeCombo.ItemsSource = GetSoundsetTypeItems();
                        soundsetTypeCombo.SelectedIndex = 0; // Hello
                    }
                }
            }
        }

        /// <summary>
        /// Callback to set the current soundset ID in MainWindow for play button (#916).
        /// </summary>
        public Action<ushort>? SetCurrentSoundsetId { get; set; }

        /// <summary>
        /// Gets the common sound type items for the dropdown (#916).
        /// </summary>
        private static List<SoundsetTypeItem> GetSoundsetTypeItems()
        {
            return new List<SoundsetTypeItem>
            {
                new() { Name = "Hello", SoundType = Radoub.Formats.Ssf.SsfSoundType.Hello },
                new() { Name = "Goodbye", SoundType = Radoub.Formats.Ssf.SsfSoundType.Goodbye },
                new() { Name = "Yes", SoundType = Radoub.Formats.Ssf.SsfSoundType.Yes },
                new() { Name = "No", SoundType = Radoub.Formats.Ssf.SsfSoundType.No },
                new() { Name = "Attack", SoundType = Radoub.Formats.Ssf.SsfSoundType.Attack },
                new() { Name = "Battlecry", SoundType = Radoub.Formats.Ssf.SsfSoundType.Battlecry1 },
                new() { Name = "Taunt", SoundType = Radoub.Formats.Ssf.SsfSoundType.Taunt },
                new() { Name = "Death", SoundType = Radoub.Formats.Ssf.SsfSoundType.Death },
                new() { Name = "Laugh", SoundType = Radoub.Formats.Ssf.SsfSoundType.Laugh },
                new() { Name = "Selected", SoundType = Radoub.Formats.Ssf.SsfSoundType.Selected },
            };
        }

        /// <summary>
        /// Populates shape/color ComboBoxes based on speaker tag preferences.
        /// Disabled for PC nodes and empty speakers (Owner).
        /// </summary>
        public void PopulateSpeakerVisualPreferences(DialogNode dialogNode)
        {
            var shapeComboBox = _window.FindControl<ComboBox>("SpeakerShapeComboBox");
            var colorComboBox = _window.FindControl<ComboBox>("SpeakerColorComboBox");

            bool isPC = (dialogNode.Type == DialogNodeType.Reply);
            bool hasNamedSpeaker = !string.IsNullOrWhiteSpace(dialogNode.Speaker);

            // Disable for PC nodes and Owner (empty speaker)
            bool enableControls = !isPC && hasNamedSpeaker;

            if (shapeComboBox != null)
            {
                shapeComboBox.IsEnabled = enableControls;
                if (enableControls)
                {
                    var (_, prefShape) = SettingsService.Instance.GetSpeakerPreference(dialogNode.Speaker);
                    if (prefShape.HasValue)
                    {
                        // Set to preference
                        shapeComboBox.SelectedItem = prefShape.Value;
                    }
                    else
                    {
                        // Show hash-based default
                        var defaultShape = SpeakerVisualHelper.GetSpeakerShape(dialogNode.Speaker, false);
                        shapeComboBox.SelectedItem = defaultShape;
                    }
                }
                else
                {
                    shapeComboBox.SelectedIndex = -1;
                }
            }

            if (colorComboBox != null)
            {
                colorComboBox.IsEnabled = enableControls;
                if (enableControls)
                {
                    var (prefColor, _) = SettingsService.Instance.GetSpeakerPreference(dialogNode.Speaker);
                    if (!string.IsNullOrEmpty(prefColor))
                    {
                        // Set to preference
                        foreach (var obj in colorComboBox.Items)
                        {
                            if (obj is ComboBoxItem item && item.Tag as string == prefColor)
                            {
                                colorComboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Show hash-based default
                        var defaultColor = SpeakerVisualHelper.GetSpeakerColor(dialogNode.Speaker, false);
                        foreach (var obj in colorComboBox.Items)
                        {
                            if (obj is ComboBoxItem item && item.Tag as string == defaultColor)
                            {
                                colorComboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    colorComboBox.SelectedIndex = -1;
                }
            }
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

            // Issue #786, #915: Clear soundset info and portrait
            var soundsetInfoTextBlock = _window.FindControl<TextBlock>("SoundsetInfoTextBlock");
            if (soundsetInfoTextBlock != null)
                soundsetInfoTextBlock.Text = "";

            var portraitBorder = _window.FindControl<Border>("PortraitBorder");
            if (portraitBorder != null)
                portraitBorder.IsVisible = false;

            // Issue #178: Clear script preview TextBoxes
            var conditionalPreview = _window.FindControl<TextBox>("ConditionalScriptPreviewTextBox");
            if (conditionalPreview != null)
                conditionalPreview.Text = "// Conditional script preview will appear here";

            var actionPreview = _window.FindControl<TextBox>("ActionScriptPreviewTextBox");
            if (actionPreview != null)
                actionPreview.Text = "// Action script preview will appear here";
        }

        /// <summary>
        /// Converts ImageData from Radoub.Formats to an Avalonia Bitmap (#916).
        /// </summary>
        private static Bitmap? ImageDataToBitmap(ImageData imageData)
        {
            if (imageData.Width == 0 || imageData.Height == 0 || imageData.Pixels == null)
                return null;

            try
            {
                using var stream = new MemoryStream();

                // Write BMP header + pixel data (32-bit BGRA)
                WriteBmpHeader(stream, imageData.Width, imageData.Height);
                WriteBmpPixels(stream, imageData.Width, imageData.Height, imageData.Pixels);

                stream.Position = 0;
                return new Bitmap(stream);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to convert image data to bitmap: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Writes BMP file header for 32-bit BGRA format.
        /// </summary>
        private static void WriteBmpHeader(Stream stream, int width, int height)
        {
            var writer = new BinaryWriter(stream);

            // BMP File Header (14 bytes)
            var pixelDataSize = width * height * 4;
            var fileSize = 14 + 108 + pixelDataSize; // BITMAPV4HEADER is 108 bytes

            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(fileSize);
            writer.Write((short)0); // Reserved
            writer.Write((short)0); // Reserved
            writer.Write(14 + 108); // Pixel data offset

            // BITMAPV4HEADER (108 bytes)
            writer.Write(108); // Header size
            writer.Write(width);
            writer.Write(-height); // Negative for top-down DIB
            writer.Write((short)1); // Planes
            writer.Write((short)32); // Bits per pixel
            writer.Write(3); // Compression: BI_BITFIELDS
            writer.Write(pixelDataSize);
            writer.Write(2835); // X pixels per meter (72 DPI)
            writer.Write(2835); // Y pixels per meter (72 DPI)
            writer.Write(0); // Colors used
            writer.Write(0); // Important colors

            // Color masks for BGRA
            writer.Write(0x00FF0000); // Red mask
            writer.Write(0x0000FF00); // Green mask
            writer.Write(0x000000FF); // Blue mask
            writer.Write(0xFF000000); // Alpha mask

            // Color space type: LCS_sRGB
            writer.Write(0x73524742);

            // CIEXYZTRIPLE endpoints (36 bytes)
            for (int i = 0; i < 9; i++)
                writer.Write(0);

            // Gamma values
            writer.Write(0); // Red gamma
            writer.Write(0); // Green gamma
            writer.Write(0); // Blue gamma
        }

        /// <summary>
        /// Writes pixel data in BGRA format.
        /// </summary>
        private static void WriteBmpPixels(Stream stream, int width, int height, byte[] rgbaPixels)
        {
            // Convert RGBA to BGRA and write
            for (int i = 0; i < rgbaPixels.Length; i += 4)
            {
                var r = rgbaPixels[i];
                var g = rgbaPixels[i + 1];
                var b = rgbaPixels[i + 2];
                var a = rgbaPixels[i + 3];

                stream.WriteByte(b); // Blue
                stream.WriteByte(g); // Green
                stream.WriteByte(r); // Red
                stream.WriteByte(a); // Alpha
            }
        }
    }
}
