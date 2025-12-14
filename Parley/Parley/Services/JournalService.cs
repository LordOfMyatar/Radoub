using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DialogEditor.Models;
using DialogEditor.Utils;

// Alias Radoub.Formats.Jrl types to avoid conflict with DialogEditor.Models types
using JrlFile = Radoub.Formats.Jrl.JrlFile;
using JrlReader = Radoub.Formats.Jrl.JrlReader;
using JrlLocString = Radoub.Formats.Jrl.JrlLocString;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service for parsing and caching NWN module.jrl (journal/quest) files.
    /// 2025-12-14: Updated to use Radoub.Formats.Jrl shared library (Sprint #398)
    /// </summary>
    public class JournalService
    {
        private static readonly Lazy<JournalService> _instance = new Lazy<JournalService>(() => new JournalService());
        public static JournalService Instance => _instance.Value;

        private List<JournalCategory>? _cachedCategories;
        private string? _cachedFilePath;

        /// <summary>
        /// Parse module.jrl file and return all quest categories
        /// </summary>
        public async Task<List<JournalCategory>> ParseJournalFileAsync(string filePath)
        {
            // Return cached if same file
            if (_cachedCategories != null && _cachedFilePath == filePath)
            {
                UnifiedLogger.LogJournal(LogLevel.DEBUG, $"Returning cached journal data for {UnifiedLogger.SanitizePath(filePath)}");
                return _cachedCategories;
            }

            if (!File.Exists(filePath))
            {
                UnifiedLogger.LogJournal(LogLevel.WARN, $"Journal file not found: {UnifiedLogger.SanitizePath(filePath)}");
                return new List<JournalCategory>();
            }

            try
            {
                UnifiedLogger.LogJournal(LogLevel.INFO, $"Parsing journal file: {UnifiedLogger.SanitizePath(filePath)}");

                // Use shared JrlReader from Radoub.Formats
                var fileBytes = await File.ReadAllBytesAsync(filePath);
                var jrlFile = JrlReader.Read(fileBytes);

                UnifiedLogger.LogJournal(LogLevel.DEBUG, $"JRL file parsed: {jrlFile.Categories.Count} categories");

                // Convert to DialogEditor types
                var categories = ConvertCategories(jrlFile);

                _cachedCategories = categories;
                _cachedFilePath = filePath;

                // Write cache file for script parameter browser
                WriteCacheFile(categories);

                UnifiedLogger.LogJournal(LogLevel.INFO, $"Parsed {categories.Count} quest categories from journal");
                return categories;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogJournal(LogLevel.ERROR, $"Error parsing journal file: {ex.Message}");
                return new List<JournalCategory>();
            }
        }

        /// <summary>
        /// Convert Radoub.Formats.Jrl categories to DialogEditor types
        /// </summary>
        private List<JournalCategory> ConvertCategories(JrlFile jrlFile)
        {
            var categories = new List<JournalCategory>();

            foreach (var jrlCategory in jrlFile.Categories)
            {
                var category = new JournalCategory
                {
                    Tag = jrlCategory.Tag,
                    Priority = jrlCategory.Priority,
                    XP = jrlCategory.XP,
                    Comment = jrlCategory.Comment,
                    Name = ConvertLocString(jrlCategory.Name)
                };

                // Convert entries
                foreach (var jrlEntry in jrlCategory.Entries)
                {
                    var entry = new JournalEntry
                    {
                        ID = jrlEntry.ID,
                        End = jrlEntry.End,
                        Text = ConvertLocString(jrlEntry.Text)
                    };
                    category.Entries.Add(entry);
                }

                UnifiedLogger.LogJournal(LogLevel.INFO, $"Quest '{category.Tag}' has {category.Entries.Count} entries");
                categories.Add(category);
            }

            return categories;
        }

        /// <summary>
        /// Convert JrlLocString to DialogEditor LocString.
        /// 2025-12-14: Now preserves StrRef for TLK internationalization (Issue #403)
        /// </summary>
        private LocString? ConvertLocString(JrlLocString jrlLocString)
        {
            if (jrlLocString.IsEmpty)
                return null;

            var locString = new LocString
            {
                StrRef = jrlLocString.StrRef
            };

            foreach (var kvp in jrlLocString.Strings)
            {
                locString.Strings[(int)kvp.Key] = kvp.Value;
            }

            // Set default text if English (0) exists
            if (locString.Strings.TryGetValue(0, out var defaultText))
            {
                locString.DefaultText = defaultText;
            }

            return locString;
        }

        /// <summary>
        /// Get all quest tags from cached journal
        /// </summary>
        public List<string> GetQuestTags()
        {
            if (_cachedCategories == null)
                return new List<string>();

            return _cachedCategories.Select(c => c.Tag).ToList();
        }

        /// <summary>
        /// Get entries for a specific quest tag
        /// </summary>
        public List<JournalEntry> GetEntriesForQuest(string questTag)
        {
            if (_cachedCategories == null || string.IsNullOrEmpty(questTag))
                return new List<JournalEntry>();

            var category = _cachedCategories.FirstOrDefault(c => c.Tag == questTag);
            return category?.Entries ?? new List<JournalEntry>();
        }

        /// <summary>
        /// Get category by tag
        /// </summary>
        public JournalCategory? GetCategory(string questTag)
        {
            if (_cachedCategories == null || string.IsNullOrEmpty(questTag))
                return null;

            return _cachedCategories.FirstOrDefault(c => c.Tag == questTag);
        }

        /// <summary>
        /// Get all unique entry IDs across all quests (for parameter browser)
        /// Returns formatted strings like "0", "1", "2", etc.
        /// </summary>
        public List<string> GetAllEntryIDs()
        {
            if (_cachedCategories == null)
                return new List<string>();

            var allIDs = new HashSet<uint>();
            foreach (var category in _cachedCategories)
            {
                foreach (var entry in category.Entries)
                {
                    allIDs.Add(entry.ID);
                }
            }

            return allIDs.OrderBy(id => id).Select(id => id.ToString()).ToList();
        }

        /// <summary>
        /// Clear cached journal data
        /// </summary>
        public void ClearCache()
        {
            _cachedCategories = null;
            _cachedFilePath = null;
            UnifiedLogger.LogJournal(LogLevel.DEBUG, "Journal cache cleared");
        }

        /// <summary>
        /// Write journal cache file for script parameter browser.
        /// Format: { "quest_tag": ["0", "1", "2", ...], ... }
        /// </summary>
        private void WriteCacheFile(List<JournalCategory> categories)
        {
            try
            {
                var cacheFilePath = GetCacheFilePath();
                var cacheDir = Path.GetDirectoryName(cacheFilePath);

                if (!string.IsNullOrEmpty(cacheDir) && !Directory.Exists(cacheDir))
                {
                    Directory.CreateDirectory(cacheDir);
                }

                // Build cache dictionary: quest_tag -> entry_ids
                var cache = new Dictionary<string, List<string>>();
                foreach (var category in categories)
                {
                    var entryIds = category.Entries
                        .Select(e => e.ID.ToString())
                        .ToList();
                    cache[category.Tag] = entryIds;
                }

                // Write JSON
                var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(cacheFilePath, json);

                UnifiedLogger.LogJournal(LogLevel.DEBUG,
                    $"Wrote journal cache file: {UnifiedLogger.SanitizePath(cacheFilePath)} ({cache.Count} quests)");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogJournal(LogLevel.WARN,
                    $"Failed to write journal cache file: {ex.Message}");
            }
        }

        /// <summary>
        /// Get path to journal cache file (in user's temp/app data directory)
        /// </summary>
        public static string GetCacheFilePath()
        {
            var userDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Parley",
                "Cache");
            return Path.Combine(userDataDir, "journal_cache.json");
        }

    }
}
