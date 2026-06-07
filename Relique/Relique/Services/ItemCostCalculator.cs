using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;

namespace ItemEditor.Services;

/// <summary>
/// Computes an item's gold cost using the Aurora engine formula (wiki Ch4 §4.4):
///
///   ItemCost = [BaseCost + 1000*(Multiplier^2 - NegMultiplier^2) + SpellCosts]
///              * MaxStack * BaseMult + AddCost
///
/// The engine recomputes Cost on load, so the stored UTI value is advisory only.
/// This calculator reproduces the engine result so the editor can display and save
/// the value the game will actually use. Returns null when game data is unavailable,
/// in which case the caller should keep the stored value (#2235).
/// </summary>
public class ItemCostCalculator
{
    private const int CastSpellPropertyName = 15;

    private readonly IGameDataService _gameData;

    public ItemCostCalculator(IGameDataService gameData)
    {
        _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
    }

    /// <summary>
    /// Compute the item cost. Returns null if game data is not configured or the
    /// base item row is missing, so the caller can fall back to the stored value.
    /// </summary>
    public uint? Calculate(UtiFile uti)
    {
        if (uti == null) throw new ArgumentNullException(nameof(uti));
        if (!_gameData.IsConfigured) return null;

        var baseItems = _gameData.Get2DA("baseitems");
        if (baseItems == null || uti.BaseItem < 0 || uti.BaseItem >= baseItems.RowCount)
            return null;

        var modelType = GetInt(baseItems.GetValue(uti.BaseItem, "ModelType"), 0);
        var isArmor = modelType == 3;

        double baseCost = isArmor
            ? ResolveArmorBaseCost(uti)
            : GetDouble(baseItems.GetValue(uti.BaseItem, "BaseCost"), 0);

        double maxStack = GetDouble(baseItems.GetValue(uti.BaseItem, "Stacking"), 1);
        if (maxStack < 1) maxStack = 1;
        double baseMult = GetDouble(baseItems.GetValue(uti.BaseItem, "ItemMultiplier"), 1);
        if (baseMult <= 0) baseMult = 1;

        double multiplier = 0;     // sum of positive non-spell property costs
        double negMultiplier = 0;  // sum of |negative| non-spell property costs
        var spellCosts = new List<double>();

        foreach (var prop in uti.Properties)
        {
            if (prop.PropertyName == CastSpellPropertyName)
            {
                spellCosts.Add(CalculateCastSpellCost(prop));
                continue;
            }

            double cost = CalculatePropertyCost(prop);
            if (cost > 0) multiplier += cost;
            else if (cost < 0) negMultiplier += -cost;
        }

        double spellTotal = AggregateSpellCosts(spellCosts);

        double bracket = baseCost
            + 1000.0 * (multiplier * multiplier - negMultiplier * negMultiplier)
            + spellTotal;

        double total = bracket * maxStack * baseMult + uti.AddCost;
        if (total < 0) total = 0;
        return (uint)Math.Round(total, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Armor BaseCost = armor.2da[baseAC].COST, where baseAC = parts_chest.2da[Torso].ACBONUS.
    /// </summary>
    private double ResolveArmorBaseCost(UtiFile uti)
    {
        var torsoPart = uti.ArmorParts.TryGetValue("Torso", out var t) ? t : (byte)0;

        var acBonusStr = _gameData.Get2DAValue("parts_chest", torsoPart, "ACBONUS");
        var baseAc = GetInt(acBonusStr, 0);

        var costStr = _gameData.Get2DAValue("armor", baseAc, "COST");
        return GetDouble(costStr, 0);
    }

    /// <summary>
    /// Per-property cost. The BioWare doc (§4.4.2) describes this additively
    /// (PropertyCost + SubtypeCost + CostValue), but NWN:EE actually treats the
    /// itempropdef.2da <c>Cost</c> column as a <b>multiplier</b> on the property's
    /// magnitude, not an additive term. Verified against the Aurora toolset:
    /// studded leather +2 AC → PropertyCost 0.9 × CostValue 1.9 = 1.71, and
    /// 15 + 1000·1.71² = 2939 (matches the toolset; the additive reading gave 7855).
    ///
    /// Model: magnitude = CostValue (+ SubtypeCost only when there is no PropertyCost
    /// multiplier); cost = PropertyCost × magnitude, treating a missing/zero
    /// PropertyCost as a multiplier of 1 so magnitude-only properties still count.
    /// </summary>
    private double CalculatePropertyCost(ItemProperty prop)
    {
        double propertyCost = GetPropertyCost(prop.PropertyName);
        double costValue = GetCostValue(prop);
        double subtypeCost = propertyCost == 0 ? GetSubtypeCost(prop) : 0;

        double magnitude = costValue + subtypeCost;
        double multiplier = propertyCost == 0 ? 1.0 : propertyCost;
        return multiplier * magnitude;
    }

    /// <summary>
    /// Cast Spell cost = (PropertyCost + CostValue) * SubtypeCost (§4.4.3).
    /// </summary>
    private double CalculateCastSpellCost(ItemProperty prop)
    {
        double propertyCost = GetPropertyCost(prop.PropertyName);
        double subtypeCost = GetSubtypeCost(prop);
        double costValue = GetCostValue(prop);
        return (propertyCost + costValue) * subtypeCost;
    }

    /// <summary>
    /// Tier the accumulated cast-spell costs: most expensive 100%, second 75%, rest 50%.
    /// </summary>
    private static double AggregateSpellCosts(List<double> spellCosts)
    {
        if (spellCosts.Count == 0) return 0;

        var ordered = spellCosts.OrderByDescending(c => c).ToList();
        double total = 0;
        for (int i = 0; i < ordered.Count; i++)
        {
            double factor = i == 0 ? 1.0 : i == 1 ? 0.75 : 0.5;
            total += ordered[i] * factor;
        }
        return total;
    }

    /// <summary>PropertyCost: itempropdef.2da[PropertyName].Cost (**** → 0).</summary>
    private double GetPropertyCost(int propertyName)
    {
        var propDef = _gameData.Get2DA("itempropdef");
        if (propDef == null || propertyName < 0 || propertyName >= propDef.RowCount)
            return 0;
        return GetDouble(propDef.GetValue(propertyName, "Cost"), 0);
    }

    /// <summary>SubtypeCost: subtype 2da[Subtype].Cost via itempropdef SubTypeResRef.</summary>
    private double GetSubtypeCost(ItemProperty prop)
    {
        var propDef = _gameData.Get2DA("itempropdef");
        if (propDef == null || prop.PropertyName >= propDef.RowCount)
            return 0;

        var subtypeResRef = propDef.GetValue(prop.PropertyName, "SubTypeResRef");
        if (!IsValid(subtypeResRef))
            return 0;

        return GetDouble(_gameData.Get2DAValue(subtypeResRef!, prop.Subtype, "Cost"), 0);
    }

    /// <summary>
    /// CostValue: iprp_costtable.2da[CostTable].Name → cost 2da[CostValue].Cost.
    /// </summary>
    private double GetCostValue(ItemProperty prop)
    {
        var costTableResRef = _gameData.Get2DAValue("iprp_costtable", prop.CostTable, "Name");
        if (!IsValid(costTableResRef))
            return 0;

        return GetDouble(_gameData.Get2DAValue(costTableResRef!, prop.CostValue, "Cost"), 0);
    }

    private static bool IsValid(string? value) => !string.IsNullOrEmpty(value) && value != "****";

    private static int GetInt(string? value, int fallback)
    {
        if (!IsValid(value)) return fallback;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r) ? r : fallback;
    }

    private static double GetDouble(string? value, double fallback)
    {
        if (!IsValid(value)) return fallback;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : fallback;
    }
}
