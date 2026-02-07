using DialogEditor.Models;
using DialogEditor.Services;

namespace Parley.Tests.Mocks
{
    /// <summary>
    /// Mock IJournalService for unit testing.
    /// Allows setup of quest/journal data without parsing real .jrl files.
    /// </summary>
    public class MockJournalService : IJournalService
    {
        private readonly List<JournalCategory> _categories = new();

        public Task<List<JournalCategory>> ParseJournalFileAsync(string filePath)
        {
            return Task.FromResult(new List<JournalCategory>(_categories));
        }

        public List<string> GetQuestTags()
        {
            return _categories.Select(c => c.Tag).ToList();
        }

        public List<JournalEntry> GetEntriesForQuest(string questTag)
        {
            var category = _categories.FirstOrDefault(c => c.Tag == questTag);
            return category?.Entries ?? new List<JournalEntry>();
        }

        public JournalCategory? GetCategory(string questTag)
        {
            return _categories.FirstOrDefault(c => c.Tag == questTag);
        }

        public List<string> GetAllEntryIDs()
        {
            return _categories
                .SelectMany(c => c.Entries)
                .Select(e => e.ID.ToString())
                .Distinct()
                .ToList();
        }

        public void ClearCache()
        {
            _categories.Clear();
        }

        /// <summary>
        /// Add a quest category for testing.
        /// </summary>
        public void AddCategory(JournalCategory category)
        {
            _categories.Add(category);
        }

        /// <summary>
        /// Add a simple quest with entries for testing.
        /// </summary>
        public void AddQuest(string tag, params uint[] entryIds)
        {
            var category = new JournalCategory { Tag = tag };
            foreach (var id in entryIds)
            {
                category.Entries.Add(new JournalEntry { ID = id });
            }
            _categories.Add(category);
        }
    }
}
