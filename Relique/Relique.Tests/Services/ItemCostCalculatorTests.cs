using System.Collections.Generic;
using ItemEditor.Services;
using Radoub.Formats.Uti;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace ItemEditor.Tests.Services;

/// <summary>
/// Tests for the Aurora item cost formula (wiki Ch4 §4.4):
///   ItemCost = [BaseCost + 1000*(Mult^2 - NegMult^2) + SpellCosts] * MaxStack * BaseMult + AddCost
/// </summary>
public class ItemCostCalculatorTests
{
    // ---- Mock setup ---------------------------------------------------------

    /// <summary>
    /// Builds a mock with one non-armor base item (index 40, a longsword-like)
    /// and one armor base item (index 16). Property cost tables are wired so that
    /// individual tests can assert specific contributions.
    /// </summary>
    private static MockGameDataService CreateMock()
    {
        var mock = new MockGameDataService(includeSampleData: false);

        // baseitems.2da
        // 40 = non-armor: BaseCost 10, Stacking 1, ItemMultiplier 1, ModelType 0
        mock.Set2DAValue("baseitems", 40, "BaseCost", "10");
        mock.Set2DAValue("baseitems", 40, "Stacking", "1");
        mock.Set2DAValue("baseitems", 40, "ItemMultiplier", "1");
        mock.Set2DAValue("baseitems", 40, "ModelType", "0");

        // 16 = armor: BaseCost column ignored for armor, Stacking 1, ItemMultiplier 1, ModelType 3
        mock.Set2DAValue("baseitems", 16, "BaseCost", "0");
        mock.Set2DAValue("baseitems", 16, "Stacking", "1");
        mock.Set2DAValue("baseitems", 16, "ItemMultiplier", "1");
        mock.Set2DAValue("baseitems", 16, "ModelType", "3");

        // 41 = stackable non-armor: BaseCost 5, Stacking 99, ItemMultiplier 1, ModelType 0
        mock.Set2DAValue("baseitems", 41, "BaseCost", "5");
        mock.Set2DAValue("baseitems", 41, "Stacking", "99");
        mock.Set2DAValue("baseitems", 41, "ItemMultiplier", "1");
        mock.Set2DAValue("baseitems", 41, "ModelType", "0");

        // 1 = longsword (BaseCost 15), 0 = dagger (BaseCost 10) — verified against the toolset.
        // Weapons have ItemMultiplier 2 (the toolset doubles the bracket for them).
        mock.Set2DAValue("baseitems", 1, "BaseCost", "15");
        mock.Set2DAValue("baseitems", 1, "Stacking", "1");
        mock.Set2DAValue("baseitems", 1, "ItemMultiplier", "2");
        mock.Set2DAValue("baseitems", 1, "ModelType", "0");
        mock.Set2DAValue("baseitems", 0, "BaseCost", "10");
        mock.Set2DAValue("baseitems", 0, "Stacking", "1");
        mock.Set2DAValue("baseitems", 0, "ItemMultiplier", "2");
        mock.Set2DAValue("baseitems", 0, "ModelType", "0");

        // armor.2da: row = base AC, COST column
        mock.Set2DAValue("armor", 0, "COST", "0");
        mock.Set2DAValue("armor", 5, "COST", "400");   // AC 5 armor base cost 400

        // parts_chest.2da: row = torso part, ACBONUS column
        mock.Set2DAValue("parts_chest", 0, "ACBONUS", "0");
        mock.Set2DAValue("parts_chest", 7, "ACBONUS", "5");  // torso part 7 → AC 5

        // itempropdef.2da
        //  6  = Enhancement Bonus: PropertyCost 0, CostTable 2 (bonuscost)
        mock.Set2DAValue("itempropdef", 6, "Cost", "0");
        mock.Set2DAValue("itempropdef", 6, "SubTypeResRef", "****");
        mock.Set2DAValue("itempropdef", 6, "CostTableResRef", "2");
        //  0  = Ability Bonus: PropertyCost 0, has subtype iprp_abilities, CostTable 2
        mock.Set2DAValue("itempropdef", 0, "Cost", "0");
        mock.Set2DAValue("itempropdef", 0, "SubTypeResRef", "iprp_abilities");
        mock.Set2DAValue("itempropdef", 0, "CostTableResRef", "2");
        // 15  = Cast Spell: PropertyCost 0, subtype iprp_spells, CostTable 3
        mock.Set2DAValue("itempropdef", 15, "Cost", "0");
        mock.Set2DAValue("itempropdef", 15, "SubTypeResRef", "iprp_spells");
        mock.Set2DAValue("itempropdef", 15, "CostTableResRef", "3");
        // 30 = a property with a non-zero PropertyCost and a negative cost value
        mock.Set2DAValue("itempropdef", 30, "Cost", "0");
        mock.Set2DAValue("itempropdef", 30, "SubTypeResRef", "****");
        mock.Set2DAValue("itempropdef", 30, "CostTableResRef", "5");
        //  1 = AC Bonus: nonzero PropertyCost (0.9) acts as a MULTIPLIER on CostValue (NWN:EE),
        //      not an additive term. Matches the real studded-leather +2 AC case.
        mock.Set2DAValue("itempropdef", 1, "Cost", "0.9");
        mock.Set2DAValue("itempropdef", 1, "SubTypeResRef", "****");
        mock.Set2DAValue("itempropdef", 1, "CostTableResRef", "9");
        // 16 = Damage Bonus: real PropertyCost 3.5 multiplier, CostTable 4 (verified vs toolset).
        mock.Set2DAValue("itempropdef", 16, "Cost", "3.5");
        mock.Set2DAValue("itempropdef", 16, "SubTypeResRef", "****");
        mock.Set2DAValue("itempropdef", 16, "CostTableResRef", "4");

        // iprp_costtable.2da: index → cost 2da name
        mock.Set2DAValue("iprp_costtable", 2, "Name", "iprp_bonuscost");
        mock.Set2DAValue("iprp_costtable", 3, "Name", "iprp_spellcost");
        mock.Set2DAValue("iprp_costtable", 4, "Name", "iprp_damagecost");
        mock.Set2DAValue("iprp_costtable", 5, "Name", "iprp_negcost");
        mock.Set2DAValue("iprp_costtable", 9, "Name", "iprp_armorcost");

        // iprp_armorcost.2da: AC bonus magnitudes
        mock.Set2DAValue("iprp_armorcost", 2, "Cost", "1.9");  // +2 AC → 1.9

        // iprp_bonuscost.2da: enhancement +5 → 4.9, +2 → 1.9 (verified vs toolset)
        mock.Set2DAValue("iprp_bonuscost", 5, "Cost", "4.9");
        // iprp_damagecost.2da: damage value 5 → magnitude 1.0
        mock.Set2DAValue("iprp_damagecost", 5, "Cost", "1");

        // iprp_bonuscost.2da: CostValue row → Cost column
        mock.Set2DAValue("iprp_bonuscost", 1, "Cost", "1");   // +1 → cost 1
        mock.Set2DAValue("iprp_bonuscost", 2, "Cost", "2");   // +2 → cost 2
        mock.Set2DAValue("iprp_bonuscost", 3, "Cost", "3");   // +3 → cost 3

        // iprp_abilities.2da: subtype rows have a Cost column (SubtypeCost)
        mock.Set2DAValue("iprp_abilities", 0, "Cost", "0");

        // iprp_spells.2da: subtype rows have a Cost column (SubtypeCost for Cast Spell)
        mock.Set2DAValue("iprp_spells", 0, "Cost", "1");

        // iprp_spellcost.2da: spell cost values
        mock.Set2DAValue("iprp_spellcost", 1, "Cost", "10");
        mock.Set2DAValue("iprp_spellcost", 2, "Cost", "20");

        // iprp_negcost.2da: negative cost values
        mock.Set2DAValue("iprp_negcost", 1, "Cost", "-2");

        return mock;
    }

