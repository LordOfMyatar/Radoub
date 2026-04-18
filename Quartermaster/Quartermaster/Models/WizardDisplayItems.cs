using Avalonia.Media;
using Quartermaster.Services;

namespace Quartermaster.Models;

/// <summary>
/// Shared display item classes used by both NCW and LUW wizards (#1798).
/// </summary>

/// <summary>
/// Display item for skill lists in wizard UIs.
/// Superset of properties used by both NCW and LUW.
/// LUW-specific properties (CurrentRanks, AddedRanks, NameBrush, computed properties)
/// default to safe values when unused by NCW.
/// </summary>
public class SkillDisplayItem : SkillDisplayHelper.ISortableSkill
{
    public int SkillId { get; set; }
    public string Name { get; set; } = "";
    public string KeyAbility { get; set; } = "";
    public bool IsClassSkill { get; set; }
    public bool IsUnavailable { get; set; }
    public int MaxRanks { get; set; }
    public int AllocatedRanks { get; set; }
    public int CurrentRanks { get; set; }
    public int AddedRanks { get; set; }
    public int Cost { get; set; } = 1;
    public IBrush? NameBrush { get; set; }

    public string ClassSkillIndicator => SkillDisplayHelper.GetClassSkillIndicator(IsClassSkill, IsUnavailable);
    public bool CanIncrease => !IsUnavailable && CurrentRanks + AddedRanks < MaxRanks;
    public bool CanDecrease => AddedRanks > 0;
    public double RowOpacity => IsUnavailable ? 0.4 : 1.0;
}

/// <summary>
/// Display item for spell lists in wizard UIs.
/// Used by both NCW and LUW.
/// </summary>
public class SpellDisplayItem : SkillDisplayHelper.INamedItem
{
    public int SpellId { get; set; }
    public string Name { get; set; } = "";
    public string SchoolAbbrev { get; set; } = "";

    public string DisplayName => string.IsNullOrEmpty(SchoolAbbrev) ? Name : $"{Name} [{SchoolAbbrev}]";
}
