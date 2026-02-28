using Avalonia.Headless.XUnit;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.ViewModels;
using Parley.Tests.Mocks;
using Parley.Views.Helpers;
using Xunit;

namespace Parley.Tests.Controllers
{
    /// <summary>
    /// Unit tests for QuestUIController.
    /// Tests quest tag/entry logic, clear operations, and journal loading.
    /// UI-dependent methods (browse dialogs) require headless tests.
    /// </summary>
    public class QuestUIControllerTests
    {
        private readonly MockSettingsService _mockSettings;
        private readonly MockJournalService _mockJournal;
        private readonly MainViewModel _viewModel;
        private TreeViewSafeNode? _selectedNode;
        private bool _isPopulatingProperties;
        private bool _autoSaveTriggered;

        public QuestUIControllerTests()
        {
            _mockSettings = new MockSettingsService();
            _mockJournal = new MockJournalService();
            _viewModel = new MainViewModel();
        }

        #region Quest Tag Text Changed Guards

        [AvaloniaFact]
        public void OnQuestTagTextChanged_NoSelection_DoesNothing()
        {
            _selectedNode = null;
            var controller = CreateController();

            // Should not throw when no node selected
            controller.OnQuestTagTextChanged(null, null!);
        }

        [AvaloniaFact]
        public void OnQuestTagTextChanged_PopulatingProperties_DoesNothing()
        {
            _selectedNode = CreateTestNode();
            _isPopulatingProperties = true;
            var controller = CreateController();

            // Should return early
            controller.OnQuestTagTextChanged(null, null!);
        }

        #endregion

        #region Quest Entry Text Changed Guards

        [AvaloniaFact]
        public void OnQuestEntryTextChanged_NoSelection_DoesNothing()
        {
            _selectedNode = null;
            var controller = CreateController();

            controller.OnQuestEntryTextChanged(null, null!);
        }

        [AvaloniaFact]
        public void OnQuestEntryTextChanged_PopulatingProperties_DoesNothing()
        {
            _selectedNode = CreateTestNode();
            _isPopulatingProperties = true;
            var controller = CreateController();

            controller.OnQuestEntryTextChanged(null, null!);
        }

        #endregion

        #region Quest Tag Lost Focus Guards

        [AvaloniaFact]
        public void OnQuestTagLostFocus_NoSelection_DoesNothing()
        {
            _selectedNode = null;
            _autoSaveTriggered = false;
            var controller = CreateController();

            controller.OnQuestTagLostFocus(null, null!);

            Assert.False(_autoSaveTriggered);
        }

        [AvaloniaFact]
        public void OnQuestTagLostFocus_PopulatingProperties_DoesNothing()
        {
            _selectedNode = CreateTestNode();
            _isPopulatingProperties = true;
            _autoSaveTriggered = false;
            var controller = CreateController();

            controller.OnQuestTagLostFocus(null, null!);

            Assert.False(_autoSaveTriggered);
        }

        [AvaloniaFact]
        public void OnQuestTagLostFocus_WithSelection_TriggersAutoSave()
        {
            _selectedNode = CreateTestNode();
            _isPopulatingProperties = false;
            _autoSaveTriggered = false;
            var controller = CreateController();

            controller.OnQuestTagLostFocus(null, null!);

            Assert.True(_autoSaveTriggered);
            Assert.True(_viewModel.HasUnsavedChanges);
        }

        #endregion

        #region Quest Entry Lost Focus Guards

        [AvaloniaFact]
        public void OnQuestEntryLostFocus_NoSelection_DoesNothing()
        {
            _selectedNode = null;
            _autoSaveTriggered = false;
            var controller = CreateController();

            controller.OnQuestEntryLostFocus(null, null!);

            Assert.False(_autoSaveTriggered);
        }

        [AvaloniaFact]
        public void OnQuestEntryLostFocus_WithSelection_TriggersAutoSave()
        {
            _selectedNode = CreateTestNode();
            _isPopulatingProperties = false;
            _autoSaveTriggered = false;
            var controller = CreateController();

            controller.OnQuestEntryLostFocus(null, null!);

            Assert.True(_autoSaveTriggered);
            Assert.True(_viewModel.HasUnsavedChanges);
        }

        #endregion

        #region Clear Quest Tag

        [AvaloniaFact]
        public void OnClearQuestTagClick_NoSelection_DoesNothing()
        {
            _selectedNode = null;
            _autoSaveTriggered = false;
            var controller = CreateController();

            controller.OnClearQuestTagClick(null, null!);

            Assert.False(_autoSaveTriggered);
        }

