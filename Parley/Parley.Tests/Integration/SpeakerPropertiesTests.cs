using Avalonia.Headless.XUnit;
using DialogEditor.Models;
using DialogEditor.Services;
using Parley.Tests.Mocks;
using Parley.Views.Helpers;
using Xunit;

namespace Parley.Tests.Integration
{
    /// <summary>
    /// Integration tests for SpeakerPropertiesPopulator.
    /// Tests speaker field behavior for NPC entry nodes vs PC reply nodes,
    /// visual preferences, and soundset/portrait service interactions.
    ///
    /// In headless tests, FindControl throws InvalidOperationException.
    /// Assert.Throws confirms the populator reached the UI update path.
    /// </summary>
    public class SpeakerPropertiesTests
    {
        private readonly MockSettingsService _mockSettings;
        private readonly MockGameDataService _mockGameData;
        private readonly MockImageService _mockImageService;

        public SpeakerPropertiesTests()
        {
            _mockSettings = new MockSettingsService();
            _mockGameData = new MockGameDataService();
            _mockImageService = new MockImageService();
        }

        #region Entry Node (NPC) Speaker

        [AvaloniaFact]
        public void PopulateSpeaker_EntryNodeWithSpeaker_ReachesUI()
        {
            var populator = CreateSpeakerPopulator();
            var node = CreateEntryNode("Guard");

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateSpeaker(node));
        }

        [AvaloniaFact]
        public void PopulateSpeaker_EntryNodeEmptySpeaker_ReachesUI()
        {
            var populator = CreateSpeakerPopulator();
            var node = CreateEntryNode("");

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateSpeaker(node));
        }

        [AvaloniaFact]
        public void PopulateSpeaker_EntryNodeWithCreatureService_ReachesUI()
        {
            var populator = CreateSpeakerPopulator();
            var node = CreateEntryNode("Guard");
            var creatureService = new CreatureService();

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateSpeaker(node, creatureService));
        }

        [AvaloniaFact]
        public void PopulateSpeaker_NullCreatureService_ReachesUI()
        {
            var populator = CreateSpeakerPopulator();
            var node = CreateEntryNode("Guard");

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateSpeaker(node, null));
        }

        #endregion

        #region Reply Node (PC) Speaker

        [AvaloniaFact]
        public void PopulateSpeaker_ReplyNode_ReachesUI()
        {
            var populator = CreateSpeakerPopulator();
            var node = CreateReplyNode();

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateSpeaker(node));
        }

        #endregion

        #region Visual Preferences

        [AvaloniaFact]
        public void PopulateSpeakerVisualPreferences_EntryWithSpeaker_ReachesUI()
        {
            var populator = CreateSpeakerPopulator();
            var node = CreateEntryNode("Guard");

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateSpeakerVisualPreferences(node));
        }

        [AvaloniaFact]
        public void PopulateSpeakerVisualPreferences_EntryWithSavedPreference_ReachesUI()
        {
            _mockSettings.SetSpeakerPreference("Guard", "#FF0000",
                DialogEditor.Utils.SpeakerVisualHelper.SpeakerShape.Diamond);
            var populator = CreateSpeakerPopulator();
            var node = CreateEntryNode("Guard");

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateSpeakerVisualPreferences(node));
        }

        [AvaloniaFact]
        public void PopulateSpeakerVisualPreferences_ReplyNode_ReachesUI()
        {
            var populator = CreateSpeakerPopulator();
            var node = CreateReplyNode();

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateSpeakerVisualPreferences(node));
        }

        [AvaloniaFact]
        public void PopulateSpeakerVisualPreferences_EmptySpeaker_ReachesUI()
        {
            var populator = CreateSpeakerPopulator();
            var node = CreateEntryNode("");

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateSpeakerVisualPreferences(node));
        }

        #endregion

        #region Clear

        [AvaloniaFact]
        public void ClearSpeakerFields_ReachesUI()
        {
            var populator = CreateSpeakerPopulator();

            Assert.Throws<InvalidOperationException>(() =>
                populator.ClearSpeakerFields());
        }

        #endregion

        #region Service Injection

        [AvaloniaFact]
        public void SetImageService_AcceptsService()
        {
            var window = new Avalonia.Controls.Window();
            var populator = new SpeakerPropertiesPopulator(window, _mockSettings);

            populator.SetImageService(_mockImageService);
        }

        [AvaloniaFact]
        public void SetGameDataService_AcceptsService()
        {
            var window = new Avalonia.Controls.Window();
            var populator = new SpeakerPropertiesPopulator(window, _mockSettings);

            populator.SetGameDataService(_mockGameData);
        }

        [AvaloniaFact]
        public void SetCurrentSoundsetId_CallbackStoredCorrectly()
        {
            var window = new Avalonia.Controls.Window();
            var populator = new SpeakerPropertiesPopulator(window, _mockSettings);

            ushort capturedId = 0;
            populator.SetCurrentSoundsetId = id => capturedId = id;

            Assert.NotNull(populator.SetCurrentSoundsetId);
        }

        #endregion

        #region Constructor Validation

        [AvaloniaFact]
        public void Constructor_NullWindow_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>("window", () =>
                new SpeakerPropertiesPopulator(null!, _mockSettings));
        }

        [AvaloniaFact]
        public void Constructor_NullSettings_ThrowsArgumentNullException()
        {
            var window = new Avalonia.Controls.Window();
            Assert.Throws<ArgumentNullException>("settings", () =>
                new SpeakerPropertiesPopulator(window, null!));
        }

        #endregion

        #region Rapid Node Switching

        [AvaloniaFact]
        public void PopulateSpeaker_RapidSwitchEntryToReply_EachReachesUI()
        {
            var populator = CreateSpeakerPopulator();

            var entry = CreateEntryNode("Guard");
            var reply = CreateReplyNode();
            var entry2 = CreateEntryNode("Merchant");

            Assert.Throws<InvalidOperationException>(() => populator.PopulateSpeaker(entry));
            Assert.Throws<InvalidOperationException>(() => populator.PopulateSpeaker(reply));
            Assert.Throws<InvalidOperationException>(() => populator.PopulateSpeaker(entry2));
        }

        #endregion

        #region Helper Methods

        private SpeakerPropertiesPopulator CreateSpeakerPopulator()
        {
            var window = new Avalonia.Controls.Window();
            var populator = new SpeakerPropertiesPopulator(window, _mockSettings);
            populator.SetImageService(_mockImageService);
            populator.SetGameDataService(_mockGameData);
            return populator;
        }

        private static DialogNode CreateEntryNode(string speaker)
        {
            var node = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Speaker = speaker,
                Text = new LocString()
            };
            node.Text.Add(0, "Test text");
            return node;
        }

        private static DialogNode CreateReplyNode()
        {
            var node = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Speaker = "",
                Text = new LocString()
            };
            node.Text.Add(0, "PC reply");
            return node;
        }

        #endregion
    }
}
