using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;

namespace DialogEditor.Parsers
{
    /// <summary>
    /// Parses Neverwinter Nights UTC (creature) files.
    /// Inherits GFF parsing from GffParser base class.
    /// Supports NWN 1.69+ format (20+ years post-release with extended features).
    /// </summary>
    public class CreatureParser : GffParser
    {
        /// <summary>
        /// Parse UTC file and extract creature information.
        /// Focus: Tag, FirstName, LastName, Description, ClassList, PortraitId.
        /// </summary>
        public async Task<CreatureInfo?> ParseFromFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    UnifiedLogger.LogParser(LogLevel.WARN, $"UTC file not found: {UnifiedLogger.SanitizePath(filePath)}");
                    return null;
                }

                if (!IsValidUtcFile(filePath))
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, $"Invalid UTC file: {UnifiedLogger.SanitizePath(filePath)}");
                    return null;
                }

                UnifiedLogger.LogParser(LogLevel.DEBUG, $"Parsing UTC file: {Path.GetFileName(filePath)}");

                var buffer = await File.ReadAllBytesAsync(filePath);
                var rootStruct = ParseGffFromBuffer(buffer);

                if (rootStruct == null)
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, "Failed to parse GFF structure from UTC");
                    return null;
                }

                var creatureInfo = ExtractCreatureData(rootStruct);

                if (creatureInfo != null)
                {
                    UnifiedLogger.LogParser(LogLevel.DEBUG,
                        $"Parsed creature: Tag={creatureInfo.Tag}, Name={creatureInfo.DisplayName}");
                }

                return creatureInfo;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"Error parsing UTC file {UnifiedLogger.SanitizePath(filePath)}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Validate that file is a UTC file by checking header.
        /// </summary>
        private bool IsValidUtcFile(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                if (stream.Length < 8)
                    return false;

                var buffer = new byte[8];
                stream.ReadExactly(buffer, 0, 8);

                // Check for "UTC V3.2" header
                var fileType = System.Text.Encoding.ASCII.GetString(buffer, 0, 4);
                var fileVersion = System.Text.Encoding.ASCII.GetString(buffer, 4, 4);

                return fileType == "UTC " && fileVersion == "V3.2";
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.DEBUG, $"File is not a valid UTC: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Extract creature data from parsed GFF root struct.
        /// Handles NWN 1.69+ extended format (e.g., ClassList may exceed original 3-class limit).
        /// </summary>
        private CreatureInfo? ExtractCreatureData(GffStruct rootStruct)
        {
            try
            {
                var creature = new CreatureInfo();

                // Extract Tag (CExoString) - CRITICAL field
                creature.Tag = rootStruct.GetFieldValue<string>("Tag", string.Empty);
                if (string.IsNullOrEmpty(creature.Tag))
                {
                    UnifiedLogger.LogParser(LogLevel.WARN, "Creature has empty Tag field");
                }

                // Extract FirstName (CExoLocString → LocString conversion)
                var firstNameField = rootStruct.GetField("FirstName");
                if (firstNameField != null && firstNameField.Value is CExoLocString cexoFirstName)
                {
                    creature.FirstName = ConvertCExoLocString(cexoFirstName);
                }

                // Extract LastName (CExoLocString → LocString conversion)
                var lastNameField = rootStruct.GetField("LastName");
                if (lastNameField != null && lastNameField.Value is CExoLocString cexoLastName)
                {
                    creature.LastName = ConvertCExoLocString(cexoLastName);
                }

                // Extract Description (CExoLocString → LocString conversion)
                var descField = rootStruct.GetField("Description");
                if (descField != null && descField.Value is CExoLocString cexoDescription)
                {
                    creature.Description = ConvertCExoLocString(cexoDescription);
                }

                // Extract PortraitId (WORD)
                creature.PortraitId = rootStruct.GetFieldValue<ushort>("PortraitId", (ushort)0);

                // Extract SoundSetFile (WORD) - Index into soundset.2da (#786)
                creature.SoundSetFile = rootStruct.GetFieldValue<ushort>("SoundSetFile", ushort.MaxValue);

                // Extract ClassList (List, StructID 2)
                // Note: Original spec says "up to 3" but NWN 1.69+ may support more
                var classListField = rootStruct.GetField("ClassList");
                if (classListField != null && classListField.Type == GffField.List && classListField.Value is GffList classList)
                {
                    creature.Classes = ParseClassList(classList);

                    // Log if class count exceeds original 3-class limit
                    if (creature.Classes.Count > 3)
                    {
                        UnifiedLogger.LogParser(LogLevel.INFO,
                            $"Creature has {creature.Classes.Count} classes (exceeds original 3-class limit, confirms NWN 1.69+ extended support)");
                    }
                }
                else
                {
                    UnifiedLogger.LogParser(LogLevel.WARN, "Creature has no ClassList or invalid ClassList format");
                }

                return creature;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"Error extracting creature data: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parse ClassList from GFF list field.
        /// Each class struct (StructID 2) contains: Class (INT), ClassLevel (SHORT).
        /// </summary>
        private System.Collections.Generic.List<CreatureClass> ParseClassList(GffList classList)
        {
            var classes = new System.Collections.Generic.List<CreatureClass>();

            if (classList.Elements == null || classList.Elements.Count == 0)
            {
                return classes;
            }

            foreach (var classStruct in classList.Elements)
            {
                try
                {
                    // Verify StructID 2 (optional, for validation)
                    if (classStruct.Type != 2)
                    {
                        UnifiedLogger.LogParser(LogLevel.DEBUG,
                            $"ClassList element has unexpected StructID {classStruct.Type} (expected 2)");
                    }

                    var creatureClass = new CreatureClass
                    {
                        // Class (INT) - Index into classes.2da
                        ClassId = classStruct.GetFieldValue<int>("Class", 0),

                        // ClassLevel (SHORT)
                        Level = classStruct.GetFieldValue<short>("ClassLevel", (short)0)
                    };

                    // Validate class data
                    if (creatureClass.ClassId == 0 && creatureClass.Level == 0)
                    {
                        UnifiedLogger.LogParser(LogLevel.DEBUG, "Skipping empty class entry");
                        continue;
                    }

                    classes.Add(creatureClass);
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogParser(LogLevel.WARN, $"Error parsing class struct: {ex.Message}");
                }
            }

            return classes;
        }

        /// <summary>
        /// Convert GFF CExoLocString to Models.LocString.
        /// Handles the two different LocString implementations in the codebase.
        /// </summary>
        private LocString ConvertCExoLocString(CExoLocString cexo)
        {
            var locString = new LocString();

            // Convert localized strings (uint keys → int keys)
            foreach (var kvp in cexo.LocalizedStrings)
            {
                locString.Add((int)kvp.Key, kvp.Value);
            }

            return locString;
        }
    }
}
