using Radoub.Formats.Ifo;
using Xunit;

namespace Radoub.Formats.Tests;

public class IfoVersionRequirementsTests
{
    #region GameVersionComparer Tests

    [Theory]
    [InlineData("1.69", "1.69", 0)]
    [InlineData("1.69", "1.74", -1)]
    [InlineData("1.74", "1.69", 1)]
    [InlineData("1.80", "1.79", 1)]
    [InlineData("1.87.8193.35", "1.87", 1)]
    [InlineData("1.87", "1.87.8193.35", -1)]
    [InlineData("1.85.8193.30", "1.85.8193.31", -1)]
    public void GameVersionComparer_Compare_ReturnsCorrectOrder(string v1, string v2, int expected)
    {
        var result = GameVersionComparer.Instance.Compare(v1, v2);
        Assert.Equal(expected, Math.Sign(result));
    }

    [Theory]
    [InlineData("1.80", "1.80", true)]
    [InlineData("1.85", "1.80", true)]
    [InlineData("1.74", "1.80", false)]
    [InlineData("1.69", "1.69", true)]
    [InlineData("1.87.8193.35", "1.87", true)]
    public void GameVersionComparer_Supports_ReturnsCorrectResult(string moduleVersion, string requiredVersion, bool expected)
    {
        var result = GameVersionComparer.Supports(moduleVersion, requiredVersion);
        Assert.Equal(expected, result);
    }

    #endregion

    #region GetPopulatedEeFields Tests

    [Fact]
    public void GetPopulatedEeFields_EmptyIfo_ReturnsEmpty()
    {
        var ifo = new IfoFile();
        var result = IfoVersionRequirements.GetPopulatedEeFields(ifo);
        Assert.Empty(result);
    }

    [Fact]
    public void GetPopulatedEeFields_WithOnlyBaseFields_ReturnsEmpty()
    {
        var ifo = new IfoFile
        {
            OnModuleLoad = "mod_load",
            OnClientEnter = "client_enter",
            OnHeartbeat = "heartbeat"
        };
        var result = IfoVersionRequirements.GetPopulatedEeFields(ifo);
        Assert.Empty(result);
    }

