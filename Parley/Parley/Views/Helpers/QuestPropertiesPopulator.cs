using System.Linq;
using Avalonia.Controls;
using DialogEditor.Models;
using DialogEditor.Services;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Populates quest-related properties in the Properties Panel.
    /// Extracted from PropertyPanelPopulator to reduce class size (Epic #1219, Sprint 2.3 #1228).
    ///
    /// Handles:
    /// 1. Quest tag field population
    /// 2. Quest name lookup from JournalService
    /// 3. Quest entry ID and preview population
    /// 4. Quest completion status display
    /// </summary>
    public class QuestPropertiesPopulator
    {
        private readonly Window _window;

        public QuestPropertiesPopulator(Window window)
        {
            _window = window ?? throw new System.ArgumentNullException(nameof(window));
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
        public void ClearQuestFields()
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
    }
}
