using ItemEditor.Services;
using Radoub.Formats.Uti;
using Radoub.TestUtilities.Mocks;

namespace ItemEditor.Tests.Services;

public class ItemPropertyServiceTests
{
    private MockGameDataService CreateMockWithItemPropertyData()
    {
        var mock = new MockGameDataService(includeSampleData: false);

        // itempropdef.2da - master property definitions
        // Columns: Name, Label, SubTypeResRef, CostTableResRef, Param1ResRef, GameStrRef
        mock.Set2DAValue("itempropdef", 0, "Name", "100");
        mock.Set2DAValue("itempropdef", 0, "Label", "IP_CONST_ABILITY_BONUS");
        mock.Set2DAValue("itempropdef", 0, "SubTypeResRef", "iprp_abilities");
        mock.Set2DAValue("itempropdef", 0, "CostTableResRef", "2");
        mock.Set2DAValue("itempropdef", 0, "Param1ResRef", "****");
        mock.Set2DAValue("itempropdef", 0, "GameStrRef", "100");

        mock.Set2DAValue("itempropdef", 1, "Name", "101");
        mock.Set2DAValue("itempropdef", 1, "Label", "IP_CONST_ACBONUS");
        mock.Set2DAValue("itempropdef", 1, "SubTypeResRef", "****");
        mock.Set2DAValue("itempropdef", 1, "CostTableResRef", "2");
        mock.Set2DAValue("itempropdef", 1, "Param1ResRef", "****");
        mock.Set2DAValue("itempropdef", 1, "GameStrRef", "101");

        mock.Set2DAValue("itempropdef", 6, "Name", "106");
        mock.Set2DAValue("itempropdef", 6, "Label", "IP_CONST_ENHANCEMENT_BONUS");
        mock.Set2DAValue("itempropdef", 6, "SubTypeResRef", "****");
        mock.Set2DAValue("itempropdef", 6, "CostTableResRef", "2");
        mock.Set2DAValue("itempropdef", 6, "Param1ResRef", "****");
        mock.Set2DAValue("itempropdef", 6, "GameStrRef", "106");

        mock.Set2DAValue("itempropdef", 15, "Name", "115");
        mock.Set2DAValue("itempropdef", 15, "Label", "IP_CONST_CASTSPELL");
        mock.Set2DAValue("itempropdef", 15, "SubTypeResRef", "iprp_spells");
        mock.Set2DAValue("itempropdef", 15, "CostTableResRef", "3");
        mock.Set2DAValue("itempropdef", 15, "Param1ResRef", "****");
        mock.Set2DAValue("itempropdef", 15, "GameStrRef", "115");

        mock.Set2DAValue("itempropdef", 16, "Name", "116");
        mock.Set2DAValue("itempropdef", 16, "Label", "IP_CONST_DAMAGEBONUS");
        mock.Set2DAValue("itempropdef", 16, "SubTypeResRef", "iprp_damagetype");
        mock.Set2DAValue("itempropdef", 16, "CostTableResRef", "4");
        mock.Set2DAValue("itempropdef", 16, "Param1ResRef", "****");
        mock.Set2DAValue("itempropdef", 16, "GameStrRef", "116");

        // iprp_abilities.2da - subtypes for Ability Bonus
        mock.Set2DAValue("iprp_abilities", 0, "Name", "200");
        mock.Set2DAValue("iprp_abilities", 0, "Label", "IP_CONST_ABILITY_STR");
        mock.Set2DAValue("iprp_abilities", 1, "Name", "201");
        mock.Set2DAValue("iprp_abilities", 1, "Label", "IP_CONST_ABILITY_DEX");
        mock.Set2DAValue("iprp_abilities", 2, "Name", "202");
        mock.Set2DAValue("iprp_abilities", 2, "Label", "IP_CONST_ABILITY_CON");
        mock.Set2DAValue("iprp_abilities", 3, "Name", "203");
        mock.Set2DAValue("iprp_abilities", 3, "Label", "IP_CONST_ABILITY_INT");
        mock.Set2DAValue("iprp_abilities", 4, "Name", "204");
        mock.Set2DAValue("iprp_abilities", 4, "Label", "IP_CONST_ABILITY_WIS");
        mock.Set2DAValue("iprp_abilities", 5, "Name", "205");
        mock.Set2DAValue("iprp_abilities", 5, "Label", "IP_CONST_ABILITY_CHA");

        // iprp_spells.2da - subtypes for Cast Spell
        mock.Set2DAValue("iprp_spells", 0, "Name", "300");
        mock.Set2DAValue("iprp_spells", 0, "Label", "Fireball");
        mock.Set2DAValue("iprp_spells", 0, "SpellIndex", "26");
        mock.Set2DAValue("iprp_spells", 1, "Name", "301");
        mock.Set2DAValue("iprp_spells", 1, "Label", "Heal");
        mock.Set2DAValue("iprp_spells", 1, "SpellIndex", "67");

        // iprp_damagetype.2da - subtypes for Damage Bonus
        mock.Set2DAValue("iprp_damagetype", 0, "Name", "350");
        mock.Set2DAValue("iprp_damagetype", 0, "Label", "Bludgeoning");
        mock.Set2DAValue("iprp_damagetype", 5, "Name", "355");
        mock.Set2DAValue("iprp_damagetype", 5, "Label", "Fire");

        // iprp_costtable.2da - maps cost table index to 2DA name
        mock.Set2DAValue("iprp_costtable", 2, "Name", "iprp_bonuscost");
        mock.Set2DAValue("iprp_costtable", 2, "Label", "IPRP_BONUSCOST");
        mock.Set2DAValue("iprp_costtable", 3, "Name", "iprp_chargecost");
        mock.Set2DAValue("iprp_costtable", 3, "Label", "IPRP_CHARGECOST");
        mock.Set2DAValue("iprp_costtable", 4, "Name", "iprp_damagecost");
        mock.Set2DAValue("iprp_costtable", 4, "Label", "IPRP_DAMAGECOST");

        // iprp_bonuscost.2da - cost values for enhancement/ability/AC bonus
        mock.Set2DAValue("iprp_bonuscost", 1, "Name", "401");
        mock.Set2DAValue("iprp_bonuscost", 1, "Label", "+1");
        mock.Set2DAValue("iprp_bonuscost", 2, "Name", "402");
        mock.Set2DAValue("iprp_bonuscost", 2, "Label", "+2");
        mock.Set2DAValue("iprp_bonuscost", 3, "Name", "403");
        mock.Set2DAValue("iprp_bonuscost", 3, "Label", "+3");

        // iprp_chargecost.2da - charges per day for Cast Spell
        mock.Set2DAValue("iprp_chargecost", 1, "Name", "500");
        mock.Set2DAValue("iprp_chargecost", 1, "Label", "1_Use_Per_Day");
        mock.Set2DAValue("iprp_chargecost", 2, "Name", "501");
        mock.Set2DAValue("iprp_chargecost", 2, "Label", "2_Uses_Per_Day");

        // iprp_damagecost.2da - damage values
        mock.Set2DAValue("iprp_damagecost", 1, "Name", "600");
        mock.Set2DAValue("iprp_damagecost", 1, "Label", "1d4");
        mock.Set2DAValue("iprp_damagecost", 2, "Name", "601");
        mock.Set2DAValue("iprp_damagecost", 2, "Label", "1d6");

        // TLK strings
        mock.SetTlkString(100, "Ability Bonus");
        mock.SetTlkString(101, "AC Bonus");
        mock.SetTlkString(106, "Enhancement Bonus");
        mock.SetTlkString(115, "Cast Spell");
        mock.SetTlkString(116, "Damage Bonus");
        mock.SetTlkString(200, "Strength");
        mock.SetTlkString(201, "Dexterity");
        mock.SetTlkString(202, "Constitution");
        mock.SetTlkString(203, "Intelligence");
        mock.SetTlkString(204, "Wisdom");
        mock.SetTlkString(205, "Charisma");
        mock.SetTlkString(300, "Fireball (5)");
        mock.SetTlkString(301, "Heal (11)");
        mock.SetTlkString(350, "Bludgeoning");
        mock.SetTlkString(355, "Fire");
        mock.SetTlkString(401, "+1");
        mock.SetTlkString(402, "+2");
        mock.SetTlkString(403, "+3");
        mock.SetTlkString(500, "1 Use/Day");
        mock.SetTlkString(501, "2 Uses/Day");
        mock.SetTlkString(600, "1d4");
        mock.SetTlkString(601, "1d6");

        return mock;
    }

