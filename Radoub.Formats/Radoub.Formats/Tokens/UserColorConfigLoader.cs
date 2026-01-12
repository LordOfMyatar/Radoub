using System.Text.Json;
using System.Text.Json.Serialization;

namespace Radoub.Formats.Tokens;

/// <summary>
/// Loads and saves user-defined color token configurations.
/// Configuration file location: ~/Radoub/token-colors.json
/// </summary>
public static class UserColorConfigLoader
{
    private const string ConfigFileName = "token-colors.json";
    private const string RadoubFolderName = "Radoub";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Get the default configuration file path.
    /// </summary>
    public static string GetDefaultConfigPath()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDir, RadoubFolderName, ConfigFileName);
    }

    /// <summary>
    /// Load configuration from the default path.
    /// Returns null if file doesn't exist.
    /// </summary>
    public static UserColorConfig? Load()
    {
        return Load(GetDefaultConfigPath());
    }

    /// <summary>
    /// Load configuration from a specific path.
    /// Returns null if file doesn't exist or is invalid.
    /// </summary>
    public static UserColorConfig? Load(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            var fileConfig = JsonSerializer.Deserialize<UserColorConfigFile>(json, JsonOptions);

            if (fileConfig == null)
                return null;

            return ConvertFromFile(fileConfig);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Load configuration, creating default if not exists.
    /// </summary>
    public static UserColorConfig LoadOrCreateDefault()
    {
        var config = Load();
        if (config != null)
            return config;

        config = UserColorConfig.CreateDefault();
        Save(config);
        return config;
    }

    /// <summary>
    /// Save configuration to the default path.
    /// </summary>
    public static void Save(UserColorConfig config)
    {
        Save(config, GetDefaultConfigPath());
    }

    /// <summary>
    /// Save configuration to a specific path.
    /// </summary>
    public static void Save(UserColorConfig config, string path)
    {
        var fileConfig = ConvertToFile(config);
        var json = JsonSerializer.Serialize(fileConfig, JsonOptions);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Check if a configuration file exists.
    /// </summary>
    public static bool ConfigExists()
    {
        return File.Exists(GetDefaultConfigPath());
    }

    private static UserColorConfig ConvertFromFile(UserColorConfigFile file)
    {
        var config = new UserColorConfig
        {
            CloseToken = file.CloseToken ?? ""
        };

        if (file.Colors != null)
        {
            foreach (var color in file.Colors)
            {
                config.Colors[color.Name] = color.Token;
                config.ColorHexValues[color.Name] = color.HexColor;
            }
        }

        return config;
    }

    private static UserColorConfigFile ConvertToFile(UserColorConfig config)
    {
        var colors = new List<ColorDefinition>();

        foreach (var kvp in config.Colors)
        {
            colors.Add(new ColorDefinition
            {
                Name = kvp.Key,
                Token = kvp.Value,
                HexColor = config.ColorHexValues.GetValueOrDefault(kvp.Key, "#FFFFFF")
            });
        }

        return new UserColorConfigFile
        {
            CloseToken = config.CloseToken,
            Colors = colors,
            Description = "User-defined color tokens for NWN dialog/journal text. " +
                          "Token numbers must match SetCustomToken() calls in module scripts."
        };
    }
}

/// <summary>
/// JSON file format for user color configuration.
/// </summary>
internal class UserColorConfigFile
{
    /// <summary>
    /// Human-readable description of the file purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The close token that ends colored text.
    /// </summary>
    public string? CloseToken { get; set; }

    /// <summary>
    /// List of color definitions.
    /// </summary>
    public List<ColorDefinition>? Colors { get; set; }
}

/// <summary>
/// A single color definition in the config file.
/// </summary>
internal class ColorDefinition
{
    /// <summary>
    /// Display name for the color (e.g., "Red", "Gold").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The CUSTOM token to insert (e.g., "&lt;CUSTOM1001&gt;").
    /// </summary>
    public string Token { get; set; } = "";

    /// <summary>
    /// Hex color for display preview (e.g., "#FF0000").
    /// </summary>
    public string HexColor { get; set; } = "#FFFFFF";
}
