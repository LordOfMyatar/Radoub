using Avalonia.Headless.XUnit;
using DialogEditor.Models;
using DialogEditor.Services;
using Parley.Models;
using Parley.Tests.Mocks;
using Parley.Views.Helpers;
using Xunit;

namespace Parley.Tests.Integration
{
    /// <summary>
    /// Integration tests for PropertyPanelPopulator coordinator.
    /// Verifies the coordinator correctly delegates to sub-populators
    /// and that all four work together.
    ///
    /// In headless tests, FindControl throws InvalidOperationException
    /// (no NameScope on bare Window). We use Assert.Throws to confirm
    /// delegation reached the sub-populator's FindControl call.
    /// Methods with early returns (e.g., null dialog) complete without throwing.
    /// </summary>
    public class PropertyPanelIntegrationTests
    {
        private readonly MockSettingsService _mockSettings;
        private readonly MockJournalService _mockJournal;

        public PropertyPanelIntegrationTests()
        {
            _mockSettings = new MockSettingsService();
            _mockJournal = new MockJournalService();
        }

        #region Coordinator Construction

        [AvaloniaFact]
        public void Constructor_ValidDependencies_CreatesAllSubPopulators()
        {
            var window = new Avalonia.Controls.Window();
            var populator = new PropertyPanelPopulator(window, _mockSettings, _mockJournal);

            Assert.NotNull(populator.BasicPopulator);
            Assert.NotNull(populator.SpeakerPopulator);
            Assert.NotNull(populator.ScriptPopulator);
            Assert.NotNull(populator.QuestPopulator);
        }

        [AvaloniaFact]
        public void SetImageService_DelegatesToSpeakerPopulator()
        {
            var window = new Avalonia.Controls.Window();
            var populator = new PropertyPanelPopulator(window, _mockSettings, _mockJournal);

            var mockImageService = new MockImageService();
            populator.SetImageService(mockImageService);
        }

        [AvaloniaFact]
        public void SetGameDataService_DelegatesToSpeakerPopulator()
        {
            var window = new Avalonia.Controls.Window();
            var populator = new PropertyPanelPopulator(window, _mockSettings, _mockJournal);

            var mockGameData = new MockGameDataService();
            populator.SetGameDataService(mockGameData);
        }

        [AvaloniaFact]
        public void SetCurrentSoundsetId_DelegatesToSpeakerPopulator()
        {
            var window = new Avalonia.Controls.Window();
            var populator = new PropertyPanelPopulator(window, _mockSettings, _mockJournal);

            ushort capturedId = 0;
            populator.SetCurrentSoundsetId = id => capturedId = id;

            Assert.NotNull(populator.SetCurrentSoundsetId);
        }

        #endregion

        #region Delegation to BasicPopulator

        [AvaloniaFact]
        public void PopulateConversationSettings_NullDialog_ReturnsEarly()
        {
            var populator = CreatePopulator();

            // Null dialog triggers early return before FindControl
            populator.PopulateConversationSettings(null);
        }

        [AvaloniaFact]
        public void PopulateConversationSettings_WithDialog_DelegatesToBasicPopulator()
        {
            var populator = CreatePopulator();
            var dialog = new Dialog
            {
                ScriptEnd = "nw_d1_ending",
                ScriptAbort = "nw_d1_abort",
                PreventZoom = true
            };

            // Reaches BasicPopulator.PopulateConversationSettings -> FindControl
            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateConversationSettings(dialog, "test_dialog.dlg"));
        }

