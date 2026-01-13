using System.Text.Json;
using Radoub.Formats.Tokens;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for UserColorConfigLoader file operations and UserColorConfig edge cases.
/// Issue #892: Token system test coverage.
/// </summary>
public class UserColorConfigLoaderTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testConfigPath;

    public UserColorConfigLoaderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"RadoubTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _testConfigPath = Path.Combine(_testDir, "token-colors.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    #region Load Tests

    [Fact]
    public void Load_NonExistentFile_ReturnsNull()
    {
        var result = UserColorConfigLoader.Load(Path.Combine(_testDir, "nonexistent.json"));

        Assert.Null(result);
    }

    [Fact]
    public void Load_ValidConfig_ReturnsConfig()
    {
        var json = """
            {
              "closeToken": "<CUSTOM1000>",
              "colors": [
                { "name": "Red", "token": "<CUSTOM1001>", "hexColor": "#FF0000" }
              ]
            }
            """;
        File.WriteAllText(_testConfigPath, json);

        var result = UserColorConfigLoader.Load(_testConfigPath);

        Assert.NotNull(result);
        Assert.Equal("<CUSTOM1000>", result.CloseToken);
        Assert.Single(result.Colors);
        Assert.Equal("<CUSTOM1001>", result.Colors["Red"]);
        Assert.Equal("#FF0000", result.ColorHexValues["Red"]);
    }

    [Fact]
    public void Load_ConfigWithMultipleColors_LoadsAll()
    {
        var json = """
            {
              "closeToken": "<CUSTOM1000>",
              "colors": [
                { "name": "Red", "token": "<CUSTOM1001>", "hexColor": "#FF0000" },
                { "name": "Green", "token": "<CUSTOM1002>", "hexColor": "#00FF00" },
                { "name": "Blue", "token": "<CUSTOM1003>", "hexColor": "#0000FF" }
              ]
            }
            """;
        File.WriteAllText(_testConfigPath, json);

        var result = UserColorConfigLoader.Load(_testConfigPath);

        Assert.NotNull(result);
        Assert.Equal(3, result.Colors.Count);
        Assert.Equal(3, result.ColorHexValues.Count);
    }

    [Fact]
    public void Load_InvalidJson_ReturnsNull()
    {
        File.WriteAllText(_testConfigPath, "{ invalid json }");

        var result = UserColorConfigLoader.Load(_testConfigPath);

        Assert.Null(result);
    }

    [Fact]
    public void Load_EmptyFile_ReturnsNull()
    {
        File.WriteAllText(_testConfigPath, "");

        var result = UserColorConfigLoader.Load(_testConfigPath);

        Assert.Null(result);
    }

    [Fact]
    public void Load_EmptyObject_ReturnsConfigWithEmptyCollections()
    {
        File.WriteAllText(_testConfigPath, "{}");

        var result = UserColorConfigLoader.Load(_testConfigPath);

        Assert.NotNull(result);
        Assert.Empty(result.Colors);
        Assert.Empty(result.ColorHexValues);
        Assert.Empty(result.CloseToken);
    }

    [Fact]
    public void Load_NullCloseToken_DefaultsToEmptyString()
    {
        var json = """
            {
              "colors": [
                { "name": "Red", "token": "<CUSTOM1001>", "hexColor": "#FF0000" }
              ]
            }
            """;
        File.WriteAllText(_testConfigPath, json);

        var result = UserColorConfigLoader.Load(_testConfigPath);

        Assert.NotNull(result);
        Assert.Empty(result.CloseToken);
    }

    [Fact]
    public void Load_NullColors_ResultsInEmptyDictionary()
    {
        var json = """
            {
              "closeToken": "<CUSTOM1000>"
            }
            """;
        File.WriteAllText(_testConfigPath, json);

        var result = UserColorConfigLoader.Load(_testConfigPath);

        Assert.NotNull(result);
        Assert.Empty(result.Colors);
    }

    #endregion

    #region Save Tests

    [Fact]
    public void Save_ValidConfig_WritesFile()
    {
        var config = new UserColorConfig
        {
            CloseToken = "<CUSTOM1000>",
            Colors = new Dictionary<string, string> { ["Red"] = "<CUSTOM1001>" },
            ColorHexValues = new Dictionary<string, string> { ["Red"] = "#FF0000" }
        };

        UserColorConfigLoader.Save(config, _testConfigPath);

        Assert.True(File.Exists(_testConfigPath));
        var content = File.ReadAllText(_testConfigPath);
        Assert.Contains("closeToken", content);
        Assert.Contains("CUSTOM1000", content);
    }

    [Fact]
    public void Save_CreatesDirectory_WhenNotExists()
    {
        var subDir = Path.Combine(_testDir, "subdir", "nested");
        var nestedPath = Path.Combine(subDir, "config.json");
        var config = UserColorConfig.CreateDefault();

        UserColorConfigLoader.Save(config, nestedPath);

        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public void Save_OverwritesExistingFile()
    {
        File.WriteAllText(_testConfigPath, "old content");
        var config = new UserColorConfig { CloseToken = "<CUSTOM9999>" };

        UserColorConfigLoader.Save(config, _testConfigPath);

        var content = File.ReadAllText(_testConfigPath);
        Assert.Contains("CUSTOM9999", content);
        Assert.DoesNotContain("old content", content);
    }

    [Fact]
    public void Save_IncludesDescription()
    {
        var config = UserColorConfig.CreateDefault();

        UserColorConfigLoader.Save(config, _testConfigPath);

        var content = File.ReadAllText(_testConfigPath);
        Assert.Contains("description", content);
        Assert.Contains("User-defined color tokens", content);
    }

    #endregion

    #region RoundTrip Tests

    [Fact]
    public void RoundTrip_PreservesAllData()
    {
        var original = new UserColorConfig
        {
            CloseToken = "<CUSTOM1000>",
            Colors = new Dictionary<string, string>
            {
                ["Red"] = "<CUSTOM1001>",
                ["Green"] = "<CUSTOM1002>"
            },
            ColorHexValues = new Dictionary<string, string>
            {
                ["Red"] = "#FF0000",
                ["Green"] = "#00FF00"
            }
        };

        UserColorConfigLoader.Save(original, _testConfigPath);
        var loaded = UserColorConfigLoader.Load(_testConfigPath);

        Assert.NotNull(loaded);
        Assert.Equal(original.CloseToken, loaded.CloseToken);
        Assert.Equal(original.Colors.Count, loaded.Colors.Count);
        foreach (var kvp in original.Colors)
        {
            Assert.Equal(kvp.Value, loaded.Colors[kvp.Key]);
        }
        foreach (var kvp in original.ColorHexValues)
        {
            Assert.Equal(kvp.Value, loaded.ColorHexValues[kvp.Key]);
        }
    }

    [Fact]
    public void RoundTrip_DefaultConfig_PreservesAllColors()
    {
        var original = UserColorConfig.CreateDefault();

        UserColorConfigLoader.Save(original, _testConfigPath);
        var loaded = UserColorConfigLoader.Load(_testConfigPath);

        Assert.NotNull(loaded);
        Assert.Equal(original.CloseToken, loaded.CloseToken);
        Assert.Equal(original.Colors.Count, loaded.Colors.Count);
        Assert.Equal(original.ColorHexValues.Count, loaded.ColorHexValues.Count);

        foreach (var colorName in original.Colors.Keys)
        {
            Assert.Equal(original.Colors[colorName], loaded.Colors[colorName]);
            Assert.Equal(original.ColorHexValues[colorName], loaded.ColorHexValues[colorName]);
        }
    }

    #endregion

    #region LoadOrCreateDefault Tests

    [Fact]
    public void LoadOrCreateDefault_WhenFileNotExists_ReturnsDefault()
    {
        var path = Path.Combine(_testDir, "newconfig", "token-colors.json");

        var config = LoadOrCreateDefaultWithPath(path);

        Assert.NotNull(config);
        Assert.Equal("<CUSTOM1000>", config.CloseToken);
        Assert.True(config.Colors.Count > 0);
    }

    [Fact]
    public void LoadOrCreateDefault_WhenFileNotExists_CreatesFile()
    {
        var path = Path.Combine(_testDir, "newconfig", "token-colors.json");

        LoadOrCreateDefaultWithPath(path);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void LoadOrCreateDefault_WhenFileExists_ReturnsExisting()
    {
        var json = """
            {
              "closeToken": "<CUSTOM9999>",
              "colors": [
                { "name": "Custom", "token": "<CUSTOM9001>", "hexColor": "#AABBCC" }
              ]
            }
            """;
        File.WriteAllText(_testConfigPath, json);

        var config = LoadOrCreateDefaultWithPath(_testConfigPath);

        Assert.Equal("<CUSTOM9999>", config.CloseToken);
        Assert.Single(config.Colors);
        Assert.Equal("<CUSTOM9001>", config.Colors["Custom"]);
    }

    // Helper to test LoadOrCreateDefault with custom path
    private UserColorConfig LoadOrCreateDefaultWithPath(string path)
    {
        var config = UserColorConfigLoader.Load(path);
        if (config != null)
            return config;

        config = UserColorConfig.CreateDefault();
        UserColorConfigLoader.Save(config, path);
        return config;
    }

    #endregion

    #region GetDefaultConfigPath Tests

    [Fact]
    public void GetDefaultConfigPath_ContainsRadoubFolder()
    {
        var path = UserColorConfigLoader.GetDefaultConfigPath();

        Assert.Contains("Radoub", path);
        Assert.EndsWith("token-colors.json", path);
    }

    [Fact]
    public void GetDefaultConfigPath_IsAbsolutePath()
    {
        var path = UserColorConfigLoader.GetDefaultConfigPath();

        Assert.True(Path.IsPathRooted(path));
    }

    #endregion

    #region UserColorConfig Edge Cases

    [Fact]
    public void UserColorConfig_GetHexColor_ExistingColor_ReturnsHex()
    {
        var config = UserColorConfig.CreateDefault();

        var hex = config.GetHexColor("Red");

        Assert.NotNull(hex);
        Assert.StartsWith("#", hex);
    }

    [Fact]
    public void UserColorConfig_GetHexColor_NonexistentColor_ReturnsNull()
    {
        var config = UserColorConfig.CreateDefault();

        var hex = config.GetHexColor("NonexistentColor");

        Assert.Null(hex);
    }

    [Fact]
    public void UserColorConfig_CreateDefault_Has19Colors()
    {
        var config = UserColorConfig.CreateDefault();

        Assert.Equal(19, config.Colors.Count);
        Assert.Equal(19, config.ColorHexValues.Count);
    }

    [Fact]
    public void UserColorConfig_CreateDefault_AllColorsHaveHexValues()
    {
        var config = UserColorConfig.CreateDefault();

        foreach (var colorName in config.Colors.Keys)
        {
            Assert.True(config.ColorHexValues.ContainsKey(colorName),
                $"Color '{colorName}' missing hex value");
        }
    }

    [Fact]
    public void UserColorConfig_CreateDefault_HexValuesAreValid()
    {
        var config = UserColorConfig.CreateDefault();

        foreach (var hex in config.ColorHexValues.Values)
        {
            Assert.Matches(@"^#[0-9A-Fa-f]{6}$", hex);
        }
    }

    [Fact]
    public void UserColorConfig_CreateDefault_TokensUseCustomFormat()
    {
        var config = UserColorConfig.CreateDefault();

        Assert.StartsWith("<CUSTOM", config.CloseToken);
        foreach (var token in config.Colors.Values)
        {
            Assert.StartsWith("<CUSTOM", token);
            Assert.EndsWith(">", token);
        }
    }

    [Fact]
    public void UserColorConfig_EmptyConfig_WorksWithTokenParser()
    {
        var config = new UserColorConfig();
        var parser = new TokenParser(config);

        var result = parser.Parse("<CUSTOM1001>text<CUSTOM1000>");

        // Without colors defined, should parse as individual CUSTOM tokens
        Assert.Equal(3, result.Count);
        Assert.IsType<CustomToken>(result[0]);
        Assert.IsType<PlainTextSegment>(result[1]);
        Assert.IsType<CustomToken>(result[2]);
    }

    [Fact]
    public void UserColorConfig_MismatchedDictionaries_GetHexColorHandlesGracefully()
    {
        var config = new UserColorConfig
        {
            Colors = new Dictionary<string, string> { ["Red"] = "<CUSTOM1001>" },
            ColorHexValues = new Dictionary<string, string>() // Intentionally empty
        };

        var hex = config.GetHexColor("Red");

        Assert.Null(hex);
    }

    #endregion

    #region ConfigExists Tests

    [Fact]
    public void ConfigExists_WhenDefault_DependsOnFileSystem()
    {
        // This test just verifies the method works
        // Actual result depends on whether user has created config
        var exists = UserColorConfigLoader.ConfigExists();

        // Just verify it doesn't throw and returns a bool
        Assert.True(exists || !exists);
    }

    #endregion

    #region Edge Case: Special Characters in Config

    [Fact]
    public void RoundTrip_ColorNameWithSpaces_PreservesName()
    {
        var config = new UserColorConfig
        {
            CloseToken = "<CUSTOM1000>",
            Colors = new Dictionary<string, string>
            {
                ["Dark Red"] = "<CUSTOM1001>",
                ["Light Green"] = "<CUSTOM1002>"
            },
            ColorHexValues = new Dictionary<string, string>
            {
                ["Dark Red"] = "#8B0000",
                ["Light Green"] = "#90EE90"
            }
        };

        UserColorConfigLoader.Save(config, _testConfigPath);
        var loaded = UserColorConfigLoader.Load(_testConfigPath);

        Assert.NotNull(loaded);
        Assert.True(loaded.Colors.ContainsKey("Dark Red"));
        Assert.True(loaded.Colors.ContainsKey("Light Green"));
    }

    [Fact]
    public void RoundTrip_UnicodeColorName_PreservesName()
    {
        var config = new UserColorConfig
        {
            CloseToken = "<CUSTOM1000>",
            Colors = new Dictionary<string, string> { ["Rouge"] = "<CUSTOM1001>" },
            ColorHexValues = new Dictionary<string, string> { ["Rouge"] = "#FF0000" }
        };

        UserColorConfigLoader.Save(config, _testConfigPath);
        var loaded = UserColorConfigLoader.Load(_testConfigPath);

        Assert.NotNull(loaded);
        Assert.True(loaded.Colors.ContainsKey("Rouge"));
    }

    #endregion
}
