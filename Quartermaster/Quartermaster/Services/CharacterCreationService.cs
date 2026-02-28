using System;
using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Bic;
using Radoub.Formats.Gff;
using Radoub.Formats.Services;
using Radoub.Formats.Utc;

namespace Quartermaster.Services;

/// <summary>
/// Handles character creation logic extracted from NewCharacterWizardWindow.
/// Builds UtcFile/BicFile from wizard selections without UI dependencies.
/// </summary>
public class CharacterCreationService
{
    private readonly CreatureDisplayService _displayService;
    private readonly IGameDataService _gameDataService;

    public CharacterCreationService(CreatureDisplayService displayService, IGameDataService gameDataService)
    {
        ArgumentNullException.ThrowIfNull(displayService);
        ArgumentNullException.ThrowIfNull(gameDataService);
        _displayService = displayService;
        _gameDataService = gameDataService;
    }

    /// <summary>
    /// Complete set of inputs needed to create a character.
    /// </summary>
    public class CharacterCreationInput
    {
        // Step 1
        public bool IsBicFile { get; set; }
        public bool ApplyDefaultScripts { get; set; }

        // Step 2
        public byte RaceId { get; set; }
        public byte Gender { get; set; }

        // Step 3
        public ushort AppearanceId { get; set; }
        public int Phenotype { get; set; }
        public ushort PortraitId { get; set; } = 1;
        public BodyPartVariations BodyParts { get; set; } = new();
        public ColorSelections Colors { get; set; } = new();

        // Step 4
        public int ClassId { get; set; }
        public byte PackageId { get; set; } = 255;
        public byte GoodEvil { get; set; } = 50;
        public byte LawChaos { get; set; } = 50;
        public int Domain1 { get; set; }
        public int Domain2 { get; set; }
        public int FamiliarType { get; set; }

        // Step 5
        public Dictionary<string, int> AbilityBaseScores { get; set; } = new()
        {
            { "STR", 8 }, { "DEX", 8 }, { "CON", 8 },
            { "INT", 8 }, { "WIS", 8 }, { "CHA", 8 }
        };

        // Step 6
        public List<int> ChosenFeatIds { get; set; } = new();

        // Step 7
        public Dictionary<int, int> SkillRanksAllocated { get; set; } = new();

        // Step 8
        public Dictionary<int, List<int>> SelectedSpellsByLevel { get; set; } = new();
        public bool NeedsSpellSelection { get; set; }
        public bool IsDivineCaster { get; set; }

        // Step 9
        public List<EquipmentItem> EquipmentItems { get; set; } = new();

        // Step 10
        public string CharacterName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Description { get; set; } = "";
        public byte PaletteId { get; set; } = 1;
        public ushort FactionId { get; set; } = 1;
        public ushort VoiceSetId { get; set; }
        public int Age { get; set; } = 25;
    }

    /// <summary>
    /// Body part variations for character appearance.
    /// </summary>
    public class BodyPartVariations
    {
        public byte Head { get; set; } = 1;
        public byte Neck { get; set; } = 1;
        public byte Torso { get; set; } = 1;
        public byte Pelvis { get; set; } = 1;
        public byte Belt { get; set; }
        public byte LShoulder { get; set; }
        public byte RShoulder { get; set; }
        public byte LBicep { get; set; } = 1;
        public byte RBicep { get; set; } = 1;
        public byte LForearm { get; set; } = 1;
        public byte RForearm { get; set; } = 1;
        public byte LHand { get; set; } = 1;
        public byte RHand { get; set; } = 1;
        public byte LThigh { get; set; } = 1;
        public byte RThigh { get; set; } = 1;
        public byte LShin { get; set; } = 1;
        public byte RShin { get; set; } = 1;
        public byte LFoot { get; set; } = 1;
        public byte RFoot { get; set; } = 1;
    }

    /// <summary>
    /// Color selections for character appearance.
    /// </summary>
    public class ColorSelections
    {
        public byte Skin { get; set; }
        public byte Hair { get; set; }
        public byte Tattoo1 { get; set; }
        public byte Tattoo2 { get; set; }
    }

    /// <summary>
    /// An equipment item with ResRef and slot information.
    /// </summary>
    public class EquipmentItem
    {
        public string ResRef { get; set; } = "";
        public string Name { get; set; } = "";
        public int SlotFlags { get; set; }
    }