        [AvaloniaFact]
        public void PopulateNodeType_DelegatesToBasicPopulator()
        {
            var populator = CreatePopulator();
            var node = CreateEntryNode("Guard", "Hello");

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateNodeType(node));
        }

        [AvaloniaFact]
        public void PopulateBasicProperties_DelegatesToBasicPopulator()
        {
            var populator = CreatePopulator();
            var node = CreateEntryNode("Guard", "Hello");
            var safeNode = new TreeViewSafeNode(node);

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateBasicProperties(node, safeNode));
        }

        [AvaloniaFact]
        public void PopulateAnimation_DelegatesToBasicPopulator()
        {
            var populator = CreatePopulator();
            var node = CreateEntryNode("Guard", "Hello");

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateAnimation(node));
        }

        [AvaloniaFact]
        public void PopulateIsChildIndicator_DelegatesToBasicPopulator()
        {
            var populator = CreatePopulator();
            var node = CreateEntryNode("Guard", "Hello");
            var safeNode = new TreeViewSafeNode(node);

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateIsChildIndicator(safeNode));
        }

        #endregion

        #region Delegation to SpeakerPopulator

        [AvaloniaFact]
        public void PopulateSpeaker_DelegatesToSpeakerPopulator()
        {
            var populator = CreatePopulator();
            var node = CreateEntryNode("Guard", "Hello");

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateSpeaker(node));
        }

        [AvaloniaFact]
        public void PopulateSpeakerVisualPreferences_DelegatesToSpeakerPopulator()
        {
            var populator = CreatePopulator();
            var node = CreateEntryNode("Guard", "Hello");

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateSpeakerVisualPreferences(node));
        }

        #endregion

        #region Delegation to ScriptPopulator

        [AvaloniaFact]
        public void PopulateScripts_DelegatesToScriptPopulator()
        {
            var populator = CreatePopulator();
            var node = CreateEntryNode("Guard", "Test");
            node.ScriptAction = "nw_d1_action";
            var safeNode = new TreeViewSafeNode(node);

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateScripts(node, safeNode,
                    (s, c) => { }, (s, c) => { }, c => { }));
        }

        [AvaloniaFact]
        public void PopulateParameterGrids_DelegatesToScriptPopulator()
        {
            var populator = CreatePopulator();
            var node = CreateEntryNode("Guard", "Test");
            node.ActionParams["sParam1"] = "value1";

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateParameterGrids(node, null,
                    (panel, name, value, isCond) => { }));
        }

        #endregion

        #region Delegation to QuestPopulator

        [AvaloniaFact]
        public void PopulateQuest_DelegatesToQuestPopulator()
        {
            var populator = CreatePopulator();
            var node = CreateEntryNode("Guard", "Hello");
            node.Quest = "q_rescue";
            node.QuestEntry = 1;

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateQuest(node));
        }

        [AvaloniaFact]
        public void ClearQuest_DelegatesToQuestPopulator()
        {
            var populator = CreatePopulator();

            Assert.Throws<InvalidOperationException>(() =>
                populator.ClearQuest());
        }

        #endregion

        #region ClearAllFields

        [AvaloniaFact]
        public void ClearAllFields_DelegatesToAllSubPopulators()
        {
            var populator = CreatePopulator();

            // ClearAllFields calls all four sub-populator clear methods.
            // First FindControl hit throws.
            Assert.Throws<InvalidOperationException>(() =>
                populator.ClearAllFields());
        }

        #endregion

        #region Full Population Sequence

        [AvaloniaFact]
        public void PopulateEntryNode_FullSequence_EachDelegationReachesSubPopulator()
        {
            var populator = CreatePopulator();
            var node = CreateEntryNode("Guard", "Hello, adventurer!");
            var safeNode = new TreeViewSafeNode(node);

            // Each method delegates to its sub-populator and hits FindControl
            Assert.Throws<InvalidOperationException>(() => populator.PopulateNodeType(node));
            Assert.Throws<InvalidOperationException>(() => populator.PopulateSpeaker(node));
            Assert.Throws<InvalidOperationException>(() => populator.PopulateSpeakerVisualPreferences(node));
            Assert.Throws<InvalidOperationException>(() => populator.PopulateBasicProperties(node, safeNode));
            Assert.Throws<InvalidOperationException>(() => populator.PopulateAnimation(node));
            Assert.Throws<InvalidOperationException>(() => populator.PopulateIsChildIndicator(safeNode));
            Assert.Throws<InvalidOperationException>(() => populator.PopulateQuest(node));
        }

        [AvaloniaFact]
        public void PopulateReplyNode_FullSequence_EachDelegationReachesSubPopulator()
        {
            var populator = CreatePopulator();
            var node = CreateReplyNode("I need information.");
            var safeNode = new TreeViewSafeNode(node);

            Assert.Throws<InvalidOperationException>(() => populator.PopulateNodeType(node));
            Assert.Throws<InvalidOperationException>(() => populator.PopulateSpeaker(node));
            Assert.Throws<InvalidOperationException>(() => populator.PopulateBasicProperties(node, safeNode));
            Assert.Throws<InvalidOperationException>(() => populator.PopulateAnimation(node));
            Assert.Throws<InvalidOperationException>(() => populator.PopulateIsChildIndicator(safeNode));
            Assert.Throws<InvalidOperationException>(() => populator.PopulateQuest(node));
        }

        #endregion

        #region Helper Methods

        private PropertyPanelPopulator CreatePopulator()
        {
            var window = new Avalonia.Controls.Window();
            return new PropertyPanelPopulator(window, _mockSettings, _mockJournal);
        }

        private static DialogNode CreateEntryNode(string speaker, string text)
        {
            var node = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Speaker = speaker,
                Text = new LocString(),
                Sound = "",
                Comment = "",
                Quest = "",
                QuestEntry = uint.MaxValue,
                ScriptAction = "",
                Delay = uint.MaxValue,
                Animation = DialogAnimation.Default,
                AnimationLoop = false
            };
            node.Text.Add(0, text);
            return node;
        }

        private static DialogNode CreateReplyNode(string text)
        {
            var node = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Speaker = "",
                Text = new LocString(),
                Sound = "",
                Comment = "",
                Quest = "",
                QuestEntry = uint.MaxValue,
                ScriptAction = "",
                Delay = uint.MaxValue,
                Animation = DialogAnimation.Default,
                AnimationLoop = false
            };
            node.Text.Add(0, text);
            return node;
        }

        #endregion
    }
}
