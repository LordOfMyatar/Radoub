using ItemEditor.Services;
using Radoub.Formats.Uti;
using Radoub.TestUtilities.Mocks;

namespace ItemEditor.Tests.Services;

/// <summary>
/// Tests for the #2405 checked-property pair model. The Available Properties tree now lets the
/// user tick the exact subtype they want (no silent "first child" add); each checked
/// (PropertyIndex, SubtypeIndex) pair resolves to one ItemProperty. CheckedPropertyResolver is the
/// pure, FlaUI-free mapping the View's AddCheckedProperties delegates to.
/// </summary>
public class CheckedPropertyResolverTests
{
    // Sentinel used for "this property has no subtypes" so subtype index 0 is never confused
    // with the no-subtype case.
    private const int NoSubtype = CheckedProperty.NoSubtype;

    private MockGameDataService CreateMock()
    {
        var mock = new MockGameDataService(includeSampleData: false);

        // itempropdef.2da: Ability Bonus (row 0, has subtypes), Cast Spell (row 15, has subtypes),
        // Keen (row 40, no subtypes).
        SetupPropertyType(mock, 0, "IP_CONST_ABILITY_BONUS", "100", "iprp_abilities", "2", "****");
        SetupPropertyType(mock, 15, "IP_CONST_CASTSPELL", "115", "iprp_spells", "3", "****");
        SetupPropertyType(mock, 40, "IP_CONST_KEEN", "140", "****", "****", "****");

        mock.Set2DAValue("iprp_abilities", 0, "Name", "200");
        mock.Set2DAValue("iprp_abilities", 0, "Label", "STR");
        mock.Set2DAValue("iprp_abilities", 1, "Name", "201");
        mock.Set2DAValue("iprp_abilities", 1, "Label", "DEX");

        mock.Set2DAValue("iprp_spells", 3, "Name", "303");
        mock.Set2DAValue("iprp_spells", 3, "Label", "AcidFog");
        mock.Set2DAValue("iprp_spells", 5, "Name", "305");
        mock.Set2DAValue("iprp_spells", 5, "Label", "Aid");

        mock.Set2DAValue("iprp_costtable", 2, "Name", "iprp_bonuscost");
        mock.Set2DAValue("iprp_costtable", 3, "Name", "iprp_chargecost");
        mock.Set2DAValue("iprp_bonuscost", 1, "Name", "401");
        mock.Set2DAValue("iprp_bonuscost", 1, "Label", "+1");
        mock.Set2DAValue("iprp_chargecost", 1, "Name", "500");
        mock.Set2DAValue("iprp_chargecost", 1, "Label", "1_Use_Per_Day");

        mock.SetTlkString(100, "Ability Bonus");
        mock.SetTlkString(115, "Cast Spell");
        mock.SetTlkString(140, "Keen");
        mock.SetTlkString(200, "Strength");
        mock.SetTlkString(201, "Dexterity");
        mock.SetTlkString(303, "Acid Fog");
        mock.SetTlkString(305, "Aid");

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
    public void Resolve_CheckedSubtype_ProducesExactSubtype_NotFirstChild()
    {
        var mock = CreateMock();
        var service = new ItemPropertyService(mock);

        // Tick "Aid" (subtype 5) under Cast Spell — NOT the first available subtype (3 = Acid Fog).
        var checkd = new[] { new CheckedProperty(15, 5) };

        var result = CheckedPropertyResolver.Resolve(checkd: checkd, service: service,
            baseItem: 0, assignedProperties: new List<ItemProperty>());

        Assert.Single(result.ToAdd);
        Assert.Equal(15, result.ToAdd[0].PropertyName);
        Assert.Equal(5, result.ToAdd[0].Subtype); // the exact ticked subtype, not 3
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void Resolve_MultipleSubtypesUnderOneParent_AddsAllAsSeparateProperties()
    {
        var mock = CreateMock();
        var service = new ItemPropertyService(mock);

        var checkd = new[]
        {
            new CheckedProperty(15, 3), // Acid Fog
            new CheckedProperty(15, 5), // Aid
        };

        var result = CheckedPropertyResolver.Resolve(checkd, service, baseItem: 0,
            assignedProperties: new List<ItemProperty>());

        Assert.Equal(2, result.ToAdd.Count);
        Assert.Contains(result.ToAdd, p => p.Subtype == 3);
        Assert.Contains(result.ToAdd, p => p.Subtype == 5);
    }

    [Fact]
    public void Resolve_NoSubtypeProperty_UsesSentinel_ProducesOneProperty()
    {
        var mock = CreateMock();
        var service = new ItemPropertyService(mock);

        // Keen (40) has no subtypes — checked via the parent checkbox carrying the NoSubtype sentinel.
        var checkd = new[] { new CheckedProperty(40, NoSubtype) };

        var result = CheckedPropertyResolver.Resolve(checkd, service, baseItem: 0,
            assignedProperties: new List<ItemProperty>());

        Assert.Single(result.ToAdd);
        Assert.Equal(40, result.ToAdd[0].PropertyName);
        Assert.Equal(0, result.ToAdd[0].Subtype); // CreateItemProperty maps no-subtype to 0
    }

    [Fact]
    public void Resolve_AlreadyAssignedSubtype_IsSkipped()
    {
        var mock = CreateMock();
        var service = new ItemPropertyService(mock);

        // Acid Fog (15/3) already on the item; ticking it again should be skipped (move semantics).
        var assigned = new List<ItemProperty>
        {
            service.CreateItemProperty(15, 3, 0, null)
        };

        var checkd = new[] { new CheckedProperty(15, 3) };

        var result = CheckedPropertyResolver.Resolve(checkd, service, baseItem: 0, assigned);

        Assert.Empty(result.ToAdd);
        Assert.Single(result.Skipped);
    }
}
