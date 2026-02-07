using System.Collections.Generic;
using System.Threading.Tasks;
using DialogEditor.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Interface for parsing and caching NWN module.jrl (journal/quest) files.
    /// #1230: Phase 3 - Service interface extraction for dependency injection.
    /// </summary>
    public interface IJournalService
    {
        /// <summary>
        /// Parse module.jrl file and return all quest categories.
        /// </summary>
        Task<List<JournalCategory>> ParseJournalFileAsync(string filePath);

        /// <summary>
        /// Get all quest tags from cached journal.
        /// </summary>
        List<string> GetQuestTags();

        /// <summary>
        /// Get entries for a specific quest tag.
        /// </summary>
        List<JournalEntry> GetEntriesForQuest(string questTag);

        /// <summary>
        /// Get category by tag.
        /// </summary>
        JournalCategory? GetCategory(string questTag);

        /// <summary>
        /// Get all unique entry IDs across all quests (for parameter browser).
        /// </summary>
        List<string> GetAllEntryIDs();

        /// <summary>
        /// Clear cached journal data.
        /// </summary>
        void ClearCache();
    }
}