    #region GetAvailablePropertyTypes

    [Fact]
    public void GetAvailablePropertyTypes_ReturnsAllTypesFromItempropdef()
    {
        var mock = CreateMockWithItemPropertyData();
        var service = new ItemPropertyService(mock);

        var types = service.GetAvailablePropertyTypes();

        // We defined rows 0, 1, 6, 15, 16 in itempropdef
        Assert.True(types.Count >= 5);
        Assert.Contains(types, t => t.PropertyIndex == 0 && t.DisplayName == "Ability Bonus");
        Assert.Contains(types, t => t.PropertyIndex == 1 && t.DisplayName == "AC Bonus");
        Assert.Contains(types, t => t.PropertyIndex == 6 && t.DisplayName == "Enhancement Bonus");
        Assert.Contains(types, t => t.PropertyIndex == 15 && t.DisplayName == "Cast Spell");
        Assert.Contains(types, t => t.PropertyIndex == 16 && t.DisplayName == "Damage Bonus");
    }

    [Fact]
    public void GetAvailablePropertyTypes_ReturnsSortedByDisplayName()
    {
        var mock = CreateMockWithItemPropertyData();
        var service = new ItemPropertyService(mock);

        var types = service.GetAvailablePropertyTypes();
        var names = types.Select(t => t.DisplayName).ToList();

        Assert.Equal(names.OrderBy(n => n).ToList(), names);
    }

