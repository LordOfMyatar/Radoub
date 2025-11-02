using System.Collections.Generic;

namespace DialogEditor.Models
{
    /// <summary>
    /// Represents a journal category (quest) from module.jrl
    /// </summary>
    public class JournalCategory
    {
        public string Tag { get; set; } = string.Empty;
        public LocString? Name { get; set; }
        public uint Priority { get; set; } = 0;
        public uint XP { get; set; } = 0;
        public string Comment { get; set; } = string.Empty;
        public List<JournalEntry> Entries { get; set; } = new();

        /// <summary>
        /// Display string for UI: "tag - Name" or just tag if no name
        /// Null-safe for nodes without quest data
        /// </summary>
        public string DisplayName
        {
            get
            {
                var nameText = Name?.GetDefault();
                return string.IsNullOrEmpty(nameText)
                    ? Tag
                    : $"{Tag} - {nameText}";
            }
        }
    }

    /// <summary>
    /// Represents a journal entry within a category
    /// </summary>
    public class JournalEntry
    {
        public uint ID { get; set; }
        public LocString? Text { get; set; }
        public bool End { get; set; } = false;

        /// <summary>
        /// Display string for UI: Just entry ID (text shown in tooltip)
        /// Null-safe for nodes without quest data
        /// </summary>
        public string DisplayText
        {
            get
            {
                return $"Entry {ID}";
            }
        }

        /// <summary>
        /// Preview of text for secondary display (first 20 chars)
        /// </summary>
        public string TextPreview
        {
            get
            {
                var text = Text?.GetDefault() ?? string.Empty;
                if (string.IsNullOrEmpty(text))
                    return "";

                var preview = text.Length > 20 ? text.Substring(0, 20) + "..." : text;
                return preview;
            }
        }

        /// <summary>
        /// Full text for tooltips - null-safe
        /// </summary>
        public string FullText => Text?.GetDefault() ?? string.Empty;
    }
}