    private static ItemProperty Prop(ushort name, ushort costValue, byte costTable, ushort subtype = 0) =>
        new() { PropertyName = name, CostValue = costValue, CostTable = costTable, Subtype = subtype, Param1 = 0xFF };

    private static UtiFile Uti(int baseItem, uint addCost = 0, params ItemProperty[] props)
    {
        var uti = new UtiFile { BaseItem = baseItem, AddCost = addCost };
        foreach (var p in props) uti.Properties.Add(p);
        return uti;
    }

    // ---- Tests --------------------------------------------------------------

    [Fact]
    public void Calculate_Unconfigured_ReturnsNull()
    {
        var calc = new ItemCostCalculator(new MockGameDataService(includeSampleData: false).AsUnconfigured());
        Assert.Null(calc.Calculate(Uti(40)));
    }

    [Fact]
    public void Calculate_NonArmorNoProperties_IsBaseCostTimesStackTimesMult_PlusAddCost()
    {
        var calc = new ItemCostCalculator(CreateMock());
        // BaseCost 10, no props, Stacking 1, Mult 1, AddCost 0 → 10
        Assert.Equal(10u, calc.Calculate(Uti(40)));
    }

    [Fact]
    public void Calculate_AddCost_IsAddedOutsideTheBracket()
    {
        var calc = new ItemCostCalculator(CreateMock());
        // (10) * 1 * 1 + 25 = 35
        Assert.Equal(35u, calc.Calculate(Uti(40, addCost: 25)));
    }