        [AvaloniaFact]
        public void OnClearQuestTagClick_WithSelection_ClearsModel()
        {
            // OnClearQuestTagClick clears model data then calls FindControl for UI updates.
            // In headless tests without NameScope, FindControl throws.
            // We verify model was cleared by checking the exception comes from FindControl
            // (meaning the model logic executed before the UI update).
            var node = CreateTestNode();
            node.OriginalNode.Quest = "some_quest";
            node.OriginalNode.QuestEntry = 5;
            _selectedNode = node;
            _isPopulatingProperties = false;
            var controller = CreateController();

            // Model is updated before FindControl is called
            Assert.Throws<InvalidOperationException>(() =>
                controller.OnClearQuestTagClick(null, null!));

            // Verify model was cleared before the exception
            Assert.Equal(string.Empty, node.OriginalNode.Quest);
            Assert.Equal(uint.MaxValue, node.OriginalNode.QuestEntry);
        }

        #endregion

        #region Clear Quest Entry

        [AvaloniaFact]
        public void OnClearQuestEntryClick_NoSelection_DoesNothing()
        {
            _selectedNode = null;
            _autoSaveTriggered = false;
            var controller = CreateController();

            controller.OnClearQuestEntryClick(null, null!);

            Assert.False(_autoSaveTriggered);
        }

        [AvaloniaFact]
        public void OnClearQuestEntryClick_WithSelection_ClearsModel()
        {
            var node = CreateTestNode();
            node.OriginalNode.Quest = "keep_this";
            node.OriginalNode.QuestEntry = 10;
            _selectedNode = node;
            _isPopulatingProperties = false;
            var controller = CreateController();

            // Model is updated before FindControl is called for UI
            Assert.Throws<InvalidOperationException>(() =>
                controller.OnClearQuestEntryClick(null, null!));

            Assert.Equal("keep_this", node.OriginalNode.Quest); // Quest tag preserved
            Assert.Equal(uint.MaxValue, node.OriginalNode.QuestEntry); // Entry cleared
        }

        #endregion

        #region Browse Handlers Guards

        [AvaloniaFact]
        public void OnBrowseQuestClick_NoSelection_DoesNothing()
        {
            _selectedNode = null;
            var controller = CreateController();

            // Should return early without throwing
            controller.OnBrowseQuestClick(null, null!);
        }

        [AvaloniaFact]
        public void OnBrowseQuestEntryClick_NoSelection_DoesNothing()
        {
            _selectedNode = null;
            var controller = CreateController();

            // Should return early without throwing
            controller.OnBrowseQuestEntryClick(null, null!);
        }

        #endregion

        #region Journal Loading

        [AvaloniaFact]
        public async Task LoadJournalForCurrentModuleAsync_NoFile_DoesNotThrow()
        {
            // No current file and no module path - should handle gracefully
            _viewModel.CurrentFileName = null;
            _mockSettings.CurrentModulePath = "";
            var controller = CreateController();

            await controller.LoadJournalForCurrentModuleAsync();

            // Should complete without throwing
        }

        [AvaloniaFact]
        public async Task LoadJournalForCurrentModuleAsync_NonexistentPath_DoesNotThrow()
        {
            _viewModel.CurrentFileName = null;
            _mockSettings.CurrentModulePath = Path.Combine(Path.GetTempPath(), "nonexistent_module_path_12345");
            var controller = CreateController();

            await controller.LoadJournalForCurrentModuleAsync();

            // Should handle missing directory gracefully
        }

        #endregion

        #region Helper

        private QuestUIController CreateController(
            Avalonia.Controls.Window? window = null,
            ISettingsService? settings = null,
            IJournalService? journalService = null,
            Func<MainViewModel>? getViewModel = null,
            Func<TreeViewSafeNode?>? getSelectedNode = null,
            Func<bool>? isPopulatingProperties = null,
            Action<bool>? setIsPopulatingProperties = null,
            Action? triggerAutoSave = null)
        {
            var w = window ?? new Avalonia.Controls.Window();
            var controls = new SafeControlFinder(w);

            return new QuestUIController(
                w,
                controls,
                settings ?? _mockSettings,
                journalService ?? _mockJournal,
                getViewModel ?? (() => _viewModel),
                getSelectedNode ?? (() => _selectedNode),
                isPopulatingProperties ?? (() => _isPopulatingProperties),
                setIsPopulatingProperties ?? (v => _isPopulatingProperties = v),
                triggerAutoSave ?? (() => _autoSaveTriggered = true));
        }

        private static TreeViewSafeNode CreateTestNode(string text = "Test node")
        {
            var dialogNode = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Quest = "",
                QuestEntry = uint.MaxValue
            };
            dialogNode.Text.Add(0, text);
            return new TreeViewSafeNode(dialogNode);
        }

        #endregion
    }
}
