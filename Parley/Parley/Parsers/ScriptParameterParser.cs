using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.Parsers
{
    /// <summary>
    /// Parses NWScript (.nss) files to extract parameter declarations from comment blocks.
    /// Supports standardized ----KeyList---- and ----ValueList---- formats.
    /// Also supports dynamic value sources: FROM_JOURNAL_TAGS, FROM_JOURNAL_ENTRIES(key)
    /// </summary>
    public class ScriptParameterParser
    {
        // Regex patterns to extract KeyList and ValueList sections from comments
        private static readonly Regex KeyListRegex =
            new(@"----KeyList----\s*(.*?)\s*(?:----|/\*|\*/)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex ValueListRegex =
            new(@"----ValueList----\s*(.*?)\s*(?:----|/\*|\*/)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex KeyedValueListRegex =
            new(@"----ValueList-(\w+)----\s*(.*?)(?=----|\*/|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // Regex patterns for dynamic value sources
        private static readonly Regex JournalTagsRegex =
            new(@"FROM_JOURNAL_TAGS", RegexOptions.IgnoreCase);

        private static readonly Regex JournalEntriesRegex =
            new(@"FROM_JOURNAL_ENTRIES\((\w+)\)", RegexOptions.IgnoreCase);

        /// <summary>
        /// Parses NWScript content to extract parameter declarations.
        /// </summary>
        /// <param name="nssContent">The content of the .nss file</param>
        /// <returns>Script parameter declarations with keys and values</returns>
        public ScriptParameterDeclarations Parse(string nssContent)
        {
            if (string.IsNullOrWhiteSpace(nssContent))
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "ScriptParameterParser: Empty or null content provided");
                return ScriptParameterDeclarations.Empty;
            }

            try
            {
                var declarations = new ScriptParameterDeclarations();

                // Extract KeyList section
                var keyMatch = KeyListRegex.Match(nssContent);
                if (keyMatch.Success)
                {
                    declarations.Keys = ParseList(keyMatch.Groups[1].Value);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"ScriptParameterParser: Found {declarations.Keys.Count} keys in KeyList");
                }

                // Extract legacy ValueList section (without key suffix)
                var valueMatch = ValueListRegex.Match(nssContent);
                if (valueMatch.Success)
                {
                    declarations.Values = ParseList(valueMatch.Groups[1].Value);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"ScriptParameterParser: Found {declarations.Values.Count} values in ValueList");
                }

                // Extract keyed ValueList sections (e.g., ----ValueList-BASE_ITEM----)
                var keyedMatches = KeyedValueListRegex.Matches(nssContent);
                foreach (Match match in keyedMatches)
                {
                    string key = match.Groups[1].Value; // Parameter key name
                    string content = match.Groups[2].Value; // Values content

                    // Check for dynamic value sources and track dependencies
                    var values = ParseValueListContent(key, content, declarations);

                    if (values.Count > 0)
                    {
                        declarations.ValuesByKey[key] = values;
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"ScriptParameterParser: Found {values.Count} values for key '{key}'");
                    }
                }

                if (!declarations.HasDeclarations)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        "ScriptParameterParser: No parameter declarations found in script");
                }

                return declarations;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"ScriptParameterParser: Error parsing script content - {ex.Message}");
                return ScriptParameterDeclarations.Empty;
            }
        }

        /// <summary>
        /// Parses ValueList content, handling both static values and dynamic sources.
        /// Supports FROM_JOURNAL_TAGS and FROM_JOURNAL_ENTRIES(key) syntax.
        /// </summary>
        /// <param name="key">The parameter key name</param>
        /// <param name="content">The raw content from ValueList section</param>
        /// <param name="declarations">The declarations object to store dependency metadata</param>
        /// <returns>List of parameter values</returns>
        private List<string> ParseValueListContent(string key, string content, ScriptParameterDeclarations declarations)
        {
            if (string.IsNullOrWhiteSpace(content))
                return new List<string>();

            // Check for FROM_JOURNAL_TAGS
            if (JournalTagsRegex.IsMatch(content))
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"ScriptParameterParser: Dynamic source FROM_JOURNAL_TAGS for key '{key}'");
                return GetJournalTags();
            }

            // Check for FROM_JOURNAL_ENTRIES(paramKey)
            var entriesMatch = JournalEntriesRegex.Match(content);
            if (entriesMatch.Success)
            {
                string questParamKey = entriesMatch.Groups[1].Value;
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"ScriptParameterParser: Dynamic source FROM_JOURNAL_ENTRIES({questParamKey}) for key '{key}' - storing dependency");

                // Store the dependency: this key depends on questParamKey
                declarations.Dependencies[key] = questParamKey;

                // Return all entry IDs as placeholder (will be filtered at runtime based on selected quest)
                return GetAllJournalEntryIDs();
            }

            // Default: parse as static list
            return ParseList(content);
        }

        /// <summary>
        /// Retrieves quest tags from journal cache file.
        /// </summary>
        /// <returns>List of quest tags or message if empty</returns>
        private List<string> GetJournalTags()
        {
            try
            {
                var cacheFilePath = JournalService.GetCacheFilePath();
                if (!File.Exists(cacheFilePath))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        "ScriptParameterParser: Journal cache file not found");
                    return new List<string> { "Journal cache not found - open a dialog file to generate" };
                }

                // Read cache file
                var json = File.ReadAllText(cacheFilePath);
                var cache = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);

                if (cache == null || cache.Count == 0)
                {
                    return new List<string> { "No journal entries found" };
                }

                // Return sorted quest tags (keys)
                var tags = cache.Keys.OrderBy(k => k).ToList();

                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"ScriptParameterParser: Loaded {tags.Count} quest tags from cache");
                return tags;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"ScriptParameterParser: Error loading journal cache - {ex.Message}");
                return new List<string> { "Error loading journal data" };
            }
        }

        /// <summary>
        /// Retrieves all unique entry IDs from journal cache file.
        /// Returns all entry IDs across all quests (for initial display before quest selection).
        /// </summary>
        /// <returns>List of entry IDs or message if empty</returns>
        private List<string> GetAllJournalEntryIDs()
        {
            try
            {
                var cacheFilePath = JournalService.GetCacheFilePath();
                if (!File.Exists(cacheFilePath))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        "ScriptParameterParser: Journal cache file not found");
                    return new List<string> { "Journal cache not found - open a dialog file to generate" };
                }

                // Read cache file
                var json = File.ReadAllText(cacheFilePath);
                var cache = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);

                if (cache == null || cache.Count == 0)
                {
                    return new List<string> { "No journal entries found" };
                }

                // Return all unique entry IDs across all quests
                var allIDs = new HashSet<string>();
                foreach (var questEntries in cache.Values)
                {
                    foreach (var id in questEntries)
                    {
                        allIDs.Add(id);
                    }
                }

                var sortedIDs = allIDs.OrderBy(id => int.TryParse(id, out var num) ? num : int.MaxValue).ToList();

                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"ScriptParameterParser: Loaded {sortedIDs.Count} unique entry IDs from cache");
                return sortedIDs;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"ScriptParameterParser: Error loading journal cache - {ex.Message}");
                return new List<string> { "Error loading journal data" };
            }
        }

        /// <summary>
        /// Parses a list of parameter keys or values from a comment section.
        /// Supports both newline-separated and comma-separated formats.
        /// </summary>
        /// <param name="content">The raw content from KeyList or ValueList section</param>
        /// <returns>Cleaned list of parameter strings</returns>
        private List<string> ParseList(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return new List<string>();

            try
            {
                return content
                    .Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Where(s => !s.StartsWith("//"))  // Filter out line comments
                    .Where(s => !s.StartsWith("/*"))  // Filter out block comment starts
                    .Where(s => !s.StartsWith("*/"))  // Filter out block comment ends
                    .Where(s => !s.StartsWith("*"))   // Filter out block comment lines
                    .Distinct()
                    .ToList();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"ScriptParameterParser: Error parsing list content - {ex.Message}");
                return new List<string>();
            }
        }
    }
}