    [Fact]
    public void Calculate_StackableItem_MultipliesByStacking()
    {
        var calc = new ItemCostCalculator(CreateMock());
        // BaseCost 5 * Stacking 99 * Mult 1 = 495
        Assert.Equal(495u, calc.Calculate(Uti(41)));
    }

    [Fact]
    public void Calculate_SinglePositiveProperty_SquaresMultiplierTimes1000()
    {
        var calc = new ItemCostCalculator(CreateMock());
        // Enhancement +2 → property cost 2 (positive). Mult=2, NegMult=0.
        // [10 + 1000*(2^2 - 0)] * 1 * 1 = 10 + 4000 = 4010
        var uti = Uti(40, 0, Prop(6, costValue: 2, costTable: 2));
        Assert.Equal(4010u, calc.Calculate(uti));
    }

    [Fact]
    public void Calculate_TwoPositiveProperties_SumThenSquare()
    {
        var calc = new ItemCostCalculator(CreateMock());
        // Two enhancement-style props: cost 1 and cost 3 → Mult = 4.
        // [10 + 1000*(4^2)] = 10 + 16000 = 16010
        var uti = Uti(40, 0,
            Prop(6, costValue: 1, costTable: 2),
            Prop(6, costValue: 3, costTable: 2));
        Assert.Equal(16010u, calc.Calculate(uti));
    }

    [Fact]
    public void Calculate_NegativeProperty_SubtractsSquaredNegMultiplier()
    {
        var calc = new ItemCostCalculator(CreateMock());
        // One positive cost 3 (Mult=3) and one negative cost -2 (NegMult=2).
        // [10 + 1000*(3^2 - 2^2)] = 10 + 1000*5 = 5010
        var uti = Uti(40, 0,
            Prop(6, costValue: 3, costTable: 2),
            Prop(30, costValue: 1, costTable: 5));
        Assert.Equal(5010u, calc.Calculate(uti));
    }

    [Fact]
    public void Calculate_Armor_UsesArmorTableBaseCostFromTorsoAc()
    {
        var calc = new ItemCostCalculator(CreateMock());
        // Armor base item 16, torso part 7 → AC 5 → armor.2da[5].COST = 400.
        // No props → [400] * 1 * 1 = 400
        var uti = Uti(16);
        uti.ArmorParts["Torso"] = 7;
        Assert.Equal(400u, calc.Calculate(uti));
    }

