using ItemEditor.Services;
using Radoub.Formats.Uti;
using Radoub.TestUtilities.Mocks;

namespace ItemEditor.Tests.Services;

/// <summary>
/// Tests for item property add/remove operations and round-trip preservation.
/// Covers #1716: property operations beyond the service cascade tests.
/// </summary>
public class ItemPropertyOperationTests
{
    private MockGameDataService CreateMock()
    {
        var mock = new MockGameDataService(includeSampleData: false);

        // itempropdef.2da — Enhancement Bonus (row 6), Ability Bonus (row 0), Cast Spell (row 15)
        SetupPropertyType(mock, 0, "IP_CONST_ABILITY_BONUS", "100", "iprp_abilities", "2", "****");
        SetupPropertyType(mock, 6, "IP_CONST_ENHANCEMENT_BONUS", "106", "****", "2", "****");
        SetupPropertyType(mock, 15, "IP_CONST_CASTSPELL", "115", "iprp_spells", "3", "****");
        SetupPropertyType(mock, 16, "IP_CONST_DAMAGEBONUS", "116", "iprp_damagetype", "4", "****");

        // Subtypes
        mock.Set2DAValue("iprp_abilities", 0, "Name", "200");
        mock.Set2DAValue("iprp_abilities", 0, "Label", "STR");
        mock.Set2DAValue("iprp_abilities", 1, "Name", "201");
        mock.Set2DAValue("iprp_abilities", 1, "Label", "DEX");

        mock.Set2DAValue("iprp_spells", 0, "Name", "300");
        mock.Set2DAValue("iprp_spells", 0, "Label", "Fireball");

        mock.Set2DAValue("iprp_damagetype", 0, "Name", "350");
        mock.Set2DAValue("iprp_damagetype", 0, "Label", "Bludgeoning");
        mock.Set2DAValue("iprp_damagetype", 5, "Name", "355");
        mock.Set2DAValue("iprp_damagetype", 5, "Label", "Fire");

        // Cost tables
        mock.Set2DAValue("iprp_costtable", 2, "Name", "iprp_bonuscost");
        mock.Set2DAValue("iprp_costtable", 3, "Name", "iprp_chargecost");
        mock.Set2DAValue("iprp_costtable", 4, "Name", "iprp_damagecost");

        mock.Set2DAValue("iprp_bonuscost", 1, "Name", "401");
        mock.Set2DAValue("iprp_bonuscost", 1, "Label", "+1");
        mock.Set2DAValue("iprp_bonuscost", 2, "Name", "402");
        mock.Set2DAValue("iprp_bonuscost", 2, "Label", "+2");
        mock.Set2DAValue("iprp_bonuscost", 3, "Name", "403");
        mock.Set2DAValue("iprp_bonuscost", 3, "Label", "+3");

        mock.Set2DAValue("iprp_chargecost", 1, "Name", "500");
        mock.Set2DAValue("iprp_chargecost", 1, "Label", "1_Use_Per_Day");

        mock.Set2DAValue("iprp_damagecost", 2, "Name", "601");
        mock.Set2DAValue("iprp_damagecost", 2, "Label", "1d6");

        // TLK strings
        mock.SetTlkString(100, "Ability Bonus");
        mock.SetTlkString(106, "Enhancement Bonus");
        mock.SetTlkString(115, "Cast Spell");
        mock.SetTlkString(116, "Damage Bonus");
        mock.SetTlkString(200, "Strength");
        mock.SetTlkString(201, "Dexterity");
        mock.SetTlkString(300, "Fireball (5)");
        mock.SetTlkString(350, "Bludgeoning");
        mock.SetTlkString(355, "Fire");
        mock.SetTlkString(401, "+1");
        mock.SetTlkString(402, "+2");
        mock.SetTlkString(403, "+3");
        mock.SetTlkString(500, "1 Use/Day");
        mock.SetTlkString(601, "1d6");

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

    #region Add Property

    [Fact]
    public void AddProperty_IncreasesPropertyCount()
    {
        var mock = CreateMock();
        var service = new ItemPropertyService(mock);
        var uti = new UtiFile();

        Assert.Empty(uti.Properties);

        var prop = service.CreateItemProperty(6, 0, 2, null);
        uti.Properties.Add(prop);

        Assert.Single(uti.Properties);
    }

    [Fact]
    public void AddMultipleProperties_AllPreserved()
    {
        var mock = CreateMock();
        var service = new ItemPropertyService(mock);
        var uti = new UtiFile();

        uti.Properties.Add(service.CreateItemProperty(6, 0, 2, null));   // Enhancement +2
        uti.Properties.Add(service.CreateItemProperty(0, 0, 1, null));   // Ability Bonus STR +1
        uti.Properties.Add(service.CreateItemProperty(15, 0, 1, null));  // Cast Spell Fireball 1/day

        Assert.Equal(3, uti.Properties.Count);
        Assert.Equal(6, uti.Properties[0].PropertyName);
        Assert.Equal(0, uti.Properties[1].PropertyName);
        Assert.Equal(15, uti.Properties[2].PropertyName);
    }

    [Fact]
    public void AddProperty_EnhancementBonus_CorrectFields()
    {
        var mock = CreateMock();
        var service = new ItemPropertyService(mock);

        var prop = service.CreateItemProperty(6, 0, 3, null);

        Assert.Equal(6, prop.PropertyName);    // Enhancement Bonus
        Assert.Equal(0, prop.Subtype);         // (no subtype for enhancement)
        Assert.Equal(2, prop.CostTable);       // iprp_bonuscost
        Assert.Equal(3, prop.CostValue);       // +3
        Assert.Equal(0xFF, prop.Param1);       // no param
        Assert.Equal(100, prop.ChanceAppear);
    }

    [Fact]
    public void AddProperty_AbilityBonusStrength_CorrectFields()
    {
        var mock = CreateMock();
        var service = new ItemPropertyService(mock);

        var prop = service.CreateItemProperty(0, 0, 2, null);

        Assert.Equal(0, prop.PropertyName);    // Ability Bonus
        Assert.Equal(0, prop.Subtype);         // Strength
        Assert.Equal(2, prop.CostTable);       // iprp_bonuscost
        Assert.Equal(2, prop.CostValue);       // +2
    }

    [Fact]
    public void AddProperty_CastSpell_CorrectFields()
    {
        var mock = CreateMock();
        var service = new ItemPropertyService(mock);

        var prop = service.CreateItemProperty(15, 0, 1, null);

        Assert.Equal(15, prop.PropertyName);   // Cast Spell
        Assert.Equal(0, prop.Subtype);         // Fireball
        Assert.Equal(3, prop.CostTable);       // iprp_chargecost
        Assert.Equal(1, prop.CostValue);       // 1 Use/Day
    }

    #endregion

    #region Remove Property

    [Fact]
    public void RemoveProperty_DecreasesCount()
    {
        var mock = CreateMock();
        var service = new ItemPropertyService(mock);
        var uti = new UtiFile();

        uti.Properties.Add(service.CreateItemProperty(6, 0, 2, null));
        uti.Properties.Add(service.CreateItemProperty(0, 0, 1, null));

        Assert.Equal(2, uti.Properties.Count);

        uti.Properties.RemoveAt(0);

        Assert.Single(uti.Properties);
        Assert.Equal(0, uti.Properties[0].PropertyName); // Ability Bonus remains
    }

    [Fact]
    public void RemoveAllProperties_ListEmpty()
    {
        var uti = new UtiFile();
        uti.Properties.Add(new ItemProperty { PropertyName = 6 });
        uti.Properties.Add(new ItemProperty { PropertyName = 0 });

        uti.Properties.Clear();

        Assert.Empty(uti.Properties);
    }

    #endregion

    #region Round-Trip

    [Fact]
    public void RoundTrip_AddedProperties_Preserved()
    {
        var mock = CreateMock();
        var service = new ItemPropertyService(mock);
        var uti = CreateBaseItem();

        uti.Properties.Add(service.CreateItemProperty(6, 0, 2, null));   // Enhancement +2
        uti.Properties.Add(service.CreateItemProperty(0, 1, 3, null));   // Ability DEX +3

        var result = WriteAndReadBack(uti);

        Assert.Equal(2, result.Properties.Count);

        Assert.Equal(6, result.Properties[0].PropertyName);
        Assert.Equal(0, result.Properties[0].Subtype);
        Assert.Equal(2, result.Properties[0].CostTable);
        Assert.Equal(2, result.Properties[0].CostValue);
        Assert.Equal(0xFF, result.Properties[0].Param1);

        Assert.Equal(0, result.Properties[1].PropertyName);
        Assert.Equal(1, result.Properties[1].Subtype);
        Assert.Equal(2, result.Properties[1].CostTable);
        Assert.Equal(3, result.Properties[1].CostValue);
    }

    [Fact]
    public void RoundTrip_RemovedProperties_StayRemoved()
    {
        var uti = CreateBaseItem();
        uti.Properties.Add(new ItemProperty { PropertyName = 6, CostTable = 2, CostValue = 1 });
        uti.Properties.Add(new ItemProperty { PropertyName = 0, CostTable = 2, CostValue = 2 });

        // Remove first property
        uti.Properties.RemoveAt(0);

        var result = WriteAndReadBack(uti);

        Assert.Single(result.Properties);
        Assert.Equal(0, result.Properties[0].PropertyName);
    }

    [Fact]
    public void RoundTrip_EmptyProperties_Preserved()
    {
        var uti = CreateBaseItem();
        Assert.Empty(uti.Properties);

        var result = WriteAndReadBack(uti);

        Assert.Empty(result.Properties);
    }

    [Fact]
    public void RoundTrip_PropertyFieldsExact_ByteLevel()
    {
        var uti = CreateBaseItem();
        var prop = new ItemProperty
        {
            PropertyName = 16,
            Subtype = 5,
            CostTable = 4,
            CostValue = 2,
            Param1 = 0xFF,
            Param1Value = 0,
            ChanceAppear = 100,
            Param2 = 0xFF,
            Param2Value = 0
        };
        uti.Properties.Add(prop);

        var result = WriteAndReadBack(uti);

        Assert.Single(result.Properties);
        var roundTripped = result.Properties[0];
        Assert.Equal(16, roundTripped.PropertyName);
        Assert.Equal(5, roundTripped.Subtype);
        Assert.Equal(4, roundTripped.CostTable);
        Assert.Equal(2, roundTripped.CostValue);
        Assert.Equal(0xFF, roundTripped.Param1);
        Assert.Equal(0, roundTripped.Param1Value);
        Assert.Equal(100, roundTripped.ChanceAppear);
    }

    [Fact]
    public void RoundTrip_MixedAddRemove_FinalStatePreserved()
    {
        var mock = CreateMock();
        var service = new ItemPropertyService(mock);
        var uti = CreateBaseItem();

        // Add 3 properties
        uti.Properties.Add(service.CreateItemProperty(6, 0, 1, null));
        uti.Properties.Add(service.CreateItemProperty(0, 0, 2, null));
        uti.Properties.Add(service.CreateItemProperty(15, 0, 1, null));

        // Remove the middle one
        uti.Properties.RemoveAt(1);

        var result = WriteAndReadBack(uti);

        Assert.Equal(2, result.Properties.Count);
        Assert.Equal(6, result.Properties[0].PropertyName);
        Assert.Equal(15, result.Properties[1].PropertyName);
    }

    #endregion

    #region Helpers

    private static UtiFile CreateBaseItem()
    {
        var uti = new UtiFile
        {
            TemplateResRef = "test_item",
            Tag = "TEST_TAG",
            BaseItem = 0,
            Cost = 100,
        };
        uti.LocalizedName.LocalizedStrings[0] = "Test Item";
        return uti;
    }

    private static UtiFile WriteAndReadBack(UtiFile uti)
    {
        var bytes = UtiWriter.Write(uti);
        return UtiReader.Read(bytes);
    }

    #endregion
}
