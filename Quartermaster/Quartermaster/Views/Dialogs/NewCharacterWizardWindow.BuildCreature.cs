using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Quartermaster.Services;
using Radoub.Formats.Gff;
using Radoub.Formats.Bic;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Creature construction from wizard selections, plus display item model classes.
/// </summary>
public partial class NewCharacterWizardWindow
{
    #region Build Creature

    /// <summary>
    /// Builds a UtcFile from all wizard selections.
    /// Populates all fields from Steps 1-10 using 2DA data.
    /// </summary>
    private UtcFile BuildCreature()
    {
        int classId = _selectedClassId >= 0 ? _selectedClassId : 255;
        var hitDie = _selectedClassId >= 0
            ? _displayService.Classes.GetClassMetadata(_selectedClassId).HitDie
            : 4;

        int conTotal = _abilityBaseScores["CON"] + _displayService.GetRacialModifier(_selectedRaceId, "CON");
        int hp = Math.Max(1, hitDie + CreatureDisplayService.CalculateAbilityBonus(conTotal));

        // Saving throws from class progression at level 1
        var saves = _selectedClassId >= 0
            ? _displayService.GetClassSaves(_selectedClassId, 1)
            : new SavingThrows();

        // Tag/ResRef from user input
        var sanitized = SanitizeForResRef(_characterName);
        var tag = string.IsNullOrEmpty(sanitized) ? "new_creature" : sanitized;
        var resRef = tag;

        // Palette ID from step 8
        _paletteId = (_paletteIdComboBox.SelectedItem is ComboBoxItem item && item.Tag is byte id) ? id : (byte)1;

        // Build class entry with spell data
        var creatureClass = new CreatureClass
        {
            Class = classId,
            ClassLevel = 1,
            Domain1 = GetSelectedDomainId(_domain1ComboBox),
            Domain2 = GetSelectedDomainId(_domain2ComboBox)
        };

        // Populate spells on the class
        PopulateClassSpells(creatureClass, classId);

        // Build equipment lists (equipped + backpack)
        var equipmentLists = BuildEquipmentLists();

        // FirstName
        var firstName = new CExoLocString { StrRef = 0xFFFFFFFF };
        if (!string.IsNullOrEmpty(_characterName))
        {
            firstName.LocalizedStrings[0] = _characterName; // English (language 0)
        }

        // LastName
        var lastName = new CExoLocString { StrRef = 0xFFFFFFFF };
        var lastNameText = _lastNameTextBox.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(lastNameText))
        {
            lastName.LocalizedStrings[0] = lastNameText;
        }