    [Fact]
    public void GetAvailablePropertyTypes_CachesResults()
    {
        var mock = CreateMockWithItemPropertyData();
        var service = new ItemPropertyService(mock);

        var first = service.GetAvailablePropertyTypes();
        var second = service.GetAvailablePropertyTypes();

        Assert.Same(first, second);
    }

    [Fact]
    public void GetAvailablePropertyTypes_ReturnsEmpty_WhenServiceUnconfigured()
    {
        var mock = CreateMockWithItemPropertyData();
        mock.IsConfigured = false;
        var service = new ItemPropertyService(mock);

        var types = service.GetAvailablePropertyTypes();

        Assert.Empty(types);
    }

    #endregion

    #region GetSubtypes

    [Fact]
    public void GetSubtypes_ReturnsEntries_ForPropertyWithSubtypes()
    {
        var mock = CreateMockWithItemPropertyData();
        var service = new ItemPropertyService(mock);

        // Ability Bonus (index 0) has SubTypeResRef = "iprp_abilities"
        var subtypes = service.GetSubtypes(0);

        Assert.Equal(6, subtypes.Count);
        Assert.Contains(subtypes, s => s.Index == 0 && s.DisplayName == "Strength");
        Assert.Contains(subtypes, s => s.Index == 5 && s.DisplayName == "Charisma");
    }

    [Fact]
    public void GetSubtypes_ReturnsEmpty_ForPropertyWithoutSubtypes()
    {
        var mock = CreateMockWithItemPropertyData();
        var service = new ItemPropertyService(mock);

        // Enhancement Bonus (index 6) has SubTypeResRef = "****"
        var subtypes = service.GetSubtypes(6);

        Assert.Empty(subtypes);
    }

    [Fact]
    public void GetSubtypes_ReturnsEmpty_ForInvalidPropertyIndex()
    {
        var mock = CreateMockWithItemPropertyData();
        var service = new ItemPropertyService(mock);

        var subtypes = service.GetSubtypes(999);

        Assert.Empty(subtypes);
    }

    #endregion

    #region GetCostValues

    [Fact]
    public void GetCostValues_ReturnsEntries_ForPropertyWithCostTable()
    {
        var mock = CreateMockWithItemPropertyData();
        var service = new ItemPropertyService(mock);

        // Enhancement Bonus (index 6) has CostTableResRef = "2" → iprp_bonuscost
        var costValues = service.GetCostValues(6);

        Assert.True(costValues.Count >= 3);
        Assert.Contains(costValues, c => c.Index == 1 && c.DisplayName == "+1");
        Assert.Contains(costValues, c => c.Index == 2 && c.DisplayName == "+2");
        Assert.Contains(costValues, c => c.Index == 3 && c.DisplayName == "+3");
    }

