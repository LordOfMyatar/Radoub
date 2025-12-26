using Radoub.Formats.Common;
using Radoub.Formats.Resolver;
using Radoub.Formats.Tlk;
using Radoub.Formats.TwoDA;
using Radoub.Formats.Uti;
using Xunit;

namespace Radoub.Formats.Tests;

public class ItemPropertyResolverTests
{
    [Fact]
    public void ResolvedItemProperty_Format_PropertyNameOnly()
    {
        var resolved = new ResolvedItemProperty
        {
            PropertyName = "Enhanced"
        };

        Assert.Equal("Enhanced", resolved.Format());
    }

    [Fact]
    public void ResolvedItemProperty_Format_WithSubtype()
    {
        var resolved = new ResolvedItemProperty
        {
            PropertyName = "Bonus Feat:",
            SubtypeName = "Alertness"
        };

        Assert.Equal("Bonus Feat Alertness", resolved.Format());
    }

    [Fact]
    public void ResolvedItemProperty_Format_WithCostValue()
    {
        var resolved = new ResolvedItemProperty
        {
            PropertyName = "Enhancement Bonus:",
            CostValueName = "+3"
        };

        Assert.Equal("Enhancement Bonus +3", resolved.Format());
    }

    [Fact]
    public void ResolvedItemProperty_Format_WithSubtypeAndCost()
    {
        var resolved = new ResolvedItemProperty
        {
            PropertyName = "Damage Bonus:",
            SubtypeName = "Fire",
            CostValueName = "1d6"
        };

        Assert.Equal("Damage Bonus Fire 1d6", resolved.Format());
    }

    [Fact]
    public void ResolvedItemProperty_Format_WithAllFields()
    {
        var resolved = new ResolvedItemProperty
        {
            PropertyName = "Cast Spell:",
            SubtypeName = "Fireball",
            CostValueName = "5 Charges/Use",
            ParamValueName = "Caster Level 5"
        };

        Assert.Equal("Cast Spell Fireball 5 Charges/Use Caster Level 5", resolved.Format());
    }

    [Fact]
    public void ResolvedItemProperty_Format_TrimsTrailingColon()
    {
        var resolved = new ResolvedItemProperty
        {
            PropertyName = "Enhancement Bonus:",
            CostValueName = "+1"
        };

        // Should trim the colon
        Assert.Equal("Enhancement Bonus +1", resolved.Format());
    }

    [Fact]
    public void ResolvedItemProperty_ToString_SameAsFormat()
    {
        var resolved = new ResolvedItemProperty
        {
            PropertyName = "Test Property",
            SubtypeName = "Test Subtype"
        };

        Assert.Equal(resolved.Format(), resolved.ToString());
    }

    [Fact]
    public void ItemProperty_DefaultValues()
    {
        var property = new ItemProperty();

        Assert.Equal(0, property.PropertyName);
        Assert.Equal(0, property.Subtype);
        Assert.Equal(0, property.CostTable);
        Assert.Equal(0, property.CostValue);
        Assert.Equal(0xFF, property.Param1);
        Assert.Equal(0, property.Param1Value);
    }

    // Tests that require a mock resolver would go here
    // For full integration testing, we'd need actual game 2DA files

    [Fact]
    public void ResolvedItemProperty_Indices_StoredCorrectly()
    {
        var resolved = new ResolvedItemProperty
        {
            PropertyIndex = 15,
            SubtypeIndex = 3,
            CostTableIndex = 2,
            CostValueIndex = 5,
            ParamTableIndex = 1,
            ParamValueIndex = 7
        };

        Assert.Equal(15, resolved.PropertyIndex);
        Assert.Equal(3, resolved.SubtypeIndex);
        Assert.Equal(2, resolved.CostTableIndex);
        Assert.Equal(5, resolved.CostValueIndex);
        Assert.Equal(1, resolved.ParamTableIndex);
        Assert.Equal(7, resolved.ParamValueIndex);
    }

    [Fact]
    public void ResolvedItemProperty_SubtypeTableResRef_Stored()
    {
        var resolved = new ResolvedItemProperty
        {
            SubtypeTableResRef = "iprp_feats"
        };

        Assert.Equal("iprp_feats", resolved.SubtypeTableResRef);
    }
}

/// <summary>
/// Integration tests that require NWN game data.
/// These are skipped if game data is not available.
/// </summary>
public class ItemPropertyResolverIntegrationTests
{
    private static readonly string? NwnDataPath = Environment.GetEnvironmentVariable("NWN_DATA");

    [Fact]
    public void Resolve_WithGameData_ResolvesEnhancementBonus()
    {
        if (string.IsNullOrEmpty(NwnDataPath)) return; // Skip if no game data

        var config = new GameResourceConfig { GameDataPath = NwnDataPath };
        using var resolver = new GameResourceResolver(config);

        // Try to load base TLK
        var tlkPath = Path.Combine(NwnDataPath, "dialog.tlk");
        TlkFile? tlk = null;
        if (File.Exists(tlkPath))
            tlk = TlkReader.Read(tlkPath);

        using var propResolver = new ItemPropertyResolver(resolver, tlk);

        // Enhancement Bonus +1 (PropertyName=6, CostValue for +1)
        var property = new ItemProperty
        {
            PropertyName = 6,  // Enhancement Bonus in itempropdef.2da
            CostTable = 2,     // iprp_bonuscost
            CostValue = 1      // +1
        };

        var result = propResolver.Resolve(property);

        // Should contain "Enhancement" and some value indicator
        Assert.Contains("Enhancement", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveDetailed_WithGameData_IncludesAllInfo()
    {
        if (string.IsNullOrEmpty(NwnDataPath)) return;

        var config = new GameResourceConfig { GameDataPath = NwnDataPath };
        using var resolver = new GameResourceResolver(config);

        var tlkPath = Path.Combine(NwnDataPath, "dialog.tlk");
        TlkFile? tlk = null;
        if (File.Exists(tlkPath))
            tlk = TlkReader.Read(tlkPath);

        using var propResolver = new ItemPropertyResolver(resolver, tlk);

        var property = new ItemProperty
        {
            PropertyName = 6,
            CostTable = 2,
            CostValue = 1
        };

        var result = propResolver.ResolveDetailed(property);

        Assert.Equal(6, result.PropertyIndex);
        Assert.NotEmpty(result.PropertyName);
    }
}
