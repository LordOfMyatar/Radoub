using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DialogEditor.Models;
using DialogEditor.Parsers;
using DialogEditor.Utils;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service for parsing and caching NWN module.jrl (journal/quest) files
    /// </summary>
    public class JournalService
    {
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

                var fileBytes = await File.ReadAllBytesAsync(filePath);

                // Parse GFF structure
                var header = GffBinaryReader.ParseGffHeader(fileBytes);
                var structs = GffBinaryReader.ParseStructs(fileBytes, header);
                var fields = GffBinaryReader.ParseFields(fileBytes, header);
                var labels = GffBinaryReader.ParseLabels(fileBytes, header);

                GffBinaryReader.ResolveFieldLabels(fields, labels, fileBytes, header);
                GffBinaryReader.ResolveFieldValues(fields, structs, fileBytes, header);

                // Assign fields to their parent structs (CRITICAL step!)
                AssignFieldsToStructs(structs, fields, header, fileBytes);

                // Verify file type
                if (header.FileType != "JRL ")
                {
                    UnifiedLogger.LogJournal(LogLevel.WARN, $"Unexpected file type: {header.FileType} (expected JRL )");
                }

                // Root struct is structs[0]
                if (structs.Length == 0)
                {
                    UnifiedLogger.LogJournal(LogLevel.ERROR, "No root struct found in JRL file");
                    return new List<JournalCategory>();
                }

                var categories = ParseCategories(structs[0]);

                _cachedCategories = categories;
                _cachedFilePath = filePath;

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
        /// Parse Categories list from root struct
        /// </summary>
        private List<JournalCategory> ParseCategories(GffStruct rootStruct)
        {
            var categories = new List<JournalCategory>();

            // Debug: Log all fields in root struct
            UnifiedLogger.LogJournal(LogLevel.DEBUG, $"Journal root struct has {rootStruct.Fields.Count} fields:");
            foreach (var field in rootStruct.Fields)
            {
                UnifiedLogger.LogJournal(LogLevel.DEBUG, $"  Field: Label='{field.Label}', Type={field.Type}");
            }

            // Get Categories field (List)
            var categoriesField = rootStruct.Fields.FirstOrDefault(f => f.Label == "Categories");
            if (categoriesField == null || categoriesField.Type != GffField.List)
            {
                UnifiedLogger.LogJournal(LogLevel.WARN, "No Categories list found in journal root");
                return categories;
            }

            var categoriesList = categoriesField.Value as GffList;
            if (categoriesList == null || categoriesList.Elements == null)
            {
                return categories;
            }

            foreach (var categoryStruct in categoriesList.Elements)
            {
                var category = ParseCategory(categoryStruct);
                if (category != null)
                {
                    categories.Add(category);
                }
            }

            return categories;
        }

        /// <summary>
        /// Parse a single JournalCategory struct
        /// </summary>
        private JournalCategory? ParseCategory(GffStruct categoryStruct)
        {
            try
            {
                var category = new JournalCategory
                {
                    Tag = categoryStruct.GetFieldValue<string>("Tag", string.Empty),
                    Priority = categoryStruct.GetFieldValue<uint>("Priority", 0),
                    XP = categoryStruct.GetFieldValue<uint>("XP", 0),
                    Comment = categoryStruct.GetFieldValue<string>("Comment", string.Empty)
                };

                // Parse Name (CExoLocString)
                var nameField = categoryStruct.GetField("Name");
                if (nameField != null && nameField.Value is LocString locString)
                {
                    category.Name = locString;
                }

                // Parse EntryList
                var entryListField = categoryStruct.GetField("EntryList");
                if (entryListField != null && entryListField.Type == GffField.List && entryListField.Value is GffList entryList)
                {
                    category.Entries = ParseEntries(entryList);
                    UnifiedLogger.LogJournal(LogLevel.DEBUG, $"Quest '{category.Tag}' has {category.Entries.Count} entries");
                }
                else
                {
                    UnifiedLogger.LogJournal(LogLevel.DEBUG, $"Quest '{category.Tag}' has no EntryList field or it's not a list");
                }

                return category;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogJournal(LogLevel.ERROR, $"Error parsing journal category: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parse entries from EntryList
        /// </summary>
        private List<JournalEntry> ParseEntries(GffList entryList)
        {
            var entries = new List<JournalEntry>();

            if (entryList.Elements == null)
                return entries;

            foreach (var entryStruct in entryList.Elements)
            {
                try
                {
                    var entry = new JournalEntry
                    {
                        ID = entryStruct.GetFieldValue<uint>("ID", 0),
                        End = entryStruct.GetFieldValue<ushort>("End", 0) != 0
                    };

                    // Parse Text (CExoLocString)
                    var textField = entryStruct.GetField("Text");
                    if (textField != null && textField.Value is LocString locString)
                    {
                        entry.Text = locString;
                    }

                    entries.Add(entry);
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogJournal(LogLevel.ERROR, $"Error parsing journal entry: {ex.Message}");
                }
            }

            return entries;
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
        /// Clear cached journal data
        /// </summary>
        public void ClearCache()
        {
            _cachedCategories = null;
            _cachedFilePath = null;
            UnifiedLogger.LogJournal(LogLevel.DEBUG, "Journal cache cleared");
        }

        /// <summary>
        /// Assign fields to their parent structs (copied from DialogParser)
        /// </summary>
        private void AssignFieldsToStructs(GffStruct[] structs, GffField[] fields, GffHeader header, byte[] buffer)
        {
            for (int structIdx = 0; structIdx < structs.Length; structIdx++)
            {
                var gffStruct = structs[structIdx];
                if (gffStruct.FieldCount == 0)
                {
                    // No fields
                    continue;
                }
                else if (gffStruct.FieldCount == 1)
                {
                    // Single field - DataOrDataOffset is the field index
                    var fieldIndex = gffStruct.DataOrDataOffset;
                    if (fieldIndex < fields.Length)
                    {
                        gffStruct.Fields.Add(fields[fieldIndex]);
                    }
                }
                else
                {
                    // Multiple fields - DataOrDataOffset is a byte offset from FieldIndicesOffset
                    var indicesOffset = (int)(header.FieldIndicesOffset + gffStruct.DataOrDataOffset);

                    if (indicesOffset >= buffer.Length)
                    {
                        continue;
                    }

                    for (uint fieldIdx = 0; fieldIdx < gffStruct.FieldCount; fieldIdx++)
                    {
                        var indexPos = indicesOffset + (int)(fieldIdx * 4);
                        if (indexPos + 4 <= buffer.Length)
                        {
                            var fieldIndex = BitConverter.ToUInt32(buffer, indexPos);
                            if (fieldIndex < fields.Length)
                            {
                                gffStruct.Fields.Add(fields[fieldIndex]);
                            }
                        }
                    }
                }
            }
        }
    }
}
