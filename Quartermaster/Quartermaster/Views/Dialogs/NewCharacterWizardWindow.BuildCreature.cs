namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Display item model classes used by the wizard UI.
/// Creature construction logic has been moved to CharacterCreationService.BuildCreature().
/// </summary>
public partial class NewCharacterWizardWindow
{
    /// <summary>
    /// Gets a default appearance ID for a race by reading racialtypes.2da Appearance column.
    /// </summary>
    private ushort GetDefaultAppearanceForRace(byte raceId)
    {
        var appStr = _gameDataService.Get2DAValue("racialtypes", raceId, "Appearance");
        if (!string.IsNullOrEmpty(appStr) && appStr != "****" && ushort.TryParse(appStr, out ushort appId))
            return appId;
        return 6; // Human fallback
    }

    #region Display Items

    private class RaceDisplayItem
    {
        public byte Id { get; init; }
        public string Name { get; init; } = "";
    }

    private class ClassDisplayItem
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public bool IsFavored { get; init; }
    }

    private class PackageDisplayItem
    {
        public byte Id { get; init; }
        public string Name { get; init; } = "";
    }

    private class SkillDisplayItem
    {
        public int SkillId { get; set; }
        public string Name { get; set; } = "";
        public string KeyAbility { get; set; } = "";
        public bool IsClassSkill { get; set; }
        public bool IsUnavailable { get; set; }
        public int MaxRanks { get; set; }
        public int AllocatedRanks { get; set; }
        public int Cost { get; set; } = 1;
    }

    private class SpellDisplayItem
    {
        public int SpellId { get; set; }
        public string Name { get; set; } = "";
        public string SchoolAbbrev { get; set; } = "";
    }

    private class FeatDisplayItem
    {
        public int FeatId { get; set; }
        public string Name { get; set; } = "";
        public string CategoryAbbrev { get; set; } = "";
        public bool IsGranted { get; set; }
        public bool MeetsPrereqs { get; set; } = true;
        public string SourceLabel { get; set; } = "";
    }

    private class EquipmentDisplayItem
    {
        public string ResRef { get; set; } = "";
        public string Name { get; set; } = "";
        public string SlotName { get; set; } = "";
        public int SlotFlags { get; set; } // Raw EquipableSlots bit flags from baseitems.2da
    }

    private class DomainDisplayItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public override string ToString() => Name;
    }

    #endregion
}
