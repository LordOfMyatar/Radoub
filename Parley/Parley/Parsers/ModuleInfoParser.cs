using System;
using System.IO;
using Radoub.Formats.Ifo;
using Radoub.Formats.Logging;

namespace DialogEditor.Parsers
{
    /// <summary>
    /// Helper for extracting module info from module.ifo files.
    /// Uses Radoub.Formats.Ifo library for parsing.
    /// </summary>
    public static class ModuleInfoParser
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

                var ifo = IfoReader.Read(ifoPath);

                // Get first available localized string from module name
                var moduleName = ifo.ModuleName.GetDefault();
                if (!string.IsNullOrWhiteSpace(moduleName))
                {
                    UnifiedLogger.LogParser(LogLevel.INFO, $"Module name: {moduleName}");
                    return moduleName;
                }

                UnifiedLogger.LogParser(LogLevel.WARN, "Mod_Name field empty in module.ifo");
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