    /// <summary>
    /// Builds a UtcFile (or BicFile) from complete character creation input.
    /// </summary>
    public UtcFile BuildCreature(CharacterCreationInput input)
    {
        int classId = input.ClassId >= 0 ? input.ClassId : 255;
        int hitDie = input.ClassId >= 0
            ? _displayService.Classes.GetClassMetadata(input.ClassId).HitDie
            : 4;

        int conTotal = input.AbilityBaseScores.GetValueOrDefault("CON", 8) +
                       _displayService.GetRacialModifier(input.RaceId, "CON");
        int hp = Math.Max(1, hitDie + CreatureDisplayService.CalculateAbilityBonus(conTotal));

        var saves = input.ClassId >= 0
            ? _displayService.GetClassSaves(input.ClassId, 1)
            : new SavingThrows();

        var sanitized = SanitizeForResRef(input.CharacterName);
        var tag = string.IsNullOrEmpty(sanitized) ? "new_creature" : sanitized;

        // Build class entry with spell data
        var creatureClass = new CreatureClass
        {
            Class = classId,
            ClassLevel = 1,
            Domain1 = (byte)input.Domain1,
            Domain2 = (byte)input.Domain2
        };
        PopulateClassSpells(creatureClass, classId, input);

        var equipmentLists = BuildEquipmentLists(input.EquipmentItems);

        var creature = new UtcFile
        {
            FirstName = BuildLocString(input.CharacterName),
            LastName = BuildLocString(input.LastName),
            Tag = tag,
            TemplateResRef = tag,
            Description = BuildLocString(input.Description),
            PaletteID = input.IsBicFile ? (byte)0 : input.PaletteId,

            Race = input.RaceId,
            Gender = input.Gender,

            AppearanceType = input.AppearanceId > 0 ? input.AppearanceId : GetDefaultAppearanceForRace(input.RaceId),
            Phenotype = input.Phenotype,
            PortraitId = input.PortraitId,

            AppearanceHead = input.BodyParts.Head,
            BodyPart_Neck = input.BodyParts.Neck,
            BodyPart_Torso = input.BodyParts.Torso,
            BodyPart_Pelvis = input.BodyParts.Pelvis,
            BodyPart_Belt = input.BodyParts.Belt,
            BodyPart_LShoul = input.BodyParts.LShoulder,
            BodyPart_RShoul = input.BodyParts.RShoulder,
            BodyPart_LBicep = input.BodyParts.LBicep,
            BodyPart_RBicep = input.BodyParts.RBicep,
            BodyPart_LFArm = input.BodyParts.LForearm,
            BodyPart_RFArm = input.BodyParts.RForearm,
            BodyPart_LHand = input.BodyParts.LHand,
            BodyPart_RHand = input.BodyParts.RHand,
            BodyPart_LThigh = input.BodyParts.LThigh,
            BodyPart_RThigh = input.BodyParts.RThigh,
            BodyPart_LShin = input.BodyParts.LShin,
            BodyPart_RShin = input.BodyParts.RShin,
            BodyPart_LFoot = input.BodyParts.LFoot,
            BodyPart_RFoot = input.BodyParts.RFoot,

            Color_Skin = input.Colors.Skin,
            Color_Hair = input.Colors.Hair,
            Color_Tattoo1 = input.Colors.Tattoo1,
            Color_Tattoo2 = input.Colors.Tattoo2,

            Str = (byte)input.AbilityBaseScores.GetValueOrDefault("STR", 8),
            Dex = (byte)input.AbilityBaseScores.GetValueOrDefault("DEX", 8),
            Con = (byte)input.AbilityBaseScores.GetValueOrDefault("CON", 8),
            Int = (byte)input.AbilityBaseScores.GetValueOrDefault("INT", 8),
            Wis = (byte)input.AbilityBaseScores.GetValueOrDefault("WIS", 8),
            Cha = (byte)input.AbilityBaseScores.GetValueOrDefault("CHA", 8),

            HitPoints = (short)hp,
            CurrentHitPoints = (short)hp,
            MaxHitPoints = (short)hp,

            FortBonus = (short)saves.Fortitude,
            RefBonus = (short)saves.Reflex,
            WillBonus = (short)saves.Will,

            GoodEvil = input.GoodEvil,
            LawfulChaotic = input.LawChaos,

            SoundSetFile = input.VoiceSetId,
            FactionID = input.FactionId,
            PerceptionRange = 11,
            WalkRate = 4,
            DecayTime = 5000,
            Interruptable = true,

            StartingPackage = input.PackageId != 255 ? input.PackageId : (byte)0,
            FamiliarType = input.FamiliarType,

            ClassList = new List<CreatureClass> { creatureClass },
            FeatList = GetAllFeatIdsForCreature(input).Select(id => (ushort)id).ToList(),
            SkillList = BuildSkillListForCreature(input.SkillRanksAllocated),
            SpecAbilityList = new List<SpecialAbility>(),
            ItemList = equipmentLists.Backpack,
            EquipItemList = equipmentLists.Equipped
        };

        if (!input.IsBicFile && input.ApplyDefaultScripts)
            ApplyDefaultScripts(creature);

        if (input.IsBicFile)
        {
            var bicFile = BicFile.FromUtcFile(creature);
            bicFile.Age = input.Age;
            return bicFile;
        }

        return creature;
    }