    [Fact]
    public void GetCostValues_ReturnsEmpty_WhenNoCostTable()
    {
        var mock = CreateMockWithItemPropertyData();
        // Add a property with no cost table
        mock.Set2DAValue("itempropdef", 20, "Name", "120");
        mock.Set2DAValue("itempropdef", 20, "Label", "IP_CONST_NOCOST");
        mock.Set2DAValue("itempropdef", 20, "SubTypeResRef", "****");
        mock.Set2DAValue("itempropdef", 20, "CostTableResRef", "****");
        mock.Set2DAValue("itempropdef", 20, "Param1ResRef", "****");
        mock.Set2DAValue("itempropdef", 20, "GameStrRef", "120");
        mock.SetTlkString(120, "No Cost Property");

        var service = new ItemPropertyService(mock);
        var costValues = service.GetCostValues(20);

        Assert.Empty(costValues);
    }

    [Fact]
    public void GetCostValues_ResolvesThroughCostTableChain()
    {
        var mock = CreateMockWithItemPropertyData();
        var service = new ItemPropertyService(mock);

        // Cast Spell (index 15) has CostTableResRef = "3" → iprp_chargecost
        var costValues = service.GetCostValues(15);

        Assert.Contains(costValues, c => c.Index == 1 && c.DisplayName == "1 Use/Day");
        Assert.Contains(costValues, c => c.Index == 2 && c.DisplayName == "2 Uses/Day");
    }

    #endregion

    #region GetParamValues

    [Fact]
    public void GetParamValues_ReturnsEmpty_WhenNoParamTable()
    {
        var mock = CreateMockWithItemPropertyData();
        var service = new ItemPropertyService(mock);

        // Enhancement Bonus has no Param1ResRef
        var paramValues = service.GetParamValues(6);

        Assert.Empty(paramValues);
    }

    [Fact]
    public void GetParamValues_ReturnsEntries_WhenParamTableExists()
    {
        var mock = CreateMockWithItemPropertyData();

        // Add a property with param table
        mock.Set2DAValue("itempropdef", 24, "Name", "124");
        mock.Set2DAValue("itempropdef", 24, "Label", "IP_CONST_WITHPARAM");
        mock.Set2DAValue("itempropdef", 24, "SubTypeResRef", "****");
        mock.Set2DAValue("itempropdef", 24, "CostTableResRef", "****");
        mock.Set2DAValue("itempropdef", 24, "Param1ResRef", "1");
        mock.Set2DAValue("itempropdef", 24, "GameStrRef", "124");
        mock.SetTlkString(124, "Param Property");

        // iprp_paramtable.2da - maps param table index to 2DA name
        mock.Set2DAValue("iprp_paramtable", 1, "TableResRef", "iprp_damagetype");
        mock.Set2DAValue("iprp_paramtable", 1, "Name", "Damage Type");

        var service = new ItemPropertyService(mock);
        var paramValues = service.GetParamValues(24);

        // Should resolve through paramtable → iprp_damagetype
        Assert.Contains(paramValues, p => p.Index == 0 && p.DisplayName == "Bludgeoning");
        Assert.Contains(paramValues, p => p.Index == 5 && p.DisplayName == "Fire");
    }

    #endregion

    #region SearchProperties

    [Fact]
    public void SearchProperties_MatchesByPropertyName()
    {
        var mock = CreateMockWithItemPropertyData();
        var service = new ItemPropertyService(mock);

        var results = service.SearchProperties("enhancement");

        Assert.Contains(results, r => r.PropertyIndex == 6);
    }

    [Fact]
    public void SearchProperties_MatchesBySubtypeName()
    {
        var mock = CreateMockWithItemPropertyData();
        var service = new ItemPropertyService(mock);

        var results = service.SearchProperties("strength");

        // Should find Ability Bonus because it has a "Strength" subtype
        Assert.Contains(results, r => r.PropertyIndex == 0);
    }

    [Fact]
    public void SearchProperties_IsCaseInsensitive()
    {
        var mock = CreateMockWithItemPropertyData();
        var service = new ItemPropertyService(mock);

        var upper = service.SearchProperties("ENHANCEMENT");
        var lower = service.SearchProperties("enhancement");

        Assert.Equal(upper.Count, lower.Count);
    }

