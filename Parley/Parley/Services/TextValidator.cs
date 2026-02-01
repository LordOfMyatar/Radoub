using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DialogEditor.Services
{
    /// <summary>
    /// Validates dialog text for NWN compatibility.
    /// NWN uses Windows-1252 (CP1252) encoding - characters outside this range won't render.
    /// Issue #152: Warn users about unsupported characters before save.
    /// </summary>
    public static class TextValidator
    {
        // Windows-1252 can encode code points 0-255, but some are undefined
        // Undefined positions in CP1252: 0x81, 0x8D, 0x8F, 0x90, 0x9D
        private static readonly HashSet<int> UndefinedCp1252 = new() { 0x81, 0x8D, 0x8F, 0x90, 0x9D };

        /// <summary>
        /// Check if a character is supported by NWN (Windows-1252 compatible).
        /// </summary>
        public static bool IsCharacterSupported(char c)
        {
            int codePoint = c;

            // Basic ASCII (0-127) is always supported
            if (codePoint <= 127)
                return true;

            // Extended ASCII (128-255) - most are supported except undefined positions
            if (codePoint <= 255)
                return !UndefinedCp1252.Contains(codePoint);

            // Anything above 255 is not in Windows-1252
            return false;
        }

        /// <summary>
        /// Find all unsupported characters in a text string.
        /// </summary>
        /// <param name="text">Text to validate</param>
        /// <returns>List of unsupported characters with their positions</returns>
        public static List<UnsupportedCharacter> FindUnsupportedCharacters(string? text)
        {
            var result = new List<UnsupportedCharacter>();
            if (string.IsNullOrEmpty(text))
                return result;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (!IsCharacterSupported(c))
                {
                    result.Add(new UnsupportedCharacter
                    {
                        Character = c,
                        Position = i,
                        CodePoint = (int)c,
                        Context = GetContext(text, i)
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Get surrounding context for a character position.
        /// </summary>
        private static string GetContext(string text, int position, int contextChars = 10)
        {
            int start = Math.Max(0, position - contextChars);
            int end = Math.Min(text.Length, position + contextChars + 1);
            var context = text.Substring(start, end - start);

            // Mark the problem character
            int markerPos = position - start;
            return context.Substring(0, markerPos) + "→" + context[markerPos] + "←" +
                   (markerPos + 1 < context.Length ? context.Substring(markerPos + 1) : "");
        }

        /// <summary>
        /// Validate all text in a dialog and return warnings.
        /// </summary>
        /// <param name="dialog">Dialog to validate</param>
        /// <returns>Validation result with any warnings</returns>
        public static TextValidationResult ValidateDialog(Models.Dialog dialog)
        {
            var result = new TextValidationResult();

            if (dialog == null)
                return result;

            // Check all entries
            for (int i = 0; i < dialog.Entries.Count; i++)
            {
                ValidateNode(dialog.Entries[i], "Entry", i, result);
            }

            // Check all replies
            for (int i = 0; i < dialog.Replies.Count; i++)
            {
                ValidateNode(dialog.Replies[i], "Reply", i, result);
            }

            return result;
        }

        private static void ValidateNode(Models.DialogNode node, string nodeType, int nodeIndex, TextValidationResult result)
        {
            // Check main text in all languages
            foreach (var kvp in node.Text.Strings)
            {
                var unsupported = FindUnsupportedCharacters(kvp.Value);
                foreach (var uc in unsupported)
                {
                    result.AddWarning(new TextValidationWarning
                    {
                        NodeType = nodeType,
                        NodeIndex = nodeIndex,
                        LanguageId = kvp.Key,
                        FieldName = "Text",
                        Character = uc
                    });
                }
            }

            // Check comment field
            var commentUnsupported = FindUnsupportedCharacters(node.Comment);
            foreach (var uc in commentUnsupported)
            {
                result.AddWarning(new TextValidationWarning
                {
                    NodeType = nodeType,
                    NodeIndex = nodeIndex,
                    LanguageId = 0,
                    FieldName = "Comment",
                    Character = uc
                });
            }
        }

        /// <summary>
        /// Get a friendly description of a character for user display.
        /// </summary>
        public static string GetCharacterDescription(char c)
        {
            int codePoint = (int)c;

            // Common emoji ranges
            if (codePoint >= 0x1F600 && codePoint <= 0x1F64F)
                return "Emoticon";
            if (codePoint >= 0x1F300 && codePoint <= 0x1F5FF)
                return "Symbol/Pictograph";
            if (codePoint >= 0x1F680 && codePoint <= 0x1F6FF)
                return "Transport/Map Symbol";
            if (codePoint >= 0x2600 && codePoint <= 0x26FF)
                return "Misc Symbol";
            if (codePoint >= 0x2700 && codePoint <= 0x27BF)
                return "Dingbat";

            // Unicode block detection for common non-Latin scripts
            if (codePoint >= 0x0400 && codePoint <= 0x04FF)
                return "Cyrillic";
            if (codePoint >= 0x0370 && codePoint <= 0x03FF)
                return "Greek";
            if (codePoint >= 0x0590 && codePoint <= 0x05FF)
                return "Hebrew";
            if (codePoint >= 0x0600 && codePoint <= 0x06FF)
                return "Arabic";
            if (codePoint >= 0x4E00 && codePoint <= 0x9FFF)
                return "CJK (Chinese/Japanese/Korean)";
            if (codePoint >= 0x3040 && codePoint <= 0x309F)
                return "Hiragana";
            if (codePoint >= 0x30A0 && codePoint <= 0x30FF)
                return "Katakana";

            // Generic fallback
            if (codePoint > 255)
                return $"Unicode (U+{codePoint:X4})";

            return "Extended ASCII";
        }
    }

    /// <summary>
    /// Represents an unsupported character found in text.
    /// </summary>
    public class UnsupportedCharacter
    {
        public char Character { get; set; }
        public int Position { get; set; }
        public int CodePoint { get; set; }
        public string Context { get; set; } = string.Empty;

        public string Description => TextValidator.GetCharacterDescription(Character);

        public override string ToString()
        {
            return $"'{Character}' (U+{CodePoint:X4}) at position {Position}";
        }
    }

    /// <summary>
    /// Warning about unsupported text in a specific dialog node.
    /// </summary>
    public class TextValidationWarning
    {
        public string NodeType { get; set; } = string.Empty;
        public int NodeIndex { get; set; }
        public int LanguageId { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public UnsupportedCharacter Character { get; set; } = new();

        public override string ToString()
        {
            var langInfo = LanguageId > 0 ? $" (lang {LanguageId})" : "";
            return $"{NodeType}[{NodeIndex}].{FieldName}{langInfo}: {Character}";
        }
    }

    /// <summary>
    /// Result of validating all text in a dialog.
    /// </summary>
    public class TextValidationResult
    {
        public List<TextValidationWarning> Warnings { get; } = new();
        public bool HasWarnings => Warnings.Count > 0;

        /// <summary>
        /// Number of unique nodes with issues.
        /// </summary>
        public int AffectedNodeCount => Warnings
            .Select(w => $"{w.NodeType}{w.NodeIndex}")
            .Distinct()
            .Count();

        /// <summary>
        /// Total number of unsupported characters found.
        /// </summary>
        public int TotalCharacterCount => Warnings.Count;

        public void AddWarning(TextValidationWarning warning)
        {
            Warnings.Add(warning);
        }

        /// <summary>
        /// Get a summary message for the user.
        /// </summary>
        public string GetSummary()
        {
            if (!HasWarnings)
                return "No unsupported characters found.";

            return $"Found {TotalCharacterCount} unsupported character(s) in {AffectedNodeCount} node(s).";
        }

        /// <summary>
        /// Group warnings by node for cleaner display.
        /// </summary>
        public Dictionary<string, List<TextValidationWarning>> GroupByNode()
        {
            return Warnings
                .GroupBy(w => $"{w.NodeType}[{w.NodeIndex}]")
                .ToDictionary(g => g.Key, g => g.ToList());
        }
    }
}
