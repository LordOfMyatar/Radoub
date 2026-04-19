using Radoub.Formats.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ItemEditor.Services;

/// <summary>
/// Maps itempropdef.2da Labels to curated display categories.
/// Unmapped labels (custom content: CEP, PRC, etc.) fall into "Other".
/// </summary>
public sealed class PropertyCategoryService
{
    public const string OtherCategory = "Other";

    private static readonly string[] DisplayOrder =
    {
        "Bonus/Enhancement",
        "Damage",
        "Defense/AC",
        "On Hit",
        "Cast Spell",
        "Penalty/Decreased",
        "Skill/Ability",
        "Use Limitation",
        "Miscellaneous",
        OtherCategory,
    };

    private static readonly Dictionary<string, string> LabelToCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        // Bonus/Enhancement
        { "Enhancement", "Bonus/Enhancement" },
        { "EnhancementAlignmentGroup", "Bonus/Enhancement" },
        { "EnhancementRacialGroup", "Bonus/Enhancement" },
        { "EnhancementSpecificAlignment", "Bonus/Enhancement" },
        { "AttackBonus", "Bonus/Enhancement" },
        { "AttackBonusAlignmentGroup", "Bonus/Enhancement" },
        { "AttackBonusRacialGroup", "Bonus/Enhancement" },
        { "AttackBonusSpecificAlignment", "Bonus/Enhancement" },
        { "Ability", "Bonus/Enhancement" },
        { "Keen", "Bonus/Enhancement" },
        { "Mighty", "Bonus/Enhancement" },

        // Damage
        { "Damage", "Damage" },
        { "DamageAlignmentGroup", "Damage" },
        { "DamageRacialGroup", "Damage" },
        { "DamageSpecificAlignment", "Damage" },
        { "DamageMelee", "Damage" },
        { "DamageRanged", "Damage" },
        { "Massive_Criticals", "Damage" },
        { "Monster_damage", "Damage" },
        { "Wounding", "Damage" },
        { "Vorpal", "Damage" },

        // Defense/AC
        { "Armor", "Defense/AC" },
        { "ArmorAlignmentGroup", "Defense/AC" },
        { "ArmorDamageType", "Defense/AC" },
        { "ArmorRacialGroup", "Defense/AC" },
        { "ArmorSpecificAlignment", "Defense/AC" },
        { "ImprovedSavingThrows", "Defense/AC" },
        { "ImprovedSavingThrowsSpecific", "Defense/AC" },
        { "ImprovedMagicResist", "Defense/AC" },
        { "DamageImmunity", "Defense/AC" },
        { "Immunity", "Defense/AC" },
        { "Immunity_To_Spell_By_Level", "Defense/AC" },
        { "SpellImmunity_Specific", "Defense/AC" },
        { "SpellSchool_Immunity", "Defense/AC" },
        { "DamageReduced", "Defense/AC" },
        { "DamageResist", "Defense/AC" },
        { "MindBlank", "Defense/AC" },
        { "Turn_Resistance", "Defense/AC" },
        { "ImprovedEvasion", "Defense/AC" },

        // On Hit
        { "OnHit", "On Hit" },
        { "OnMonsterHit", "On Hit" },
        { "OnHitCastSpell", "On Hit" },

        // Cast Spell
        { "CastSpell", "Cast Spell" },
        { "BonusFeats", "Cast Spell" },
        { "SingleBonusSpellOfLevel", "Cast Spell" },

        // Penalty/Decreased
        { "DecreaseAbilityScore", "Penalty/Decreased" },
        { "DecreaseAC", "Penalty/Decreased" },
        { "AttackPenalty", "Penalty/Decreased" },
        { "ToHitPenalty", "Penalty/Decreased" },
        { "DamagePenalty", "Penalty/Decreased" },
        { "DamageNone", "Penalty/Decreased" },
        { "Damage_Vulnerability", "Penalty/Decreased" },
        { "ReducedSavingThrows", "Penalty/Decreased" },
        { "ReducedSpecificSavingThrow", "Penalty/Decreased" },
        { "DecreasedSkill", "Penalty/Decreased" },
        { "Value_Decrease", "Penalty/Decreased" },

        // Skill/Ability
        { "Skill", "Skill/Ability" },

        // Use Limitation
        { "UseLimitationAlignmentGroup", "Use Limitation" },
        { "UseLimitationClass", "Use Limitation" },
        { "UseLimitationGender", "Use Limitation" },
        { "UseLimitationRacial", "Use Limitation" },
        { "UseLimitationSpecificAlignment", "Use Limitation" },
        { "UseLimitationTerrain", "Use Limitation" },

        // Miscellaneous
        { "Regeneration", "Miscellaneous" },
        { "VampiricRegeneration", "Miscellaneous" },
        { "Haste", "Miscellaneous" },
        { "Darkvision", "Miscellaneous" },
        { "Light", "Miscellaneous" },
        { "True_Seeing", "Miscellaneous" },
        { "Freedom_of_Movement", "Miscellaneous" },
        { "Trap", "Miscellaneous" },
        { "Poison", "Miscellaneous" },
        { "VisualEffect", "Miscellaneous" },
        { "Weight_Increase", "Miscellaneous" },
        { "WeightReduction", "Miscellaneous" },
        { "Material", "Miscellaneous" },
        { "Quality", "Miscellaneous" },
        { "Additional_Property", "Miscellaneous" },
        { "UnlimitedAmmo", "Miscellaneous" },
        { "Special_Walk", "Miscellaneous" },
        { "Healers_Kit", "Miscellaneous" },
        { "ThievesTools", "Miscellaneous" },
        { "HolyAvenger", "Miscellaneous" },
        { "ArcaneSpellFailure", "Miscellaneous" },
        { "Boomerang", "Miscellaneous" },
        { "Dancing_Scimitar", "Miscellaneous" },
        { "DoubleStack", "Miscellaneous" },
        { "EnhancedContainer_BonusSlot", "Miscellaneous" },
        { "EnhancedContainer_Weight", "Miscellaneous" },
        { "Value_Increase", "Miscellaneous" },
    };

    private readonly HashSet<string> _loggedUnmappedLabels = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> GetCategoryNames(IEnumerable<PropertyTypeInfo> availableProperties)
    {
        var presentCategories = new HashSet<string>();
        foreach (var prop in availableProperties)
        {
            presentCategories.Add(Resolve(prop.Label));
        }

        return DisplayOrder.Where(presentCategories.Contains).ToList();
    }

    public bool IsInCategory(PropertyTypeInfo prop, string categoryName)
    {
        return string.Equals(Resolve(prop.Label), categoryName, StringComparison.Ordinal);
    }

    private string Resolve(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return OtherCategory;

        if (LabelToCategory.TryGetValue(label, out var category))
            return category;

        if (_loggedUnmappedLabels.Add(label))
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"PropertyCategoryService: unmapped Label '{label}' -> Other");
        }

        return OtherCategory;
    }
}