    /// <summary>
    /// Gets all feat IDs for a new creature (granted + player-chosen).
    /// </summary>
    public HashSet<int> GetAllFeatIdsForCreature(CharacterCreationInput input)
    {
        var all = GetGrantedFeatIds(input.RaceId, input.ClassId);
        foreach (var featId in input.ChosenFeatIds)
            all.Add(featId);
        return all;
    }

    /// <summary>
    /// Gets the set of feat IDs automatically granted to a new level-1 character.
    /// Combines racial feats and class feats granted at level 1.
    /// </summary>
    public HashSet<int> GetGrantedFeatIds(byte raceId, int classId)
    {
        int resolvedClassId = classId >= 0 ? classId : 0;
        var racialFeats = _displayService.Feats.GetRaceGrantedFeatIds(raceId);
        var classFeats = _displayService.Feats.GetClassFeatsGrantedAtLevel(resolvedClassId, 1);

        var combined = new HashSet<int>(racialFeats);
        combined.UnionWith(classFeats);
        return combined;
    }

    /// <summary>
    /// Calculates the HP for a level-1 character.
    /// HP = max(1, hitDie + CON modifier after racial adjustment).
    /// </summary>
    public int CalculateLevel1HP(int classId, byte raceId, int conBaseScore)
    {
        int hitDie = classId >= 0
            ? _displayService.Classes.GetClassMetadata(classId).HitDie
            : 4;
        int conTotal = conBaseScore + _displayService.GetRacialModifier(raceId, "CON");
        return Math.Max(1, hitDie + CreatureDisplayService.CalculateAbilityBonus(conTotal));
    }

    /// <summary>
    /// Calculates skill points for a level-1 character.
    /// D&amp;D 3.5/NWN rule: (basePoints + intMod) * 4 at level 1, plus racial bonus * 4.
    /// </summary>
    public int CalculateLevel1SkillPoints(int classId, byte raceId, int intBaseScore)
    {
        const int FirstLevelSkillMultiplier = 4;
        int resolvedClassId = classId >= 0 ? classId : 0;

        int intTotal = intBaseScore + _displayService.GetRacialModifier(raceId, "INT");
        int intMod = CreatureDisplayService.CalculateAbilityBonus(intTotal);
        int basePoints = _displayService.GetClassSkillPointBase(resolvedClassId);
        int total = Math.Max(1, basePoints + intMod) * FirstLevelSkillMultiplier;

        int racialExtra = _displayService.GetRacialExtraSkillPointsPerLevel(raceId);
        if (racialExtra > 0)
            total += racialExtra * FirstLevelSkillMultiplier;

        return total;
    }

    /// <summary>
    /// Calculates the max ranks for a skill at level 1.
    /// Class skill: 4, Cross-class: 2.
    /// </summary>
    public static int CalculateMaxSkillRanks(bool isClassSkill, int characterLevel = 1)
    {
        return isClassSkill
            ? characterLevel + 3
            : (characterLevel + 3) / 2;
    }

    /// <summary>
    /// Sanitizes a character name for use as an Aurora Engine ResRef.
    /// Lowercase, alphanumeric + underscore only, max 16 characters.
    /// </summary>
    public static string SanitizeForResRef(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        var sanitized = name.ToLowerInvariant().Replace(' ', '_');
        var chars = sanitized.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray();
        var result = new string(chars);

        if (result.Length > 16)
            result = result[..16];

        result = result.TrimEnd('_');
        return result;
    }

