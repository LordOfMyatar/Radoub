using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DialogEditor.Models;
using DialogEditor.Services;

namespace DialogEditor.Parsers
{
    /// <summary>
    /// Parses NWScript (.nss) files to extract parameter declarations from comment blocks.
    /// Supports standardized ----KeyList---- and ----ValueList---- formats.
    /// </summary>
    public class ScriptParameterParser
    {
        // Regex patterns to extract KeyList and ValueList sections from comments
        private static readonly Regex KeyListRegex =
            new(@"----KeyList----\s*(.*?)\s*(?:----|/\*|\*/)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static readonly Regex ValueListRegex =
            new(@"----ValueList----\s*(.*?)\s*(?:----|/\*|\*/)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

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

                // Extract ValueList section
                var valueMatch = ValueListRegex.Match(nssContent);
                if (valueMatch.Success)
                {
                    declarations.Values = ParseList(valueMatch.Groups[1].Value);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"ScriptParameterParser: Found {declarations.Values.Count} values in ValueList");
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