    [Fact]
    public void Calculate_CastSpell_ExcludedFromMultiplierAndAddedAsSpellCost()
    {
        var calc = new ItemCostCalculator(CreateMock());
        // Single Cast Spell property (prop 15), subtype 0 (iprp_spells Cost = 1),
        // CostValue 2 → iprp_spellcost[2].Cost = 20.
        // PropertyCost 0 → SubtypeCost path: subtype cost = 1.
        // CastSpellCost = (PropertyCost 0 + CostValue 20) * SubtypeCost 1 = 20.
        // Single spell → most-expensive tier *100% = 20.
        // [10 + 1000*(0) + 20] * 1 * 1 = 30
        var uti = Uti(40, 0, Prop(15, costValue: 2, costTable: 3, subtype: 0));
        Assert.Equal(30u, calc.Calculate(uti));
    }

    [Fact]
    public void Calculate_AcBonus_PropertyCostMultipliesCostValue_MatchesAuroraToolset()
    {
        var mock = CreateMock();
        // Reproduce the real studded-leather +2 AC case verified against the Aurora toolset:
        // armor base item 16, torso → AC 3 → armor.2da[3].COST = 15.
        mock.Set2DAValue("parts_chest", 4, "ACBONUS", "3");
        mock.Set2DAValue("armor", 3, "COST", "15");
        var calc = new ItemCostCalculator(mock);

        var uti = Uti(16, 0, Prop(1, costValue: 2, costTable: 9));
        uti.ArmorParts["Torso"] = 4;

        // PropertyCost 0.9 × CostValue 1.9 = 1.71 → [15 + 1000*(1.71^2)] = 15 + 2924 = 2939.
        Assert.Equal(2939u, calc.Calculate(uti));
    }

    [Fact]
    public void Calculate_WeaponEnhancement5_MatchesAuroraToolset()
    {
        var calc = new ItemCostCalculator(CreateMock());
        // Longsword (base item 1, BaseCost 15) + Enhancement +5 (prop 6, costVal 5 → 4.9).
        // PropertyCost 1 × 4.9 = 4.9 → [15 + 1000*4.9²] = 15 + 24010 = 48050 (toolset-verified).
        var uti = Uti(1, 0, Prop(6, costValue: 5, costTable: 2));
        Assert.Equal(48050u, calc.Calculate(uti));
    }

    [Fact]
    public void Calculate_MultipleProperties_SumThenSquare_MatchesAuroraToolset()
    {
        var mock = CreateMock();
        // Real enhancement +2 magnitude is 1.9 (the base mock uses round 2 elsewhere).
        mock.Set2DAValue("iprp_bonuscost", 2, "Cost", "1.9");
        var calc = new ItemCostCalculator(mock);
        // Dagger (base item 0, BaseCost 10, ItemMultiplier 2) + Enhancement +2 (1×1.9=1.9)
        // + Damage 5 (3.5×1.0=3.5). Mult = 5.4 → [10 + 1000*5.4²]*2 = 29170*2 = 58340 (toolset).
        var uti = Uti(0, 0,
            Prop(6, costValue: 2, costTable: 2),
            Prop(16, costValue: 5, costTable: 4));
        Assert.Equal(58340u, calc.Calculate(uti));
    }

    [Fact]
    public void Calculate_SubtypeCostUsedWhenPropertyCostIsZero()
    {
        var mock = CreateMock();
        // Ability bonus (prop 0): PropertyCost 0 → fall back to subtype cost.
        // iprp_abilities row 2 Cost = 4 (SubtypeCost). CostValue from bonuscost.
        mock.Set2DAValue("iprp_abilities", 2, "Cost", "4");
        var calc = new ItemCostCalculator(mock);
        // prop 0, subtype 2 (subtypecost 4), costValue 1 (bonuscost 1) → property cost 4 + 1 = 5
        // [10 + 1000*(5^2)] = 10 + 25000 = 25010
        var uti = Uti(40, 0, Prop(0, costValue: 1, costTable: 2, subtype: 2));
        Assert.Equal(25010u, calc.Calculate(uti));
    }
}