    /// <summary>
    /// Gets a human-readable alignment name from Good/Evil and Law/Chaos values.
    /// </summary>
    public static string GetAlignmentName(byte goodEvil, byte lawChaos)
    {
        string geAxis = goodEvil > 70 ? "Good" : goodEvil < 30 ? "Evil" : "Neutral";
        string lcAxis = lawChaos > 70 ? "Lawful" : lawChaos < 30 ? "Chaotic" : "Neutral";

        if (geAxis == "Neutral" && lcAxis == "Neutral")
            return "True Neutral";

        return $"{lcAxis} {geAxis}";
    }

    /// <summary>
    /// Applies default NWN creature scripts to a UTC file.
    /// </summary>
    public static void ApplyDefaultScripts(UtcFile utc)
    {
        utc.ScriptAttacked = "nw_c2_default5";
        utc.ScriptDamaged = "nw_c2_default6";
        utc.ScriptDeath = "nw_c2_default7";
        utc.ScriptDialogue = "nw_c2_default4";
        utc.ScriptDisturbed = "nw_c2_default8";
        utc.ScriptEndRound = "nw_c2_default3";
        utc.ScriptHeartbeat = "nw_c2_default1";
        utc.ScriptOnBlocked = "nw_c2_defaulte";
        utc.ScriptOnNotice = "nw_c2_default2";
        utc.ScriptRested = "nw_c2_defaulta";
        utc.ScriptSpawn = "nw_c2_default9";
        utc.ScriptSpellAt = "nw_c2_defaultb";
        utc.ScriptUserDefine = "nw_c2_defaultd";
    }

    /// <summary>
    /// Splits equipment into equipped items and backpack items.
    /// </summary>
    public static (List<EquippedItem> Equipped, List<InventoryItem> Backpack) BuildEquipmentLists(
        List<EquipmentItem> equipmentItems)
    {
        var equipped = new List<EquippedItem>();
        var backpack = new List<InventoryItem>();
        var usedSlots = new HashSet<int>();
        ushort posX = 0;
        ushort posY = 0;

        foreach (var equip in equipmentItems)
        {
            int assignedSlot = 0;

            if (equip.SlotFlags != 0)
            {
                for (int bit = 0; bit < 14; bit++)
                {
                    int slotBit = 1 << bit;
                    if ((equip.SlotFlags & slotBit) != 0 && !usedSlots.Contains(slotBit))
                    {
                        assignedSlot = slotBit;
                        break;
                    }
                }
            }

            if (assignedSlot != 0)
            {
                usedSlots.Add(assignedSlot);
                equipped.Add(new EquippedItem
                {
                    Slot = assignedSlot,
                    EquipRes = equip.ResRef
                });
            }
            else
            {
                backpack.Add(new InventoryItem
                {
                    InventoryRes = equip.ResRef,
                    Repos_PosX = posX,
                    Repos_PosY = posY,
                    Dropable = true,
                    Pickpocketable = false
                });

                posX++;
                if (posX >= 4)
                {
                    posX = 0;
                    posY++;
                }
            }
        }

        return (equipped, backpack);
    }

    private void PopulateClassSpells(CreatureClass creatureClass, int classId, CharacterCreationInput input)
    {
        if (!input.NeedsSpellSelection || input.IsDivineCaster)
            return;

        foreach (var (spellLevel, spellIds) in input.SelectedSpellsByLevel)
        {
            if (spellLevel < 0 || spellLevel >= 10) continue;

            foreach (var spellId in spellIds)
            {
                creatureClass.KnownSpells[spellLevel].Add(new KnownSpell
                {
                    Spell = (ushort)spellId,
                    SpellFlags = 0x01,
                    SpellMetaMagic = 0
                });
            }
        }
    }

    private List<byte> BuildSkillListForCreature(Dictionary<int, int> skillRanksAllocated)
    {
        var skills = new List<byte>();
        int skillCount = _displayService.GetSkillCount();
        for (int i = 0; i < skillCount; i++)
        {
            skills.Add((byte)skillRanksAllocated.GetValueOrDefault(i, 0));
        }
        return skills;
    }

    private static CExoLocString BuildLocString(string text)
    {
        var locString = new CExoLocString { StrRef = 0xFFFFFFFF };
        if (!string.IsNullOrEmpty(text?.Trim()))
        {
            locString.LocalizedStrings[0] = text.Trim();
        }
        return locString;
    }

    private ushort GetDefaultAppearanceForRace(byte raceId)
    {
        var appStr = _gameDataService.Get2DAValue("racialtypes", raceId, "Appearance");
        if (!string.IsNullOrEmpty(appStr) && appStr != "****" && ushort.TryParse(appStr, out ushort appId))
            return appId;
        return 6; // Human fallback
    }
}
