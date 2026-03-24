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

    private class RaceDisplayItem : Services.SkillDisplayHelper.INamedItem
    {
        public byte Id { get; init; }
        public string Name { get; init; } = "";
    }

    private class ClassDisplayItem : Services.SkillDisplayHelper.INamedItem
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

    // SkillDisplayItem and SpellDisplayItem moved to Quartermaster.Models.WizardDisplayItems (#1798)

    private class FeatDisplayItem : Services.SkillDisplayHelper.INamedItem
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
