using System.Reflection;
using System.Text.Json;
using Radoub.Dictionary.Models;

namespace Radoub.Dictionary;

/// <summary>
/// Discovers available dictionaries from bundled resources and user directories.
/// </summary>
public class DictionaryDiscovery
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Well-known language names for display
    private static readonly Dictionary<string, string> LanguageNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en_US"] = "English (US)",
        ["en_GB"] = "English (UK)",
        ["es_ES"] = "Spanish (Spain)",
        ["es_MX"] = "Spanish (Mexico)",
        ["fr_FR"] = "French",
        ["de_DE"] = "German",
        ["it_IT"] = "Italian",
        ["pt_BR"] = "Portuguese (Brazil)",
        ["pt_PT"] = "Portuguese (Portugal)",
        ["nl_NL"] = "Dutch",
        ["pl_PL"] = "Polish",
        ["ru_RU"] = "Russian",
        ["uk_UA"] = "Ukrainian",
        ["cs_CZ"] = "Czech",
        ["sv_SE"] = "Swedish",
        ["da_DK"] = "Danish",
        ["nb_NO"] = "Norwegian",
        ["fi_FI"] = "Finnish"
    };

    private readonly string _userDictionaryPath;
    private List<DictionaryInfo>? _cachedDictionaries;

    /// <summary>
    /// Creates a new dictionary discovery instance.
    /// </summary>
    /// <param name="userDictionaryPath">Path to user's dictionary folder (e.g., ~/Radoub/Dictionaries).</param>
    public DictionaryDiscovery(string userDictionaryPath)
    {
        _userDictionaryPath = userDictionaryPath;
    }

    /// <summary>
    /// Gets the default user dictionary path for the current platform.
    /// </summary>
    public static string GetDefaultUserDictionaryPath()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, "Radoub", "Dictionaries");
    }

    /// <summary>
    /// Scans for all available dictionaries (bundled + user-installed).
    /// </summary>
    /// <param name="forceRescan">If true, ignores cached results.</param>
    public List<DictionaryInfo> ScanForDictionaries(bool forceRescan = false)
    {
        if (_cachedDictionaries != null && !forceRescan)
            return _cachedDictionaries;

        var dictionaries = new List<DictionaryInfo>();

        // Discover bundled dictionaries
        dictionaries.AddRange(DiscoverBundledDictionaries());

        // Discover user-installed dictionaries
        dictionaries.AddRange(DiscoverUserDictionaries());

        _cachedDictionaries = dictionaries;
        return dictionaries;
    }

    /// <summary>
    /// Gets all available Hunspell language dictionaries.
    /// </summary>
    public List<DictionaryInfo> GetAvailableLanguages(bool forceRescan = false)
    {
        return ScanForDictionaries(forceRescan)
            .Where(d => d.Type == DictionaryType.Hunspell)
            .OrderByDescending(d => d.IsBundled) // Bundled first
            .ThenBy(d => d.Name)
            .ToList();
    }

    /// <summary>
    /// Gets all available custom dictionaries (NWN, LOTR, etc.).
    /// </summary>
    public List<DictionaryInfo> GetAvailableCustomDictionaries(bool forceRescan = false)
    {
        return ScanForDictionaries(forceRescan)
            .Where(d => d.Type == DictionaryType.Custom)
            .OrderByDescending(d => d.IsBundled) // Bundled first
            .ThenBy(d => d.Name)
            .ToList();
    }

    /// <summary>
    /// Clears the cached dictionary list, forcing a rescan on next call.
    /// </summary>
    public void ClearCache()
    {
        _cachedDictionaries = null;
    }

    private List<DictionaryInfo> DiscoverBundledDictionaries()
    {
        var dictionaries = new List<DictionaryInfo>();
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();

        // Find bundled Hunspell dictionaries (e.g., Radoub.Dictionary.Dictionaries.en_US.en_US.dic)
        var hunspellResources = resourceNames
            .Where(r => r.EndsWith(".dic", StringComparison.OrdinalIgnoreCase))
            .Where(r => r.Contains(".Dictionaries.") && !r.Contains(".nwn."))
            .ToList();

        foreach (var dicResource in hunspellResources)
        {
            // Extract language code from resource name
            // Format: Radoub.Dictionary.Dictionaries.{lang}.{lang}.dic
            var parts = dicResource.Split('.');
            if (parts.Length >= 4)
            {
                var langIndex = Array.IndexOf(parts, "Dictionaries") + 1;
                if (langIndex > 0 && langIndex < parts.Length - 2)
                {
                    var langCode = parts[langIndex];
                    var affResource = dicResource.Replace(".dic", ".aff");

                    if (resourceNames.Contains(affResource))
                    {
                        dictionaries.Add(new DictionaryInfo
                        {
                            Id = langCode,
                            Name = GetLanguageName(langCode),
                            Type = DictionaryType.Hunspell,
                            Path = dicResource, // Resource name, not file path
                            IsBundled = true,
                            LanguageCode = langCode
                        });
                    }
                }
            }
        }

        // Find bundled NWN dictionary
        var nwnResource = resourceNames.FirstOrDefault(r =>
            r.EndsWith(".nwn.dic", StringComparison.OrdinalIgnoreCase));

        if (nwnResource != null)
        {
            dictionaries.Add(new DictionaryInfo
            {
                Id = "nwn",
                Name = "NWN/D&D Terms",
                Type = DictionaryType.Custom,
                Path = nwnResource,
                IsBundled = true,
                Description = "Neverwinter Nights and D&D 3rd Edition terminology"
            });
        }

        return dictionaries;
    }

    private List<DictionaryInfo> DiscoverUserDictionaries()
    {
        var dictionaries = new List<DictionaryInfo>();

        if (!Directory.Exists(_userDictionaryPath))
            return dictionaries;

        // Scan for Hunspell dictionaries in subdirectories (e.g., ~/Radoub/Dictionaries/es_ES/)
        foreach (var subDir in Directory.GetDirectories(_userDictionaryPath))
        {
            var dirName = Path.GetFileName(subDir);
            var dicFile = Path.Combine(subDir, $"{dirName}.dic");
            var affFile = Path.Combine(subDir, $"{dirName}.aff");

            if (File.Exists(dicFile) && File.Exists(affFile))
            {
                dictionaries.Add(new DictionaryInfo
                {
                    Id = dirName,
                    Name = GetLanguageName(dirName),
                    Type = DictionaryType.Hunspell,
                    Path = dicFile,
                    IsBundled = false,
                    LanguageCode = dirName
                });
            }
        }

        // Scan for JSON custom dictionaries in root (e.g., ~/Radoub/Dictionaries/lotr.dic)
        var jsonPatterns = new[] { "*.dic", "*.json" };
        foreach (var pattern in jsonPatterns)
        {
            foreach (var file in Directory.GetFiles(_userDictionaryPath, pattern))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);

                // Skip settings file
                if (fileName.Equals("settings", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip user's personal custom dictionary (handled separately by SpellCheckService)
                if (fileName.Equals("custom", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip if it looks like a Hunspell file (has matching .aff)
                var affPath = Path.ChangeExtension(file, ".aff");
                if (File.Exists(affPath))
                    continue;

                // Try to load as JSON to get metadata
                var info = TryLoadJsonDictionaryInfo(file, fileName);
                if (info != null)
                {
                    dictionaries.Add(info);
                }
            }
        }

        return dictionaries;
    }

    private DictionaryInfo? TryLoadJsonDictionaryInfo(string filePath, string fallbackId)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var dict = JsonSerializer.Deserialize<CustomDictionary>(json, JsonOptions);

            if (dict == null)
                return null;

            return new DictionaryInfo
            {
                Id = fallbackId,
                Name = !string.IsNullOrWhiteSpace(dict.Source) ? dict.Source : fallbackId,
                Type = DictionaryType.Custom,
                Path = filePath,
                IsBundled = false,
                Description = dict.Description,
                WordCount = dict.Words.Count + (dict.Entries?.Count ?? 0)
            };
        }
        catch
        {
            // Invalid JSON or not a dictionary file - skip it
            return null;
        }
    }

    private static string GetLanguageName(string languageCode)
    {
        if (LanguageNames.TryGetValue(languageCode, out var name))
            return name;

        // Fallback: format code as readable name (e.g., "xx_YY" -> "Xx (YY)")
        if (languageCode.Length >= 2)
        {
            var parts = languageCode.Split('_');
            if (parts.Length == 2)
            {
                return $"{parts[0].ToUpperInvariant()} ({parts[1].ToUpperInvariant()})";
            }
        }

        return languageCode;
    }
}
