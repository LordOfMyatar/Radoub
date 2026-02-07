using System;
using Avalonia.Controls;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Services;
using Parley.Models;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Coordinator for the Properties Panel in MainWindow.
    /// Delegates all population logic to domain-specific sub-populators.
    ///
    /// Sub-populators:
    /// - BasicPropertiesPopulator: Node type, text, sound, comment, delay, animation, conversation settings (#1229)
    /// - SpeakerPropertiesPopulator: Speaker, portrait, soundset (#1226)
    /// - ScriptPropertiesPopulator: Scripts and parameters (#1227)
    /// - QuestPropertiesPopulator: Quest tag and entries (#1228)
    ///
    /// Epic #1219 - Phase 2: PropertyPanelPopulator Split
    /// </summary>
    public class PropertyPanelPopulator
    {
        private readonly BasicPropertiesPopulator _basicPopulator;
        private readonly SpeakerPropertiesPopulator _speakerPopulator;
        private readonly ScriptPropertiesPopulator _scriptPopulator;
        private readonly QuestPropertiesPopulator _questPopulator;

        public PropertyPanelPopulator(Window window)
        {
            _basicPopulator = new BasicPropertiesPopulator(window);
            _speakerPopulator = new SpeakerPropertiesPopulator(window);
            _scriptPopulator = new ScriptPropertiesPopulator(window);
            _questPopulator = new QuestPropertiesPopulator(window);
        }

        public BasicPropertiesPopulator BasicPopulator => _basicPopulator;
        public SpeakerPropertiesPopulator SpeakerPopulator => _speakerPopulator;
        public ScriptPropertiesPopulator ScriptPopulator => _scriptPopulator;
        public QuestPropertiesPopulator QuestPopulator => _questPopulator;

        public void SetImageService(IImageService imageService)
        {
            _speakerPopulator.SetImageService(imageService);
        }

        public void SetGameDataService(IGameDataService gameDataService)
        {
            _speakerPopulator.SetGameDataService(gameDataService);
        }

        // Conversation settings - delegates to BasicPopulator
        public void PopulateConversationSettings(Dialog? dialog, string? filePath = null)
        {
            _basicPopulator.PopulateConversationSettings(dialog, filePath);
        }

        // Node type - delegates to BasicPopulator
        public void PopulateNodeType(DialogNode dialogNode)
        {
            _basicPopulator.PopulateNodeType(dialogNode);
        }

        // Speaker - delegates to SpeakerPopulator
        public void PopulateSpeaker(DialogNode dialogNode, CreatureService? creatureService = null)
        {
            _speakerPopulator.PopulateSpeaker(dialogNode, creatureService);
        }

        public Action<ushort>? SetCurrentSoundsetId
        {
            get => _speakerPopulator.SetCurrentSoundsetId;
            set => _speakerPopulator.SetCurrentSoundsetId = value;
        }

        public void PopulateSpeakerVisualPreferences(DialogNode dialogNode)
        {
            _speakerPopulator.PopulateSpeakerVisualPreferences(dialogNode);
        }

        // Basic properties - delegates to BasicPopulator
        public void PopulateBasicProperties(DialogNode dialogNode, TreeViewSafeNode node)
        {
            _basicPopulator.PopulateBasicProperties(dialogNode, node);
        }

        // Animation - delegates to BasicPopulator
        public void PopulateAnimation(DialogNode dialogNode)
        {
            _basicPopulator.PopulateAnimation(dialogNode);
        }

        // IsChild indicator - delegates to BasicPopulator
        public void PopulateIsChildIndicator(TreeViewSafeNode node)
        {
            _basicPopulator.PopulateIsChildIndicator(node);
        }

        // Scripts - delegates to ScriptPopulator
        public void PopulateScripts(DialogNode dialogNode, TreeViewSafeNode node,
            Action<string, bool> loadParameterDeclarations,
            Action<string, bool> loadScriptPreview,
            Action<bool> clearScriptPreview)
        {
            _scriptPopulator.PopulateScripts(dialogNode, node, loadParameterDeclarations, loadScriptPreview, clearScriptPreview);
        }

        // Quest - delegates to QuestPopulator
        public void PopulateQuest(DialogNode dialogNode)
        {
            _questPopulator.PopulateQuest(dialogNode);
        }

        public void ClearQuest()
        {
            _questPopulator.ClearQuestFields();
        }

        // Parameter grids - delegates to ScriptPopulator
        public void PopulateParameterGrids(DialogNode node, DialogPtr? ptr, Action<StackPanel, string, string, bool> addParameterRow)
        {
            _scriptPopulator.PopulateParameterGrids(node, ptr, addParameterRow);
        }

        /// <summary>
        /// Clears all property panel fields by delegating to each sub-populator.
        /// </summary>
        public void ClearAllFields()
        {
            _basicPopulator.ClearBasicFields();
            _scriptPopulator.ClearScriptFields();
            _questPopulator.ClearQuestFields();
            _speakerPopulator.ClearSpeakerFields();
        }
    }
}
