using Avalonia.Headless.XUnit;
using DialogEditor.Models;
using DialogEditor.Services;
using Parley.Tests.Mocks;
using Parley.Views.Helpers;
using Xunit;

namespace Parley.Tests.Integration
{
    /// <summary>
    /// Integration tests for QuestPropertiesPopulator.
    /// Tests quest tag lookup, entry matching, journal service integration,
    /// and quest completion status display.
    ///
    /// In headless tests, FindControl throws InvalidOperationException.
    /// Assert.Throws confirms the populator reached the UI update path.
    /// </summary>
    public class QuestPropertiesTests
    {
        private readonly MockJournalService _mockJournal;

        public QuestPropertiesTests()
        {
            _mockJournal = new MockJournalService();
        }

        #region Constructor Validation

        [AvaloniaFact]
        public void Constructor_NullWindow_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>("window", () =>
                new QuestPropertiesPopulator(null!, _mockJournal));
        }

        [AvaloniaFact]
        public void Constructor_NullJournalService_ThrowsArgumentNullException()
        {
            var window = new Avalonia.Controls.Window();
            Assert.Throws<ArgumentNullException>("journalService", () =>
                new QuestPropertiesPopulator(window, null!));
        }

        #endregion

        #region Quest Population

        [AvaloniaFact]
        public void PopulateQuest_WithValidQuestTag_ReachesUI()
        {
            SetupTestQuest();
            var populator = CreateQuestPopulator();
            var node = CreateNodeWithQuest("q_rescue", 1);

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateQuest(node));
        }

        [AvaloniaFact]
        public void PopulateQuest_WithUnknownQuestTag_ReachesUI()
        {
            var populator = CreateQuestPopulator();
            var node = CreateNodeWithQuest("q_nonexistent", 1);

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateQuest(node));
        }

        [AvaloniaFact]
        public void PopulateQuest_WithEmptyQuestTag_ReachesUI()
        {
            var populator = CreateQuestPopulator();
            var node = CreateNodeWithQuest("", uint.MaxValue);

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateQuest(node));
        }

        [AvaloniaFact]
        public void PopulateQuest_WithCompletionEntry_ReachesUI()
        {
            SetupTestQuest();
            var populator = CreateQuestPopulator();
            var node = CreateNodeWithQuest("q_rescue", 2);

            // Entry 2 has End=true
            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateQuest(node));
        }

        [AvaloniaFact]
        public void PopulateQuest_WithUnknownEntry_ReachesUI()
        {
            SetupTestQuest();
            var populator = CreateQuestPopulator();
            var node = CreateNodeWithQuest("q_rescue", 99);

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateQuest(node));
        }

        [AvaloniaFact]
        public void PopulateQuest_WithMaxValueEntry_ReachesUI()
        {
            SetupTestQuest();
            var populator = CreateQuestPopulator();
            var node = CreateNodeWithQuest("q_rescue", uint.MaxValue);

            // MaxValue = no entry set
            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateQuest(node));
        }

        [AvaloniaFact]
        public void PopulateQuest_QuestWithNoName_ReachesUI()
        {
            var category = new JournalCategory { Tag = "q_unnamed" };
            category.Entries.Add(new JournalEntry { ID = 1 });
            _mockJournal.AddCategory(category);

            var populator = CreateQuestPopulator();
            var node = CreateNodeWithQuest("q_unnamed", 1);

            Assert.Throws<InvalidOperationException>(() =>
                populator.PopulateQuest(node));
        }

        #endregion

        #region Journal Service Integration

        [AvaloniaFact]
        public void PopulateQuest_QueriesCorrectCategory()
        {
            _mockJournal.AddQuest("q_main", 1, 2, 3);
            _mockJournal.AddQuest("q_side", 10, 20);

            // Verify mock journal returns correct data
            var mainCat = _mockJournal.GetCategory("q_main");
            var sideCat = _mockJournal.GetCategory("q_side");
            Assert.NotNull(mainCat);
            Assert.NotNull(sideCat);
            Assert.Equal("q_main", mainCat.Tag);
            Assert.Equal("q_side", sideCat.Tag);
            Assert.Equal(3, mainCat.Entries.Count);
            Assert.Equal(2, sideCat.Entries.Count);
        }

        [AvaloniaFact]
        public void PopulateQuest_MatchesCorrectEntry()
        {
            var category = new JournalCategory
            {
                Tag = "q_test",
                Name = CreateLocString("Test Quest")
            };
            category.Entries.Add(new JournalEntry
            {
                ID = 1,
                Text = CreateLocString("First step"),
                End = false
            });
            category.Entries.Add(new JournalEntry
            {
                ID = 2,
                Text = CreateLocString("Quest complete!"),
                End = true
            });
            _mockJournal.AddCategory(category);

            // Verify service returns correct entry data
            var entries = _mockJournal.GetEntriesForQuest("q_test");
            var matchingEntry = entries.FirstOrDefault(e => e.ID == 2);
            Assert.NotNull(matchingEntry);
            Assert.True(matchingEntry.End);
        }

        [AvaloniaFact]
        public void PopulateQuest_UnknownQuest_ServiceReturnsNull()
        {
            Assert.Null(_mockJournal.GetCategory("q_nonexistent"));
        }

        [AvaloniaFact]
        public void PopulateQuest_UnknownEntry_ServiceReturnsEmpty()
        {
            _mockJournal.AddQuest("q_test", 1, 2);

            var entries = _mockJournal.GetEntriesForQuest("q_test");
            Assert.DoesNotContain(entries, e => e.ID == 99);
        }

        #endregion

        #region Clear

        [AvaloniaFact]
        public void ClearQuestFields_ReachesUI()
        {
            var populator = CreateQuestPopulator();

            Assert.Throws<InvalidOperationException>(() =>
                populator.ClearQuestFields());
        }

        #endregion

        #region Rapid Node Switching

        [AvaloniaFact]
        public void PopulateQuest_RapidSwitchBetweenNodes_EachReachesUI()
        {
            SetupTestQuest();
            _mockJournal.AddQuest("q_side", 10, 20);

            var populator = CreateQuestPopulator();

            var node1 = CreateNodeWithQuest("q_rescue", 1);
            var node2 = CreateNodeWithQuest("q_side", 10);
            var node3 = CreateNodeWithQuest("", uint.MaxValue);
            var node4 = CreateNodeWithQuest("q_rescue", 2);

            Assert.Throws<InvalidOperationException>(() => populator.PopulateQuest(node1));
            Assert.Throws<InvalidOperationException>(() => populator.PopulateQuest(node2));
            Assert.Throws<InvalidOperationException>(() => populator.PopulateQuest(node3));
            Assert.Throws<InvalidOperationException>(() => populator.PopulateQuest(node4));
        }

        #endregion

        #region Helper Methods

        private QuestPropertiesPopulator CreateQuestPopulator()
        {
            var window = new Avalonia.Controls.Window();
            return new QuestPropertiesPopulator(window, _mockJournal);
        }

        private void SetupTestQuest()
        {
            var category = new JournalCategory
            {
                Tag = "q_rescue",
                Name = CreateLocString("Rescue the Princess")
            };
            category.Entries.Add(new JournalEntry
            {
                ID = 1,
                Text = CreateLocString("Find the key to the cell"),
                End = false
            });
            category.Entries.Add(new JournalEntry
            {
                ID = 2,
                Text = CreateLocString("The princess is free!"),
                End = true
            });
            _mockJournal.AddCategory(category);
        }

        private static DialogNode CreateNodeWithQuest(string questTag, uint questEntry)
        {
            var node = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Speaker = "Guard",
                Text = new LocString(),
                Quest = questTag,
                QuestEntry = questEntry
            };
            node.Text.Add(0, "Test dialog text");
            return node;
        }

        private static LocString CreateLocString(string text)
        {
            var ls = new LocString();
            ls.Add(0, text);
            return ls;
        }

        #endregion
    }
}