    [Fact]
    public void SearchProperties_ReturnsEmpty_ForNoMatch()
    {
        var mock = CreateMockWithItemPropertyData();
        var service = new ItemPropertyService(mock);

        var results = service.SearchProperties("zzzznonexistent");

        Assert.Empty(results);
    }

    #endregion

    #region HasSubtypes / HasCostTable / HasParamTable

    [Fact]
    public void HasSubtypes_ReturnsTrue_WhenSubTypeResRefExists()
    {
        var mock = CreateMockWithItemPropertyData();
        var service = new ItemPropertyService(mock);

        Assert.True(service.HasSubtypes(0));   // Ability Bonus
        Assert.True(service.HasSubtypes(15));  // Cast Spell
    }

    [Fact]
    public void HasSubtypes_ReturnsFalse_WhenNoSubTypeResRef()
    {
        var mock = CreateMockWithItemPropertyData();
        var service = new ItemPropertyService(mock);

        Assert.False(service.HasSubtypes(6));  // Enhancement Bonus
        Assert.False(service.HasSubtypes(1));  // AC Bonus
    }

    [Fact]
    public void HasCostTable_ReturnsTrue_WhenCostTableResRefExists()
    {
        var mock = CreateMockWithItemPropertyData();
        var service = new ItemPropertyService(mock);

        Assert.True(service.HasCostTable(6));  // Enhancement Bonus
    }

    [Fact]
    public void HasCostTable_ReturnsFalse_WhenNoCostTableResRef()
    {
        var mock = CreateMockWithItemPropertyData();
        mock.Set2DAValue("itempropdef", 20, "Name", "120");
        mock.Set2DAValue("itempropdef", 20, "Label", "IP_CONST_NOCOST");
        mock.Set2DAValue("itempropdef", 20, "SubTypeResRef", "****");
        mock.Set2DAValue("itempropdef", 20, "CostTableResRef", "****");
        mock.Set2DAValue("itempropdef", 20, "Param1ResRef", "****");
        mock.Set2DAValue("itempropdef", 20, "GameStrRef", "120");
        mock.SetTlkString(120, "No Cost");

        var service = new ItemPropertyService(mock);

        Assert.False(service.HasCostTable(20));
    }

    #endregion

    #region CreateItemProperty

    [Fact]
    public void CreateItemProperty_SetsCorrectIndices()
    {
        var mock = CreateMockWithItemPropertyData();
        var service = new ItemPropertyService(mock);

        // Create an Enhancement Bonus +2
        var prop = service.CreateItemProperty(
            propertyIndex: 6,
            subtypeIndex: 0,
            costValueIndex: 2,
            paramValueIndex: null);

        Assert.Equal(6, prop.PropertyName);
        Assert.Equal(0, prop.Subtype);
        Assert.Equal(2, prop.CostTable);   // from itempropdef CostTableResRef
        Assert.Equal(2, prop.CostValue);
        Assert.Equal(0xFF, prop.Param1);    // no param
        Assert.Equal(100, prop.ChanceAppear);
    }

    [Fact]
    public void CreateItemProperty_SetsParamWhenProvided()
    {
        var mock = CreateMockWithItemPropertyData();

        // Add a property type with param
        mock.Set2DAValue("itempropdef", 24, "Name", "124");
        mock.Set2DAValue("itempropdef", 24, "Label", "IP_CONST_WITHPARAM");
        mock.Set2DAValue("itempropdef", 24, "SubTypeResRef", "****");
        mock.Set2DAValue("itempropdef", 24, "CostTableResRef", "****");
        mock.Set2DAValue("itempropdef", 24, "Param1ResRef", "1");
        mock.Set2DAValue("itempropdef", 24, "GameStrRef", "124");
        mock.SetTlkString(124, "Param Property");

        mock.Set2DAValue("iprp_paramtable", 1, "TableResRef", "iprp_damagetype");
        mock.Set2DAValue("iprp_paramtable", 1, "Name", "Damage Type");

        var service = new ItemPropertyService(mock);

        var prop = service.CreateItemProperty(
            propertyIndex: 24,
            subtypeIndex: 0,
            costValueIndex: 0,
            paramValueIndex: 5);

        Assert.Equal(1, prop.Param1);       // param table index from itempropdef
        Assert.Equal(5, prop.Param1Value);   // the value we passed
    }

    #endregion
}