    [Fact]
    public void GetPopulatedEeFields_WithEeScripts_ReturnsCorrectFields()
    {
        var ifo = new IfoFile
        {
            OnModuleStart = "mod_start",
            OnPlayerChat = "chat_handler",
            OnNuiEvent = "nui_handler"
        };

        var result = IfoVersionRequirements.GetPopulatedEeFields(ifo);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, f => f.FieldName == "Mod_OnModStart" && f.MinVersion == "1.74");
        Assert.Contains(result, f => f.FieldName == "Mod_OnPlrChat" && f.MinVersion == "1.74");
        Assert.Contains(result, f => f.FieldName == "Mod_OnNuiEvent" && f.MinVersion == "1.85");
    }

    [Fact]
    public void GetPopulatedEeFields_WithDefaultBic_ReturnsField()
    {
        var ifo = new IfoFile
        {
            DefaultBic = "my_character"
        };

        var result = IfoVersionRequirements.GetPopulatedEeFields(ifo);

        Assert.Single(result);
        Assert.Equal("Mod_DefaultBic", result[0].FieldName);
        Assert.Equal("1.87", result[0].MinVersion);
        Assert.Equal("my_character", result[0].Value);
    }

    [Fact]
    public void GetPopulatedEeFields_WithModuleUuid_ReturnsField()
    {
        var ifo = new IfoFile
        {
            ModuleUuid = "12345-abcde-67890"
        };

        var result = IfoVersionRequirements.GetPopulatedEeFields(ifo);

        Assert.Single(result);
        Assert.Equal("Mod_UUID", result[0].FieldName);
        Assert.Equal("1.77", result[0].MinVersion);
    }

    [Fact]
    public void GetPopulatedEeFields_PartyControlZero_NotReturned()
    {
        var ifo = new IfoFile
        {
            PartyControl = 0  // Default value, should not be flagged
        };

        var result = IfoVersionRequirements.GetPopulatedEeFields(ifo);
        Assert.Empty(result);
    }

    [Fact]
    public void GetPopulatedEeFields_PartyControlNonZero_Returned()
    {
        var ifo = new IfoFile
        {
            PartyControl = 1
        };

        var result = IfoVersionRequirements.GetPopulatedEeFields(ifo);

        Assert.Single(result);
        Assert.Equal("Mod_PartyControl", result[0].FieldName);
        Assert.Equal("1.87", result[0].MinVersion);
    }

    #endregion

    #region GetRequiredVersion Tests

    [Fact]
    public void GetRequiredVersion_EmptyIfo_Returns169()
    {
        var ifo = new IfoFile();
        var result = IfoVersionRequirements.GetRequiredVersion(ifo);
        Assert.Equal("1.69", result);
    }

    [Fact]
    public void GetRequiredVersion_WithEe174Fields_Returns174()
    {
        var ifo = new IfoFile
        {
            OnModuleStart = "mod_start"
        };

        var result = IfoVersionRequirements.GetRequiredVersion(ifo);
        Assert.Equal("1.74", result);
    }

    [Fact]
    public void GetRequiredVersion_WithMixedEeFields_ReturnsHighest()
    {
        var ifo = new IfoFile
        {
            OnModuleStart = "mod_start",  // 1.74
            OnPlayerTarget = "target",     // 1.80
            OnNuiEvent = "nui",            // 1.85
            DefaultBic = "char"            // 1.87
        };

        var result = IfoVersionRequirements.GetRequiredVersion(ifo);
        Assert.Equal("1.87", result);
    }

    [Fact]
    public void GetRequiredVersion_With185Fields_Returns185()
    {
        var ifo = new IfoFile
        {
            OnPlayerGuiEvent = "gui_event",
            OnPlayerTileAction = "tile_action"
        };

        var result = IfoVersionRequirements.GetRequiredVersion(ifo);
        Assert.Equal("1.85", result);
    }

    #endregion

    #region GetIncompatibleFields Tests

    [Fact]
    public void GetIncompatibleFields_TargetMatchesRequired_ReturnsEmpty()
    {
        var ifo = new IfoFile
        {
            OnNuiEvent = "nui_handler"  // Requires 1.85
        };

        var result = IfoVersionRequirements.GetIncompatibleFields(ifo, "1.85");
        Assert.Empty(result);
    }

    [Fact]
    public void GetIncompatibleFields_TargetHigherThanRequired_ReturnsEmpty()
    {
        var ifo = new IfoFile
        {
            OnModuleStart = "mod_start"  // Requires 1.74
        };

        var result = IfoVersionRequirements.GetIncompatibleFields(ifo, "1.80");
        Assert.Empty(result);
    }

    [Fact]
    public void GetIncompatibleFields_TargetLowerThanRequired_ReturnsFields()
    {
        var ifo = new IfoFile
        {
            OnNuiEvent = "nui_handler",  // Requires 1.85
            DefaultBic = "my_char"        // Requires 1.87
        };

        var result = IfoVersionRequirements.GetIncompatibleFields(ifo, "1.80");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, f => f.FieldName == "Mod_OnNuiEvent");
        Assert.Contains(result, f => f.FieldName == "Mod_DefaultBic");
    }

    [Fact]
    public void GetIncompatibleFields_Target169_ReturnsAllEeFields()
    {
        var ifo = new IfoFile
        {
            OnModuleStart = "start",
            OnPlayerChat = "chat",
            ModuleUuid = "uuid",
            OnPlayerTarget = "target",
            OnNuiEvent = "nui"
        };

        var result = IfoVersionRequirements.GetIncompatibleFields(ifo, "1.69");

        Assert.Equal(5, result.Count);
    }

    #endregion

    #region FieldMinVersions Mapping Tests

    [Theory]
    [InlineData("Mod_OnModStart", "1.74")]
    [InlineData("Mod_OnPlrChat", "1.74")]
    [InlineData("Mod_UUID", "1.77")]
    [InlineData("Mod_OnPlrTarget", "1.80")]
    [InlineData("Mod_OnPlrGuiEvt", "1.85")]
    [InlineData("Mod_OnPlrTileAct", "1.85")]
    [InlineData("Mod_OnNuiEvent", "1.85")]
    [InlineData("Mod_DefaultBic", "1.87")]
    [InlineData("Mod_PartyControl", "1.87")]
    public void FieldMinVersions_ContainsExpectedMappings(string fieldName, string expectedVersion)
    {
        Assert.True(IfoVersionRequirements.FieldMinVersions.ContainsKey(fieldName));
        Assert.Equal(expectedVersion, IfoVersionRequirements.FieldMinVersions[fieldName]);
    }

    [Fact]
    public void FieldMinVersions_DoesNotContainBaseFields()
    {
        // Base 1.69 fields should NOT be in the mapping
        Assert.False(IfoVersionRequirements.FieldMinVersions.ContainsKey("Mod_OnModLoad"));
        Assert.False(IfoVersionRequirements.FieldMinVersions.ContainsKey("Mod_OnClientEntr"));
        Assert.False(IfoVersionRequirements.FieldMinVersions.ContainsKey("Mod_OnHeartbeat"));
        Assert.False(IfoVersionRequirements.FieldMinVersions.ContainsKey("Mod_MinGameVer"));
    }

    #endregion
}
