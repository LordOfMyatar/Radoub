using System;
using System.IO;
using System.Linq;
using DialogEditor.Services;

namespace DialogEditor.Parsers
{
    /// <summary>
    /// Parser for module.ifo files (Module Information GFF format).
    /// Extracts module metadata like module name.
    /// </summary>
    public class ModuleInfoParser : GffParser
    {
        /// <summary>
        /// Parse module.ifo file and extract module name.
        /// Returns null if file not found or parsing fails.
        /// </summary>
        public static string? GetModuleName(string directoryPath)
        {
            try
            {
                var ifoPath = Path.Combine(directoryPath, "module.ifo");

                if (!File.Exists(ifoPath))
                {
                    UnifiedLogger.LogParser(LogLevel.DEBUG, $"No module.ifo found in {directoryPath}");
                    return null;
                }

                var buffer = File.ReadAllBytes(ifoPath);
                var parser = new ModuleInfoParser();
                var rootStruct = parser.ParseGffFromBuffer(buffer);

                if (rootStruct == null)
                {
                    UnifiedLogger.LogParser(LogLevel.WARN, "Failed to parse module.ifo");
                    return null;
                }

                // Extract Mod_Name field (CExoLocString)
                var modNameField = rootStruct.Fields.FirstOrDefault(f => f.Label == "Mod_Name");
                if (modNameField != null && modNameField.Type == GffField.CExoLocString)
                {
                    var moduleName = modNameField.Value as string;
                    if (!string.IsNullOrWhiteSpace(moduleName))
                    {
                        UnifiedLogger.LogParser(LogLevel.INFO, $"Module name: {moduleName}");
                        return moduleName;
                    }
                }

                UnifiedLogger.LogParser(LogLevel.WARN, "Mod_Name field not found in module.ifo");
                return null;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"Error reading module.ifo: {ex.Message}");
                return null;
            }
        }
    }
}
