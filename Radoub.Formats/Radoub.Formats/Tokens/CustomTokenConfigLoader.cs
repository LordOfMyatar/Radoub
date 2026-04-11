using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Radoub.Formats.Logging;

namespace Radoub.Formats.Tokens;

/// <summary>
/// Loads user-defined custom (non-color) token configurations.
/// Configuration file location: ~/Radoub/custom-tokens.json
/// </summary>
public static class CustomTokenConfigLoader
{
    private const string ConfigFileName = "custom-tokens.json";
    private const string RadoubFolderName = "Radoub";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Regex CustomTagPattern = new(@"^<CUSTOM\d+>$", RegexOptions.Compiled);

    /// <summary>
    /// Get the default configuration file path.
    /// </summary>
    public static string GetDefaultConfigPath()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDir, RadoubFolderName, ConfigFileName);
    }

    /// <summary>
    /// Load configuration from a specific path.
    /// Returns empty config if file doesn't exist or is invalid.
    /// </summary>
    public static CustomTokenConfig Load(string path)
    {
        if (!File.Exists(path))
            return new CustomTokenConfig();

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return new CustomTokenConfig();

            var fileConfig = JsonSerializer.Deserialize<CustomTokenConfig>(json, JsonOptions);
            if (fileConfig == null)
                return new CustomTokenConfig();

            fileConfig.Tokens = ValidateTokens(fileConfig.Tokens);
            return fileConfig;
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.WARN, $"Failed to load custom token config: {ex.Message}", "CustomTokenConfigLoader", "Tokens");
            return new CustomTokenConfig();
        }
    }

    /// <summary>
    /// Load configuration from the default path, creating empty default if not exists.
    /// </summary>
    public static CustomTokenConfig LoadOrCreateDefault()
    {
        return LoadOrCreateDefault(GetDefaultConfigPath());
    }

    /// <summary>
    /// Load configuration from a specific path, creating empty default if not exists.
    /// </summary>
    public static CustomTokenConfig LoadOrCreateDefault(string path)
    {
        if (File.Exists(path))
            return Load(path);

        var config = new CustomTokenConfig();
        Save(config, path);
        return config;
    }

    /// <summary>
    /// Save configuration to a specific path.
    /// </summary>
    public static void Save(CustomTokenConfig config, string path)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, json);
    }

    private static List<CustomTokenDefinition> ValidateTokens(List<CustomTokenDefinition> tokens)
    {
        var validated = new List<CustomTokenDefinition>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token.Name))
            {
                UnifiedLogger.Log(LogLevel.WARN, "Skipping custom token with empty name", "CustomTokenConfigLoader", "Tokens");
                continue;
            }

            if (!seenNames.Add(token.Name))
            {
                UnifiedLogger.Log(LogLevel.WARN, $"Skipping duplicate custom token name: '{token.Name}'", "CustomTokenConfigLoader", "Tokens");
                continue;
            }

            if (!token.IsStandalone && !token.IsPaired)
            {
                UnifiedLogger.Log(LogLevel.WARN, $"Skipping custom token '{token.Name}' with unknown type: '{token.Type}'", "CustomTokenConfigLoader", "Tokens");
                continue;
            }

            if (token.IsStandalone)
            {
                if (string.IsNullOrWhiteSpace(token.Tag) || !IsValidCustomTag(token.Tag))
                {
                    UnifiedLogger.Log(LogLevel.WARN, $"Skipping standalone token '{token.Name}' with invalid tag: '{token.Tag}'", "CustomTokenConfigLoader", "Tokens");
                    continue;
                }
            }

            if (token.IsPaired)
            {
                if (string.IsNullOrWhiteSpace(token.OpenTag) || !IsValidCustomTag(token.OpenTag))
                {
                    UnifiedLogger.Log(LogLevel.WARN, $"Skipping paired token '{token.Name}' with invalid openTag: '{token.OpenTag}'", "CustomTokenConfigLoader", "Tokens");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(token.CloseTag) || !IsValidCustomTag(token.CloseTag))
                {
                    UnifiedLogger.Log(LogLevel.WARN, $"Skipping paired token '{token.Name}' with invalid closeTag: '{token.CloseTag}'", "CustomTokenConfigLoader", "Tokens");
                    continue;
                }
            }

            validated.Add(token);
        }

        return validated;
    }

    private static bool IsValidCustomTag(string tag)
    {
        return CustomTagPattern.IsMatch(tag);
    }
}
