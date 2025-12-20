using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.Utils;
using DialogEditor.ViewModels;
using DialogEditor.Views;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Manages all quest/journal UI interactions for MainWindow.
    /// Extracted from MainWindow to reduce method size and improve maintainability (Epic #457, Sprint 4).
    ///
    /// Handles:
    /// 1. Quest tag/entry text change events
    /// 2. Quest browser dialog handlers
    /// 3. Quest clear button handlers
    /// 4. Journal loading/integration
    /// 5. Quest display update methods
    /// </summary>
    public class QuestUIController
    {
        private readonly Window _window;
        private readonly SafeControlFinder _controls;
        private readonly Func<MainViewModel> _getViewModel;
        private readonly Func<TreeViewSafeNode?> _getSelectedNode;
        private readonly Func<bool> _isPopulatingProperties;
        private readonly Action<bool> _setIsPopulatingProperties;
        private readonly Action _triggerAutoSave;

        public QuestUIController(
            Window window,
            SafeControlFinder controls,
            Func<MainViewModel> getViewModel,
            Func<TreeViewSafeNode?> getSelectedNode,
            Func<bool> isPopulatingProperties,
            Action<bool> setIsPopulatingProperties,
            Action triggerAutoSave)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _controls = controls ?? throw new ArgumentNullException(nameof(controls));
            _getViewModel = getViewModel ?? throw new ArgumentNullException(nameof(getViewModel));
            _getSelectedNode = getSelectedNode ?? throw new ArgumentNullException(nameof(getSelectedNode));
            _isPopulatingProperties = isPopulatingProperties ?? throw new ArgumentNullException(nameof(isPopulatingProperties));
            _setIsPopulatingProperties = setIsPopulatingProperties ?? throw new ArgumentNullException(nameof(setIsPopulatingProperties));
            _triggerAutoSave = triggerAutoSave ?? throw new ArgumentNullException(nameof(triggerAutoSave));
        }

        private MainViewModel ViewModel => _getViewModel();
        private TreeViewSafeNode? SelectedNode => _getSelectedNode();

        #region Quest Tag/Entry Text Handlers

        /// <summary>
        /// Handle quest tag text changed - update model and validate against journal.
        /// Issue #166: TextBox-based quest selection.
        /// </summary>
        public void OnQuestTagTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (SelectedNode == null || _isPopulatingProperties()) return;

            var textBox = sender as TextBox;
            var questTag = textBox?.Text?.Trim() ?? "";

            var dialogNode = SelectedNode.OriginalNode;
            dialogNode.Quest = questTag;

            // Update quest name display by looking up in journal
            UpdateQuestNameDisplay(questTag);
        }

        /// <summary>
        /// Handle quest tag lost focus - trigger autosave.
        /// </summary>
        public void OnQuestTagLostFocus(object? sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || _isPopulatingProperties()) return;

            var textBox = sender as TextBox;
            var questTag = textBox?.Text?.Trim() ?? "";

            // Trigger autosave
            ViewModel.HasUnsavedChanges = true;
            _triggerAutoSave();

            if (!string.IsNullOrEmpty(questTag))
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Quest tag set to: {questTag}");
                ViewModel.StatusMessage = $"Quest tag: {questTag}";
            }
        }

        /// <summary>
        /// Handle quest entry text changed - update model.
        /// Issue #166: TextBox-based quest selection.
        /// </summary>
        public void OnQuestEntryTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (SelectedNode == null || _isPopulatingProperties()) return;

            var textBox = sender as TextBox;
            var entryText = textBox?.Text?.Trim() ?? "";

            var dialogNode = SelectedNode.OriginalNode;

            if (string.IsNullOrEmpty(entryText))
            {
                dialogNode.QuestEntry = uint.MaxValue;
                ClearQuestEntryDisplay();
            }
            else if (uint.TryParse(entryText, out uint entryId))
            {
                dialogNode.QuestEntry = entryId;
                UpdateQuestEntryDisplay(dialogNode.Quest, entryId);
            }
            // If not a valid number, don't update model (keep previous value)
        }

        /// <summary>
        /// Handle quest entry lost focus - trigger autosave.
        /// </summary>
        public void OnQuestEntryLostFocus(object? sender, RoutedEventArgs e)
        {
            if (SelectedNode == null || _isPopulatingProperties()) return;

            // Trigger autosave
            ViewModel.HasUnsavedChanges = true;
            _triggerAutoSave();

            var dialogNode = SelectedNode.OriginalNode;
            if (dialogNode.QuestEntry != uint.MaxValue)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Quest entry set to: {dialogNode.QuestEntry}");
                ViewModel.StatusMessage = $"Quest entry: {dialogNode.QuestEntry}";
            }
        }

        #endregion

        #region Quest Display Updates

        /// <summary>
        /// Update the quest name display by looking up the tag in the journal.
        /// </summary>
        public void UpdateQuestNameDisplay(string questTag)
        {
            var questNameTextBlock = _window.FindControl<TextBlock>("QuestNameTextBlock");
            if (questNameTextBlock == null) return;

            if (string.IsNullOrEmpty(questTag))
            {
                questNameTextBlock.Text = "";
                return;
            }

            // Look up quest in journal
            var category = JournalService.Instance.GetCategory(questTag);
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

        /// <summary>
        /// Update the quest entry preview display by looking up in journal.
        /// </summary>
        public void UpdateQuestEntryDisplay(string? questTag, uint entryId)
        {
            var questEntryPreviewTextBlock = _window.FindControl<TextBlock>("QuestEntryPreviewTextBlock");
            var questEntryEndTextBlock = _window.FindControl<TextBlock>("QuestEntryEndTextBlock");

            if (string.IsNullOrEmpty(questTag))
            {
                if (questEntryPreviewTextBlock != null)
                    questEntryPreviewTextBlock.Text = "";
                if (questEntryEndTextBlock != null)
                    questEntryEndTextBlock.Text = "";
                return;
            }

            // Look up entry in journal
            var entries = JournalService.Instance.GetEntriesForQuest(questTag);
            var entry = entries.FirstOrDefault(e => e.ID == entryId);

            if (entry != null)
            {
                if (questEntryPreviewTextBlock != null)
                    questEntryPreviewTextBlock.Text = entry.TextPreview;
                if (questEntryEndTextBlock != null)
                    questEntryEndTextBlock.Text = entry.End ? "✓ Quest Complete" : "";
            }
            else
            {
                if (questEntryPreviewTextBlock != null)
                    questEntryPreviewTextBlock.Text = "(entry not found)";
                if (questEntryEndTextBlock != null)
                    questEntryEndTextBlock.Text = "";
            }
        }

        /// <summary>
        /// Clear the quest entry preview display.
        /// </summary>
        public void ClearQuestEntryDisplay()
        {
            var questEntryPreviewTextBlock = _window.FindControl<TextBlock>("QuestEntryPreviewTextBlock");
            if (questEntryPreviewTextBlock != null)
                questEntryPreviewTextBlock.Text = "";

            var questEntryEndTextBlock = _window.FindControl<TextBlock>("QuestEntryEndTextBlock");
            if (questEntryEndTextBlock != null)
                questEntryEndTextBlock.Text = "";
        }

        #endregion

        #region Quest Browser Handlers

        /// <summary>
        /// Open QuestBrowserWindow to select a quest.
        /// Issue #166: Browse button for quest selection.
        /// </summary>
        public async void OnBrowseQuestClick(object? sender, RoutedEventArgs e)
        {
            if (SelectedNode == null) return;

            try
            {
                var dialogNode = SelectedNode.OriginalNode;
                var browser = new QuestBrowserWindow(
                    ViewModel.CurrentFilePath,
                    dialogNode.Quest,
                    dialogNode.QuestEntry != uint.MaxValue ? dialogNode.QuestEntry : null);

                var result = await browser.ShowDialog<QuestBrowserResult?>(_window);

                if (result != null && !string.IsNullOrEmpty(result.QuestTag))
                {
                    // Update TextBoxes with selected values
                    var questTagTextBox = _window.FindControl<TextBox>("QuestTagTextBox");
                    if (questTagTextBox != null)
                    {
                        _setIsPopulatingProperties(true);
                        questTagTextBox.Text = result.QuestTag;
                        _setIsPopulatingProperties(false);
                    }

                    dialogNode.Quest = result.QuestTag;
                    UpdateQuestNameDisplay(result.QuestTag);

                    if (result.EntryId.HasValue)
                    {
                        var questEntryTextBox = _window.FindControl<TextBox>("QuestEntryTextBox");
                        if (questEntryTextBox != null)
                        {
                            _setIsPopulatingProperties(true);
                            questEntryTextBox.Text = result.EntryId.Value.ToString();
                            _setIsPopulatingProperties(false);
                        }

                        dialogNode.QuestEntry = result.EntryId.Value;
                        UpdateQuestEntryDisplay(result.QuestTag, result.EntryId.Value);
                    }

                    // Trigger autosave
                    ViewModel.HasUnsavedChanges = true;
                    _triggerAutoSave();

                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Quest selected from browser: {result.QuestTag}");
                    ViewModel.StatusMessage = result.EntryId.HasValue
                        ? $"Quest: {result.QuestTag}, Entry: {result.EntryId.Value}"
                        : $"Quest: {result.QuestTag}";
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening quest browser: {ex.Message}");
                ViewModel.StatusMessage = $"Error opening quest browser: {ex.Message}";
            }
        }

        /// <summary>
        /// Open QuestBrowserWindow with current quest pre-selected to pick an entry.
        /// Issue #166: Browse button for quest entry selection.
        /// </summary>
        public async void OnBrowseQuestEntryClick(object? sender, RoutedEventArgs e)
        {
            if (SelectedNode == null) return;

            try
            {
                var dialogNode = SelectedNode.OriginalNode;

                // Pre-select current quest tag (if any)
                var browser = new QuestBrowserWindow(
                    ViewModel.CurrentFilePath,
                    dialogNode.Quest,
                    dialogNode.QuestEntry != uint.MaxValue ? dialogNode.QuestEntry : null);

                var result = await browser.ShowDialog<QuestBrowserResult?>(_window);

                if (result != null)
                {
                    // If user selected a different quest, update both fields
                    if (!string.IsNullOrEmpty(result.QuestTag) && result.QuestTag != dialogNode.Quest)
                    {
                        var questTagTextBox = _window.FindControl<TextBox>("QuestTagTextBox");
                        if (questTagTextBox != null)
                        {
                            _setIsPopulatingProperties(true);
                            questTagTextBox.Text = result.QuestTag;
                            _setIsPopulatingProperties(false);
                        }

                        dialogNode.Quest = result.QuestTag;
                        UpdateQuestNameDisplay(result.QuestTag);
                    }

                    // Update entry if selected
                    if (result.EntryId.HasValue)
                    {
                        var questEntryTextBox = _window.FindControl<TextBox>("QuestEntryTextBox");
                        if (questEntryTextBox != null)
                        {
                            _setIsPopulatingProperties(true);
                            questEntryTextBox.Text = result.EntryId.Value.ToString();
                            _setIsPopulatingProperties(false);
                        }

                        dialogNode.QuestEntry = result.EntryId.Value;
                        UpdateQuestEntryDisplay(result.QuestTag ?? dialogNode.Quest, result.EntryId.Value);

                        // Trigger autosave
                        ViewModel.HasUnsavedChanges = true;
                        _triggerAutoSave();

                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Quest entry selected from browser: {result.EntryId.Value}");
                        ViewModel.StatusMessage = $"Quest entry: {result.EntryId.Value}";
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening quest browser: {ex.Message}");
                ViewModel.StatusMessage = $"Error opening quest browser: {ex.Message}";
            }
        }

        #endregion

        #region Quest Clear Handlers

        /// <summary>
        /// Clear the quest tag field.
        /// Issue #166: Clear button for quest tag.
        /// </summary>
        public void OnClearQuestTagClick(object? sender, RoutedEventArgs e)
        {
            if (SelectedNode == null) return;

            var dialogNode = SelectedNode.OriginalNode;
            dialogNode.Quest = string.Empty;
            dialogNode.QuestEntry = uint.MaxValue;

            var questTagTextBox = _window.FindControl<TextBox>("QuestTagTextBox");
            if (questTagTextBox != null)
            {
                _setIsPopulatingProperties(true);
                questTagTextBox.Text = "";
                _setIsPopulatingProperties(false);
            }

            var questEntryTextBox = _window.FindControl<TextBox>("QuestEntryTextBox");
            if (questEntryTextBox != null)
            {
                _setIsPopulatingProperties(true);
                questEntryTextBox.Text = "";
                _setIsPopulatingProperties(false);
            }

            UpdateQuestNameDisplay("");
            ClearQuestEntryDisplay();

            // Trigger autosave
            ViewModel.HasUnsavedChanges = true;
            _triggerAutoSave();

            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Quest tag cleared");
            ViewModel.StatusMessage = "Quest tag cleared";
        }

        /// <summary>
        /// Clear the quest entry field.
        /// Issue #166: Clear button for quest entry.
        /// </summary>
        public void OnClearQuestEntryClick(object? sender, RoutedEventArgs e)
        {
            if (SelectedNode == null) return;

            var dialogNode = SelectedNode.OriginalNode;
            dialogNode.QuestEntry = uint.MaxValue;

            var questEntryTextBox = _window.FindControl<TextBox>("QuestEntryTextBox");
            if (questEntryTextBox != null)
            {
                _setIsPopulatingProperties(true);
                questEntryTextBox.Text = "";
                _setIsPopulatingProperties(false);
            }

            ClearQuestEntryDisplay();

            // Trigger autosave
            ViewModel.HasUnsavedChanges = true;
            _triggerAutoSave();

            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Quest entry cleared");
            ViewModel.StatusMessage = "Quest entry cleared";
        }

        #endregion

        #region Journal Loading

        /// <summary>
        /// Load journal file for the current module and cache it for quest lookups.
        /// Issue #166: No longer populates ComboBox - journal is looked up on-demand.
        /// </summary>
        public async Task LoadJournalForCurrentModuleAsync()
        {
            try
            {
                // Try to get module directory from currently loaded file
                string? modulePath = null;

                if (!string.IsNullOrEmpty(ViewModel.CurrentFileName))
                {
                    // Use directory of current .dlg file
                    modulePath = Path.GetDirectoryName(ViewModel.CurrentFileName);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Using module path from current file: {UnifiedLogger.SanitizePath(modulePath ?? "")}");
                }

                // Fallback to settings if no file loaded
                if (string.IsNullOrEmpty(modulePath))
                {
                    modulePath = SettingsService.Instance.CurrentModulePath;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Using module path from settings: {UnifiedLogger.SanitizePath(modulePath)}");
                }

                if (string.IsNullOrEmpty(modulePath) || !Directory.Exists(modulePath))
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Module path not set or doesn't exist - journal not loaded");
                    return;
                }

                var journalPath = Path.Combine(modulePath, "module.jrl");
                if (!File.Exists(journalPath))
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"No module.jrl found at {UnifiedLogger.SanitizePath(journalPath)}");
                    return;
                }

                // Parse and cache journal file for on-demand lookups
                var categories = await JournalService.Instance.ParseJournalFileAsync(journalPath);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded {categories.Count} quest categories from journal");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error loading journal: {ex.Message}");
            }
        }

        #endregion
    }
}