        // Description/Biography
        var description = new CExoLocString { StrRef = 0xFFFFFFFF };
        var descText = _descriptionTextBox.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(descText))
        {
            description.LocalizedStrings[0] = descText;
        }

        var creature = new UtcFile
        {
            // Identity (Step 10)
            FirstName = firstName,
            LastName = lastName,
            Tag = tag,
            TemplateResRef = resRef,
            Description = description,
            PaletteID = _isBicFile ? (byte)0 : _paletteId,

            // Race & gender (Step 2)
            Race = _selectedRaceId,
            Gender = _selectedGender,

            // Appearance (Step 3)
            AppearanceType = _selectedAppearanceId > 0 ? _selectedAppearanceId : GetDefaultAppearanceForRace(_selectedRaceId),
            Phenotype = _selectedPhenotype,
            PortraitId = _selectedPortraitId,

            // Body parts (Step 3)
            AppearanceHead = _headVariation,
            BodyPart_Neck = _neckVariation,
            BodyPart_Torso = _torsoVariation,
            BodyPart_Pelvis = _pelvisVariation,
            BodyPart_Belt = _beltVariation,
            BodyPart_LShoul = _lShoulVariation,
            BodyPart_RShoul = _rShoulVariation,
            BodyPart_LBicep = _lBicepVariation,
            BodyPart_RBicep = _rBicepVariation,
            BodyPart_LFArm = _lFArmVariation,
            BodyPart_RFArm = _rFArmVariation,
            BodyPart_LHand = _lHandVariation,
            BodyPart_RHand = _rHandVariation,
            BodyPart_LThigh = _lThighVariation,
            BodyPart_RThigh = _rThighVariation,
            BodyPart_LShin = _lShinVariation,
            BodyPart_RShin = _rShinVariation,
            BodyPart_LFoot = _lFootVariation,
            BodyPart_RFoot = _rFootVariation,

            // Colors (Step 3)
            Color_Skin = _skinColor,
            Color_Hair = _hairColor,
            Color_Tattoo1 = _tattoo1Color,
            Color_Tattoo2 = _tattoo2Color,

            // Ability scores (Step 5) — base scores only, game applies racial mods
            Str = (byte)_abilityBaseScores["STR"],
            Dex = (byte)_abilityBaseScores["DEX"],
            Con = (byte)_abilityBaseScores["CON"],
            Int = (byte)_abilityBaseScores["INT"],
            Wis = (byte)_abilityBaseScores["WIS"],
            Cha = (byte)_abilityBaseScores["CHA"],

            // HP (hit die + CON mod at level 1)
            HitPoints = (short)hp,
            CurrentHitPoints = (short)hp,
            MaxHitPoints = (short)hp,

            // Saving throws from class (level 1)
            FortBonus = (short)saves.Fortitude,
            RefBonus = (short)saves.Reflex,
            WillBonus = (short)saves.Will,

            // Alignment (Step 4)
            GoodEvil = _selectedGoodEvil,
            LawfulChaotic = _selectedLawChaos,

            // Voice set (Step 10)
            SoundSetFile = _selectedVoiceSetId,

            // Behavior defaults
            FactionID = 1,
            PerceptionRange = 11,
            WalkRate = 4,
            DecayTime = 5000,
            Interruptable = true,

            // Starting package (Step 4)
            StartingPackage = _selectedPackageId != 255 ? _selectedPackageId : (byte)0,

            // Class (Step 4) with spells (Step 8)
            ClassList = new List<CreatureClass> { creatureClass },

            // Feats: granted (race + class) + player-chosen (Step 6)
            FeatList = GetAllFeatIdsForCreature().Select(id => (ushort)id).ToList(),

            // Skills (Step 7)
            SkillList = BuildSkillList_ForCreature(),

            // Equipment (Step 9)
            SpecAbilityList = new List<SpecialAbility>(),
            ItemList = equipmentLists.Backpack,
            EquipItemList = equipmentLists.Equipped
        };

        // Apply default NWN scripts for UTC files if option is checked
        if (!_isBicFile && _defaultScriptsCheckBox.IsChecked == true)
            ApplyDefaultScripts(creature);

        // Convert to BicFile if creating a player character
        if (_isBicFile)
        {
            var bicFile = BicFile.FromUtcFile(creature);
            bicFile.Age = (int)(_ageNumericUpDown.Value ?? 25);
            return bicFile;
        }

        return creature;
    }

    private static void ApplyDefaultScripts(UtcFile utc)
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

    private void PopulateClassSpells(CreatureClass creatureClass, int classId)
    {
        if (!_needsSpellSelection || _isDivineCaster)
            return;

        foreach (var (spellLevel, spellIds) in _selectedSpellsByLevel)
        {
            if (spellLevel < 0 || spellLevel >= 10) continue;

            foreach (var spellId in spellIds)
            {
                creatureClass.KnownSpells[spellLevel].Add(new KnownSpell
                {
                    Spell = (ushort)spellId,
                    SpellFlags = 0x01, // Readied
                    SpellMetaMagic = 0
                });
            }
        }
    }

    /// <summary>
    /// Builds the skill list for the creature, ordered by skill ID.
    /// Each byte is the number of ranks allocated to that skill.
    /// </summary>
    private List<byte> BuildSkillList_ForCreature()
    {
        var skills = new List<byte>();
        int skillCount = _displayService.GetSkillCount();
        for (int i = 0; i < skillCount; i++)
        {
            skills.Add((byte)_skillRanksAllocated.GetValueOrDefault(i, 0));
        }
        return skills;
    }

    /// <summary>
    /// Splits equipment into equipped items and backpack items.
    /// Items with valid EquipableSlots go into equipment slots (first fit wins).
    /// Items without a slot (or when the slot is already taken) go to backpack.
    /// </summary>
    private (List<EquippedItem> Equipped, List<InventoryItem> Backpack) BuildEquipmentLists()
    {
        var equipped = new List<EquippedItem>();
        var backpack = new List<InventoryItem>();
        var usedSlots = new HashSet<int>();
        ushort posX = 0;
        ushort posY = 0;

        foreach (var equip in _equipmentItems)
        {
            int assignedSlot = 0;

            if (equip.SlotFlags != 0)
            {
                // EquipableSlots is a bitmask — pick the first available slot bit
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

    /// <summary>
    /// Gets a default appearance ID for a race by reading racialtypes.2da Appearance column.
    /// </summary>
    private ushort GetDefaultAppearanceForRace(byte raceId)
    {
        var appStr = _displayService.GameDataService.Get2DAValue("racialtypes", raceId, "Appearance");
        if (!string.IsNullOrEmpty(appStr) && appStr != "****" && ushort.TryParse(appStr, out ushort appId))
            return appId;
        return 6; // Human fallback
    }

    #endregion

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

    private class SpellAutoAssignItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
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
