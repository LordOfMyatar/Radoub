using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.Formats.Utc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Quartermaster.Services;

/// <summary>
/// Service for loading and saving Aurora Toolset script template INI files.
/// Format: [ResRefs] section with key=value pairs mapping event names to script ResRefs.
/// </summary>
public static class ScriptTemplateService
{
    /// <summary>
    /// Mapping from INI key names to UtcFile property names.
    /// </summary>
    private static readonly Dictionary<string, string> IniKeyToFieldMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OnBlocked"] = nameof(UtcFile.ScriptOnBlocked),
        ["OnDamaged"] = nameof(UtcFile.ScriptDamaged),
        ["OnDeath"] = nameof(UtcFile.ScriptDeath),
        ["OnConversation"] = nameof(UtcFile.ScriptDialogue),
        ["OnDisturbed"] = nameof(UtcFile.ScriptDisturbed),
        ["OnCombatRoundEnd"] = nameof(UtcFile.ScriptEndRound),
        ["OnHeartbeat"] = nameof(UtcFile.ScriptHeartbeat),
        ["OnPhysicalAttacked"] = nameof(UtcFile.ScriptAttacked),
        ["OnPerception"] = nameof(UtcFile.ScriptOnNotice),
        ["OnRested"] = nameof(UtcFile.ScriptRested),
        ["OnSpawn"] = nameof(UtcFile.ScriptSpawn),
        ["OnSpellCast"] = nameof(UtcFile.ScriptSpellAt),
        ["OnUserDefined"] = nameof(UtcFile.ScriptUserDefine),
    };

    /// <summary>
    /// Reverse mapping from UtcFile property names to INI key names.
    /// </summary>
    private static readonly Dictionary<string, string> FieldToIniKeyMap =
        IniKeyToFieldMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.Ordinal);

    /// <summary>
    /// Gets the default script templates directory.
    /// Uses {NeverwinterNightsPath}/scripttemplates/.
    /// </summary>
    public static string GetDefaultTemplateDirectory()
    {
        var nwnPath = RadoubSettings.Instance.NeverwinterNightsPath;
        if (!string.IsNullOrEmpty(nwnPath))
            return Path.Combine(nwnPath, "scripttemplates");
        return "";
    }

    /// <summary>
    /// Ensures the template directory exists, creating it if needed.
    /// Returns the directory path, or empty string if NWN path is not configured.
    /// </summary>
    public static string EnsureTemplateDirectory()
    {
        var dir = GetDefaultTemplateDirectory();
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            try
            {
                Directory.CreateDirectory(dir);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Created script templates directory: {UnifiedLogger.SanitizePath(dir)}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to create script templates directory: {ex.Message}");
                return "";
            }
        }
        return dir;
    }

    /// <summary>
    /// Loads a script template INI file and returns a dictionary of UtcFile field names to ResRef values.
    /// </summary>
    public static Dictionary<string, string> LoadTemplate(string filePath)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        var lines = File.ReadAllLines(filePath);
        bool inResRefsSection = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            // Section header
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var sectionName = line[1..^1].Trim();
                inResRefsSection = sectionName.Equals("ResRefs", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inResRefsSection)
                continue;

            // Parse key=value
            var eqIndex = line.IndexOf('=');
            if (eqIndex <= 0) continue;

            var iniKey = line[..eqIndex].Trim();
            var resRef = line[(eqIndex + 1)..].Trim();

            // Map INI key to UtcFile field name
            if (IniKeyToFieldMap.TryGetValue(iniKey, out var fieldName))
            {
                result[fieldName] = resRef;
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Unknown INI key in script template: {iniKey}");
            }
        }

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"Loaded script template: {Path.GetFileName(filePath)} ({result.Count} scripts)");

        return result;
    }

    /// <summary>
    /// Saves current scripts as a script template INI file.
    /// </summary>
    /// <param name="filePath">Path to save the INI file.</param>
    /// <param name="scripts">Dictionary of UtcFile field names to ResRef values.</param>
    public static void SaveTemplate(string filePath, Dictionary<string, string> scripts)
    {
        using var writer = new StreamWriter(filePath);
        writer.WriteLine("[ResRefs]");

        // Write in a consistent order matching the Aurora Toolset convention
        foreach (var (iniKey, fieldName) in IniKeyToFieldMap)
        {
            var resRef = scripts.TryGetValue(fieldName, out var value) ? value : "";
            writer.WriteLine($"{iniKey}={resRef}");
        }

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"Saved script template: {Path.GetFileName(filePath)}");
    }

    /// <summary>
    /// Maps an INI key name to a UtcFile field name.
    /// Returns null if the key is not recognized.
    /// </summary>
    public static string? IniKeyToFieldName(string iniKey)
    {
        return IniKeyToFieldMap.TryGetValue(iniKey, out var fieldName) ? fieldName : null;
    }

    /// <summary>
    /// Maps a UtcFile field name to an INI key name.
    /// Returns null if the field is not recognized.
    /// </summary>
    public static string? FieldNameToIniKey(string fieldName)
    {
        return FieldToIniKeyMap.TryGetValue(fieldName, out var iniKey) ? iniKey : null;
    }
}
