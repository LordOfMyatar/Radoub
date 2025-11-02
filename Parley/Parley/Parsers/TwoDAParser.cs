using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogEditor.Services;

namespace DialogEditor.Parsers
{
    /// <summary>
    /// Parser for NWN 2DA (plain-text table) files.
    /// Format: Space-delimited rows/columns with header.
    /// Used for game data tables (classes.2da, portraits.2da, etc.)
    /// </summary>
    public class TwoDAParser
    {
        /// <summary>
        /// Parse 2DA file and return rows as dictionaries.
        /// Key = column name, Value = cell value.
        /// </summary>
        public List<Dictionary<string, string>> ParseFile(string filePath)
        {
            var rows = new List<Dictionary<string, string>>();

            try
            {
                if (!File.Exists(filePath))
                {
                    UnifiedLogger.LogParser(LogLevel.WARN, $"2DA file not found: {filePath}");
                    return rows;
                }

                var lines = File.ReadAllLines(filePath);

                if (lines.Length < 4)
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, $"2DA file too short (need 4+ lines): {filePath}");
                    return rows;
                }

                // Line 1: Version header (e.g., "2DA V2.0")
                if (!lines[0].StartsWith("2DA"))
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, $"Invalid 2DA header: {lines[0]}");
                    return rows;
                }

                // Line 2: Blank or DEFAULT: value (skip)
                // Line 3: Column names
                var columnNames = ParseColumns(lines[2]);
                if (columnNames.Count == 0)
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, "No column names found in 2DA");
                    return rows;
                }

                // Lines 4+: Row data
                for (int i = 3; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    var rowData = ParseRow(line, columnNames);
                    if (rowData != null)
                    {
                        rows.Add(rowData);
                    }
                }

                UnifiedLogger.LogParser(LogLevel.INFO,
                    $"Parsed 2DA: {Path.GetFileName(filePath)} - {rows.Count} rows, {columnNames.Count} columns");

                return rows;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"Error parsing 2DA file {filePath}: {ex.Message}");
                return rows;
            }
        }

        /// <summary>
        /// Parse column names from header line (space-delimited).
        /// </summary>
        private List<string> ParseColumns(string line)
        {
            var columns = new List<string>();
            var tokens = TokenizeLine(line);

            // Columns are all tokens (no row index in header)
            foreach (var token in tokens)
            {
                if (!string.IsNullOrEmpty(token))
                {
                    columns.Add(token);
                }
            }

            return columns;
        }

        /// <summary>
        /// Parse data row into dictionary (column name → value).
        /// First token is row index (stored as "RowIndex" key).
        /// </summary>
        private Dictionary<string, string>? ParseRow(string line, List<string> columnNames)
        {
            try
            {
                var tokens = TokenizeLine(line);

                if (tokens.Count == 0)
                    return null;

                var rowData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // First token is row index
                rowData["RowIndex"] = tokens[0];

                // Map remaining tokens to column names
                for (int i = 1; i < tokens.Count && (i - 1) < columnNames.Count; i++)
                {
                    var columnName = columnNames[i - 1];
                    var value = tokens[i];

                    // Handle blank entries (****)
                    if (value == "****")
                        value = string.Empty;

                    rowData[columnName] = value;
                }

                return rowData;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.WARN, $"Error parsing 2DA row: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Tokenize 2DA line (space-delimited, handles quoted strings).
        /// </summary>
        private List<string> TokenizeLine(string line)
        {
            var tokens = new List<string>();
            var currentToken = "";
            var inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == '"')
                {
                    // Toggle quote mode
                    inQuotes = !inQuotes;
                }
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    // End of token (if not in quotes)
                    if (!string.IsNullOrEmpty(currentToken))
                    {
                        tokens.Add(currentToken);
                        currentToken = "";
                    }
                }
                else
                {
                    // Add character to current token
                    currentToken += c;
                }
            }

            // Add final token
            if (!string.IsNullOrEmpty(currentToken))
            {
                tokens.Add(currentToken);
            }

            return tokens;
        }

        /// <summary>
        /// Parse 2DA and create lookup dictionary (row index → column value).
        /// Useful for simple ID→Name lookups.
        /// </summary>
        public Dictionary<int, string> ParseAsLookup(string filePath, string valueColumn)
        {
            var lookup = new Dictionary<int, string>();

            try
            {
                var rows = ParseFile(filePath);

                foreach (var row in rows)
                {
                    if (row.TryGetValue("RowIndex", out var indexStr) &&
                        int.TryParse(indexStr, out var index))
                    {
                        if (row.TryGetValue(valueColumn, out var value))
                        {
                            lookup[index] = value;
                        }
                    }
                }

                UnifiedLogger.LogParser(LogLevel.INFO,
                    $"Created 2DA lookup: {Path.GetFileName(filePath)}[{valueColumn}] - {lookup.Count} entries");

                return lookup;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR,
                    $"Error creating 2DA lookup from {filePath}: {ex.Message}");
                return lookup;
            }
        }
    }
}
