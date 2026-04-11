using Radoub.Formats.Tokens;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for CustomTokenConfigLoader — user-defined custom (non-color) tokens.
/// Issue #1890: User-defined custom tokens in token chooser.
/// </summary>
public class CustomTokenConfigLoaderTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testConfigPath;

    public CustomTokenConfigLoaderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"RadoubTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _testConfigPath = Path.Combine(_testDir, "custom-tokens.json");
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
    public void Load_NonExistentFile_ReturnsEmptyConfig()
    {
        var result = CustomTokenConfigLoader.Load(Path.Combine(_testDir, "nonexistent.json"));

        Assert.NotNull(result);
        Assert.Empty(result.Tokens);
    }

    [Fact]
    public void Load_ValidStandaloneToken_ParsesCorrectly()
    {
        var json = """
            {
              "tokens": [
                { "name": "Faction Header", "type": "standalone", "tag": "<CUSTOM500>" }
              ]
            }
            """;
        File.WriteAllText(_testConfigPath, json);

        var result = CustomTokenConfigLoader.Load(_testConfigPath);

        Assert.Single(result.Tokens);
        var token = result.Tokens[0];
        Assert.Equal("Faction Header", token.Name);
        Assert.True(token.IsStandalone);
        Assert.Equal("<CUSTOM500>", token.Tag);
    }

    [Fact]
    public void Load_ValidPairedToken_ParsesCorrectly()
    {
        var json = """
            {
              "tokens": [
                { "name": "Italic", "type": "paired", "openTag": "<CUSTOM2001>", "closeTag": "<CUSTOM2000>" }
              ]
            }
            """;
        File.WriteAllText(_testConfigPath, json);

        var result = CustomTokenConfigLoader.Load(_testConfigPath);

        Assert.Single(result.Tokens);
        var token = result.Tokens[0];
        Assert.Equal("Italic", token.Name);
        Assert.True(token.IsPaired);
        Assert.Equal("<CUSTOM2001>", token.OpenTag);
        Assert.Equal("<CUSTOM2000>", token.CloseTag);
    }

    [Fact]
    public void Load_MixedTokenTypes_ParsesBoth()
    {
        var json = """
            {
              "tokens": [
                { "name": "Faction", "type": "standalone", "tag": "<CUSTOM500>" },
                { "name": "Bold", "type": "paired", "openTag": "<CUSTOM3001>", "closeTag": "<CUSTOM3000>" }
              ]
            }
            """;
        File.WriteAllText(_testConfigPath, json);

        var result = CustomTokenConfigLoader.Load(_testConfigPath);

        Assert.Equal(2, result.Tokens.Count);
        Assert.True(result.Tokens[0].IsStandalone);
        Assert.True(result.Tokens[1].IsPaired);
    }

    [Fact]
    public void Load_EmptyTokensArray_ReturnsEmptyConfig()
    {
        File.WriteAllText(_testConfigPath, """{ "tokens": [] }""");

        var result = CustomTokenConfigLoader.Load(_testConfigPath);

        Assert.NotNull(result);
        Assert.Empty(result.Tokens);
    }

    [Fact]
    public void Load_InvalidJson_ReturnsEmptyConfig()
    {
        File.WriteAllText(_testConfigPath, "{ not valid json }");

        var result = CustomTokenConfigLoader.Load(_testConfigPath);

        Assert.NotNull(result);
        Assert.Empty(result.Tokens);
    }

    [Fact]
    public void Load_EmptyFile_ReturnsEmptyConfig()
    {
        File.WriteAllText(_testConfigPath, "");

        var result = CustomTokenConfigLoader.Load(_testConfigPath);

        Assert.NotNull(result);
        Assert.Empty(result.Tokens);
    }

    [Fact]
    public void Load_EmptyObject_ReturnsEmptyConfig()
    {
        File.WriteAllText(_testConfigPath, "{}");

        var result = CustomTokenConfigLoader.Load(_testConfigPath);

        Assert.NotNull(result);
        Assert.Empty(result.Tokens);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Load_StandaloneWithoutTag_SkipsInvalidEntry()
    {
        var json = """
            {
              "tokens": [
                { "name": "Missing Tag", "type": "standalone" },
                { "name": "Valid", "type": "standalone", "tag": "<CUSTOM100>" }
              ]
            }
            """;
        File.WriteAllText(_testConfigPath, json);

        var result = CustomTokenConfigLoader.Load(_testConfigPath);

        Assert.Single(result.Tokens);
        Assert.Equal("Valid", result.Tokens[0].Name);
    }

    [Fact]
    public void Load_PairedWithoutOpenTag_SkipsInvalidEntry()
    {
        var json = """
            {
              "tokens": [
                { "name": "Missing Open", "type": "paired", "closeTag": "<CUSTOM2000>" },
                { "name": "Valid", "type": "standalone", "tag": "<CUSTOM100>" }
              ]
            }
            """;
        File.WriteAllText(_testConfigPath, json);

        var result = CustomTokenConfigLoader.Load(_testConfigPath);

        Assert.Single(result.Tokens);
        Assert.Equal("Valid", result.Tokens[0].Name);
    }

    [Fact]
    public void Load_PairedWithoutCloseTag_SkipsInvalidEntry()
    {
        var json = """
            {
              "tokens": [
                { "name": "Missing Close", "type": "paired", "openTag": "<CUSTOM2001>" },
                { "name": "Valid", "type": "standalone", "tag": "<CUSTOM100>" }
              ]
            }
            """;
        File.WriteAllText(_testConfigPath, json);

        var result = CustomTokenConfigLoader.Load(_testConfigPath);

        Assert.Single(result.Tokens);
        Assert.Equal("Valid", result.Tokens[0].Name);
    }

    [Fact]
    public void Load_InvalidTagSyntax_SkipsEntry()
    {
        var json = """
            {
              "tokens": [
                { "name": "Bad Tag", "type": "standalone", "tag": "NotACustomTag" },
                { "name": "Valid", "type": "standalone", "tag": "<CUSTOM100>" }
              ]
            }
            """;
        File.WriteAllText(_testConfigPath, json);

        var result = CustomTokenConfigLoader.Load(_testConfigPath);

        Assert.Single(result.Tokens);
        Assert.Equal("Valid", result.Tokens[0].Name);
    }

    [Fact]
    public void Load_InvalidTagSyntaxInPaired_SkipsEntry()
    {
        var json = """
            {
              "tokens": [
                { "name": "Bad Paired", "type": "paired", "openTag": "invalid", "closeTag": "<CUSTOM2000>" },
                { "name": "Valid", "type": "standalone", "tag": "<CUSTOM100>" }
              ]
            }
            """;
        File.WriteAllText(_testConfigPath, json);

        var result = CustomTokenConfigLoader.Load(_testConfigPath);

        Assert.Single(result.Tokens);
        Assert.Equal("Valid", result.Tokens[0].Name);
    }

    [Fact]
    public void Load_DuplicateNames_KeepsFirstOnly()
    {
        var json = """
            {
              "tokens": [
                { "name": "Faction", "type": "standalone", "tag": "<CUSTOM500>" },
                { "name": "Faction", "type": "standalone", "tag": "<CUSTOM501>" }
              ]
            }
            """;
        File.WriteAllText(_testConfigPath, json);

        var result = CustomTokenConfigLoader.Load(_testConfigPath);

        Assert.Single(result.Tokens);
        Assert.Equal("<CUSTOM500>", result.Tokens[0].Tag);
    }

    [Fact]
    public void Load_UnknownType_SkipsEntry()
    {
        var json = """
            {
              "tokens": [
                { "name": "Unknown", "type": "macro", "tag": "<CUSTOM100>" },
                { "name": "Valid", "type": "standalone", "tag": "<CUSTOM200>" }
              ]
            }
            """;
        File.WriteAllText(_testConfigPath, json);

        var result = CustomTokenConfigLoader.Load(_testConfigPath);

        Assert.Single(result.Tokens);
        Assert.Equal("Valid", result.Tokens[0].Name);
    }

    [Fact]
    public void Load_EmptyName_SkipsEntry()
    {
        var json = """
            {
              "tokens": [
                { "name": "", "type": "standalone", "tag": "<CUSTOM100>" },
                { "name": "Valid", "type": "standalone", "tag": "<CUSTOM200>" }
              ]
            }
            """;
        File.WriteAllText(_testConfigPath, json);

        var result = CustomTokenConfigLoader.Load(_testConfigPath);

        Assert.Single(result.Tokens);
        Assert.Equal("Valid", result.Tokens[0].Name);
    }

    #endregion

    #region Save and RoundTrip Tests

    [Fact]
    public void Save_WritesFileToPath()
    {
        var config = new CustomTokenConfig
        {
            Tokens = new List<CustomTokenDefinition>
            {
                new() { Name = "Test", Type = "standalone", Tag = "<CUSTOM100>" }
            }
        };

        CustomTokenConfigLoader.Save(config, _testConfigPath);

        Assert.True(File.Exists(_testConfigPath));
        var content = File.ReadAllText(_testConfigPath);
        Assert.Contains("Test", content);
        Assert.Contains("CUSTOM100", content);
    }

    [Fact]
    public void Save_CreatesDirectoryIfNeeded()
    {
        var nestedPath = Path.Combine(_testDir, "sub", "nested", "custom-tokens.json");
        var config = new CustomTokenConfig();

        CustomTokenConfigLoader.Save(config, nestedPath);

        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public void RoundTrip_StandaloneToken_PreservesAllFields()
    {
        var original = new CustomTokenConfig
        {
            Tokens = new List<CustomTokenDefinition>
            {
                new() { Name = "Faction Header", Type = "standalone", Tag = "<CUSTOM500>" }
            }
        };

        CustomTokenConfigLoader.Save(original, _testConfigPath);
        var loaded = CustomTokenConfigLoader.Load(_testConfigPath);

        Assert.Single(loaded.Tokens);
        Assert.Equal("Faction Header", loaded.Tokens[0].Name);
        Assert.Equal("standalone", loaded.Tokens[0].Type);
        Assert.Equal("<CUSTOM500>", loaded.Tokens[0].Tag);
    }

    [Fact]
    public void RoundTrip_PairedToken_PreservesAllFields()
    {
        var original = new CustomTokenConfig
        {
            Tokens = new List<CustomTokenDefinition>
            {
                new() { Name = "Italic", Type = "paired", OpenTag = "<CUSTOM2001>", CloseTag = "<CUSTOM2000>" }
            }
        };

        CustomTokenConfigLoader.Save(original, _testConfigPath);
        var loaded = CustomTokenConfigLoader.Load(_testConfigPath);

        Assert.Single(loaded.Tokens);
        Assert.Equal("Italic", loaded.Tokens[0].Name);
        Assert.Equal("paired", loaded.Tokens[0].Type);
        Assert.Equal("<CUSTOM2001>", loaded.Tokens[0].OpenTag);
        Assert.Equal("<CUSTOM2000>", loaded.Tokens[0].CloseTag);
    }

    [Fact]
    public void RoundTrip_MixedTokens_PreservesAll()
    {
        var original = new CustomTokenConfig
        {
            Tokens = new List<CustomTokenDefinition>
            {
                new() { Name = "Faction", Type = "standalone", Tag = "<CUSTOM500>" },
                new() { Name = "Bold", Type = "paired", OpenTag = "<CUSTOM3001>", CloseTag = "<CUSTOM3000>" },
                new() { Name = "Location", Type = "standalone", Tag = "<CUSTOM600>" }
            }
        };

        CustomTokenConfigLoader.Save(original, _testConfigPath);
        var loaded = CustomTokenConfigLoader.Load(_testConfigPath);

        Assert.Equal(3, loaded.Tokens.Count);
        Assert.Equal("Faction", loaded.Tokens[0].Name);
        Assert.Equal("Bold", loaded.Tokens[1].Name);
        Assert.Equal("Location", loaded.Tokens[2].Name);
    }

    #endregion

    #region LoadOrCreateDefault Tests

    [Fact]
    public void LoadOrCreateDefault_WhenFileNotExists_CreatesEmptyConfig()
    {
        var path = Path.Combine(_testDir, "newdir", "custom-tokens.json");

        var config = CustomTokenConfigLoader.LoadOrCreateDefault(path);

        Assert.NotNull(config);
        Assert.Empty(config.Tokens);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void LoadOrCreateDefault_WhenFileExists_ReturnsExisting()
    {
        var json = """
            {
              "tokens": [
                { "name": "Test", "type": "standalone", "tag": "<CUSTOM100>" }
              ]
            }
            """;
        File.WriteAllText(_testConfigPath, json);

        var config = CustomTokenConfigLoader.LoadOrCreateDefault(_testConfigPath);

        Assert.Single(config.Tokens);
        Assert.Equal("Test", config.Tokens[0].Name);
    }

    #endregion

    #region GetDefaultConfigPath Tests

    [Fact]
    public void GetDefaultConfigPath_ContainsRadoubFolder()
    {
        var path = CustomTokenConfigLoader.GetDefaultConfigPath();

        Assert.Contains("Radoub", path);
        Assert.EndsWith("custom-tokens.json", path);
    }

    [Fact]
    public void GetDefaultConfigPath_IsAbsolutePath()
    {
        var path = CustomTokenConfigLoader.GetDefaultConfigPath();

        Assert.True(Path.IsPathRooted(path));
    }

    #endregion

    #region CustomTokenDefinition Model Tests

    [Fact]
    public void IsStandalone_WhenTypeIsStandalone_ReturnsTrue()
    {
        var token = new CustomTokenDefinition { Type = "standalone" };
        Assert.True(token.IsStandalone);
        Assert.False(token.IsPaired);
    }

    [Fact]
    public void IsPaired_WhenTypeIsPaired_ReturnsTrue()
    {
        var token = new CustomTokenDefinition { Type = "paired" };
        Assert.True(token.IsPaired);
        Assert.False(token.IsStandalone);
    }

    [Fact]
    public void IsStandalone_CaseInsensitive()
    {
        var token = new CustomTokenDefinition { Type = "Standalone" };
        Assert.True(token.IsStandalone);
    }

    [Fact]
    public void IsPaired_CaseInsensitive()
    {
        var token = new CustomTokenDefinition { Type = "Paired" };
        Assert.True(token.IsPaired);
    }

    #endregion
}
