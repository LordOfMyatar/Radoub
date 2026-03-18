using ItemEditor.Services;
using Radoub.Formats.Uti;
using Radoub.TestUtilities.Mocks;

namespace ItemEditor.Tests.Services;

public class ItemStatisticsServiceTests
{
    private MockGameDataService CreateMock()
    {
        var mock = new MockGameDataService(includeSampleData: false);

        // itempropdef.2da
        SetupPropertyType(mock, 0, "IP_CONST_ABILITY_BONUS", "100", "iprp_abilities", "2", "****");
        SetupPropertyType(mock, 6, "IP_CONST_ENHANCEMENT_BONUS", "106", "****", "2", "****");
        SetupPropertyType(mock, 15, "IP_CONST_CASTSPELL", "115", "iprp_spells", "3", "****");

        // Subtypes
        mock.Set2DAValue("iprp_abilities", 0, "Name", "200");
        mock.Set2DAValue("iprp_abilities", 0, "Label", "STR");
        mock.Set2DAValue("iprp_abilities", 1, "Name", "201");
        mock.Set2DAValue("iprp_abilities", 1, "Label", "DEX");

        mock.Set2DAValue("iprp_spells", 0, "Name", "300");
        mock.Set2DAValue("iprp_spells", 0, "Label", "Fireball");

        // Cost tables
        mock.Set2DAValue("iprp_costtable", 2, "Name", "iprp_bonuscost");
        mock.Set2DAValue("iprp_costtable", 3, "Name", "iprp_chargecost");

        mock.Set2DAValue("iprp_bonuscost", 1, "Name", "401");
        mock.Set2DAValue("iprp_bonuscost", 1, "Label", "+1");
        mock.Set2DAValue("iprp_bonuscost", 2, "Name", "402");
        mock.Set2DAValue("iprp_bonuscost", 2, "Label", "+2");
        mock.Set2DAValue("iprp_bonuscost", 3, "Name", "403");
        mock.Set2DAValue("iprp_bonuscost", 3, "Label", "+3");

        mock.Set2DAValue("iprp_chargecost", 1, "Name", "500");
        mock.Set2DAValue("iprp_chargecost", 1, "Label", "1_Use_Per_Day");

        // TLK strings
        mock.SetTlkString(100, "Ability Bonus");
        mock.SetTlkString(106, "Enhancement Bonus");
        mock.SetTlkString(115, "Cast Spell");
        mock.SetTlkString(200, "Strength");
        mock.SetTlkString(201, "Dexterity");
        mock.SetTlkString(300, "Fireball (5)");
        mock.SetTlkString(401, "+1");
        mock.SetTlkString(402, "+2");
        mock.SetTlkString(403, "+3");
        mock.SetTlkString(500, "1 Use/Day");

        return mock;
    }

    private void SetupPropertyType(MockGameDataService mock, int row, string label,
        string gameStrRef, string subtypeResRef, string costTableResRef, string param1ResRef)
    {
        mock.Set2DAValue("itempropdef", row, "Name", gameStrRef);
        mock.Set2DAValue("itempropdef", row, "Label", label);
        mock.Set2DAValue("itempropdef", row, "SubTypeResRef", subtypeResRef);
        mock.Set2DAValue("itempropdef", row, "CostTableResRef", costTableResRef);
        mock.Set2DAValue("itempropdef", row, "Param1ResRef", param1ResRef);
        mock.Set2DAValue("itempropdef", row, "GameStrRef", gameStrRef);
    }

    [Fact]
    public void GenerateStatistics_EmptyProperties_ReturnsEmpty()
    {
        var mock = CreateMock();
        var propService = new ItemPropertyService(mock);
        var statsService = new ItemStatisticsService(propService);

        var result = statsService.GenerateStatistics(new List<ItemProperty>());

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GenerateStatistics_NullProperties_ReturnsEmpty()
    {
        var mock = CreateMock();
        var propService = new ItemPropertyService(mock);
        var statsService = new ItemStatisticsService(propService);

        var result = statsService.GenerateStatistics(null!);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GenerateStatistics_SingleEnhancement_FormatsCorrectly()
    {
        var mock = CreateMock();
        var propService = new ItemPropertyService(mock);
        var statsService = new ItemStatisticsService(propService);

        var props = new List<ItemProperty>
        {
            propService.CreateItemProperty(6, 0, 2, null) // Enhancement +2
        };

        var result = statsService.GenerateStatistics(props);

        Assert.Equal("Enhancement Bonus +2", result);
    }

    [Fact]
    public void GenerateStatistics_AbilityBonusWithSubtype_IncludesSubtype()
    {
        var mock = CreateMock();
        var propService = new ItemPropertyService(mock);
        var statsService = new ItemStatisticsService(propService);

        var props = new List<ItemProperty>
        {
            propService.CreateItemProperty(0, 0, 3, null) // Ability Bonus STR +3
        };

        var result = statsService.GenerateStatistics(props);

        Assert.Equal("Ability Bonus Strength +3", result);
    }

    [Fact]
    public void GenerateStatistics_MultipleProperties_OneLine_Each()
    {
        var mock = CreateMock();
        var propService = new ItemPropertyService(mock);
        var statsService = new ItemStatisticsService(propService);

        var props = new List<ItemProperty>
        {
            propService.CreateItemProperty(6, 0, 2, null),  // Enhancement +2
            propService.CreateItemProperty(0, 0, 3, null),  // Ability STR +3
        };

        var result = statsService.GenerateStatistics(props);
        var lines = result.Split(Environment.NewLine);

        Assert.Equal(2, lines.Length);
        Assert.Equal("Enhancement Bonus +2", lines[0]);
        Assert.Equal("Ability Bonus Strength +3", lines[1]);
    }

    [Fact]
    public void GenerateStatistics_AfterPropertyRemoved_UpdatesOutput()
    {
        var mock = CreateMock();
        var propService = new ItemPropertyService(mock);
        var statsService = new ItemStatisticsService(propService);

        var props = new List<ItemProperty>
        {
            propService.CreateItemProperty(6, 0, 2, null),  // Enhancement +2
            propService.CreateItemProperty(0, 0, 3, null),  // Ability STR +3
        };

        var before = statsService.GenerateStatistics(props);
        Assert.Equal(2, before.Split(Environment.NewLine).Length);

        props.RemoveAt(0); // Remove Enhancement
        var after = statsService.GenerateStatistics(props);

        Assert.Equal("Ability Bonus Strength +3", after);
    }

    [Fact]
    public void GenerateStatistics_AfterPropertyEdited_ReflectsNewValue()
    {
        var mock = CreateMock();
        var propService = new ItemPropertyService(mock);
        var statsService = new ItemStatisticsService(propService);

        var props = new List<ItemProperty>
        {
            propService.CreateItemProperty(6, 0, 1, null),  // Enhancement +1
        };

        Assert.Equal("Enhancement Bonus +1", statsService.GenerateStatistics(props));

        // Edit to +3
        props[0] = propService.CreateItemProperty(6, 0, 3, null);
        Assert.Equal("Enhancement Bonus +3", statsService.GenerateStatistics(props));
    }

    [Fact]
    public void GenerateStatistics_UnknownProperty_ShowsIndex()
    {
        var mock = CreateMock();
        var propService = new ItemPropertyService(mock);
        var statsService = new ItemStatisticsService(propService);

        var props = new List<ItemProperty>
        {
            new ItemProperty { PropertyName = 999, Subtype = 0, CostTable = 0, CostValue = 0, Param1 = 0xFF }
        };

        var result = statsService.GenerateStatistics(props);

        Assert.Equal("Property 999", result);
    }
}
