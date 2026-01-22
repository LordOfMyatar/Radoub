using Radoub.Formats.Gff;

namespace Radoub.Formats.Utc;

/// <summary>
/// Reads UTC (Creature Blueprint) files from binary format.
/// UTC files are GFF-based with file type "UTC ".
/// Reference: BioWare Aurora Creature Format specification, neverwinter.nim
/// </summary>
public static class UtcReader
{
    /// <summary>
    /// Read a UTC file from a file path.
    /// </summary>
    public static UtcFile Read(string filePath)
    {
        var buffer = File.ReadAllBytes(filePath);
        return Read(buffer);
    }

    /// <summary>
    /// Read a UTC file from a stream.
    /// </summary>
    public static UtcFile Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Read(ms.ToArray());
    }

    /// <summary>
    /// Read a UTC file from a byte buffer.
    /// </summary>
    public static UtcFile Read(byte[] buffer)
    {
        // Parse as GFF first
        var gff = GffReader.Read(buffer);

        // Validate file type
        if (gff.FileType.TrimEnd() != "UTC")
        {
            throw new InvalidDataException(
                $"Invalid UTC file type: '{gff.FileType}' (expected 'UTC ')");
        }

        return ParseUtcFile(gff);
    }

    private static UtcFile ParseUtcFile(GffFile gff)
    {
        var root = gff.RootStruct;

        var utc = new UtcFile
        {
            FileType = gff.FileType,
            FileVersion = gff.FileVersion,

            // Blueprint fields (Table 2.2)
            TemplateResRef = root.GetFieldValue<string>("TemplateResRef", string.Empty),
            Comment = root.GetFieldValue<string>("Comment", string.Empty),
            PaletteID = root.GetFieldValue<byte>("PaletteID", 0),

            // Identity fields (Table 2.1.1)
            Tag = root.GetFieldValue<string>("Tag", string.Empty),
            Subrace = root.GetFieldValue<string>("Subrace", string.Empty),
            Deity = root.GetFieldValue<string>("Deity", string.Empty),

            // Basic info
            Race = root.GetFieldValue<byte>("Race", 0),
            Gender = root.GetFieldValue<byte>("Gender", 0),

            // Appearance
            AppearanceType = root.GetFieldValue<ushort>("Appearance_Type", 0),
            Phenotype = root.GetFieldValue<int>("Phenotype", 0),
            PortraitId = root.GetFieldValue<ushort>("PortraitId", 0),
            Portrait = root.GetFieldValue<string>("Portrait", string.Empty),
            Tail = root.GetFieldValue<byte>("Tail", 0),
            Wings = root.GetFieldValue<byte>("Wings", 0),
            BodyBag = root.GetFieldValue<byte>("BodyBag", 0),

            // Body parts (for dynamic/part-based appearances)
            AppearanceHead = root.GetFieldValue<byte>("Appearance_Head", 0),
            BodyPart_Belt = root.GetFieldValue<byte>("BodyPart_Belt", 0),
            BodyPart_LBicep = root.GetFieldValue<byte>("BodyPart_LBicep", 0),
            BodyPart_RBicep = root.GetFieldValue<byte>("BodyPart_RBicep", 0),
            BodyPart_LFArm = root.GetFieldValue<byte>("BodyPart_LFArm", 0),
            BodyPart_RFArm = root.GetFieldValue<byte>("BodyPart_RFArm", 0),
            BodyPart_LFoot = root.GetFieldValue<byte>("BodyPart_LFoot", 0),
            // Aurora Toolset bug: BodyPart_RFoot is sometimes saved as ArmorPart_RFoot
            // Fall back to ArmorPart_RFoot if BodyPart_RFoot is missing
            BodyPart_RFoot = root.GetField("BodyPart_RFoot") != null
                ? root.GetFieldValue<byte>("BodyPart_RFoot", 0)
                : root.GetFieldValue<byte>("ArmorPart_RFoot", 0),
            BodyPart_LHand = root.GetFieldValue<byte>("BodyPart_LHand", 0),
            BodyPart_RHand = root.GetFieldValue<byte>("BodyPart_RHand", 0),
            BodyPart_LShin = root.GetFieldValue<byte>("BodyPart_LShin", 0),
            BodyPart_RShin = root.GetFieldValue<byte>("BodyPart_RShin", 0),
            BodyPart_LShoul = root.GetFieldValue<byte>("BodyPart_LShoul", 0),
            BodyPart_RShoul = root.GetFieldValue<byte>("BodyPart_RShoul", 0),
            BodyPart_LThigh = root.GetFieldValue<byte>("BodyPart_LThigh", 0),
            BodyPart_RThigh = root.GetFieldValue<byte>("BodyPart_RThigh", 0),
            BodyPart_Neck = root.GetFieldValue<byte>("BodyPart_Neck", 0),
            BodyPart_Pelvis = root.GetFieldValue<byte>("BodyPart_Pelvis", 0),
            BodyPart_Torso = root.GetFieldValue<byte>("BodyPart_Torso", 0),

            // Colors (for part-based appearances)
            Color_Skin = root.GetFieldValue<byte>("Color_Skin", 0),
            Color_Hair = root.GetFieldValue<byte>("Color_Hair", 0),
            Color_Tattoo1 = root.GetFieldValue<byte>("Color_Tattoo1", 0),
            Color_Tattoo2 = root.GetFieldValue<byte>("Color_Tattoo2", 0),

            // Armor part appearance (game instance fields - from equipped armor)
            ArmorPart_Belt = root.GetFieldValue<byte>("ArmorPart_Belt", 0),
            ArmorPart_LBicep = root.GetFieldValue<byte>("ArmorPart_LBicep", 0),
            ArmorPart_RBicep = root.GetFieldValue<byte>("ArmorPart_RBicep", 0),
            ArmorPart_LFArm = root.GetFieldValue<byte>("ArmorPart_LFArm", 0),
            ArmorPart_RFArm = root.GetFieldValue<byte>("ArmorPart_RFArm", 0),
            ArmorPart_LFoot = root.GetFieldValue<byte>("ArmorPart_LFoot", 0),
            ArmorPart_RFoot = root.GetFieldValue<byte>("ArmorPart_RFoot", 0),
            ArmorPart_LHand = root.GetFieldValue<byte>("ArmorPart_LHand", 0),
            ArmorPart_RHand = root.GetFieldValue<byte>("ArmorPart_RHand", 0),
            ArmorPart_LShin = root.GetFieldValue<byte>("ArmorPart_LShin", 0),
            ArmorPart_RShin = root.GetFieldValue<byte>("ArmorPart_RShin", 0),
            ArmorPart_LShoul = root.GetFieldValue<byte>("ArmorPart_LShoul", 0),
            ArmorPart_RShoul = root.GetFieldValue<byte>("ArmorPart_RShoul", 0),
            ArmorPart_LThigh = root.GetFieldValue<byte>("ArmorPart_LThigh", 0),
            ArmorPart_RThigh = root.GetFieldValue<byte>("ArmorPart_RThigh", 0),
            ArmorPart_Neck = root.GetFieldValue<byte>("ArmorPart_Neck", 0),
            ArmorPart_Pelvis = root.GetFieldValue<byte>("ArmorPart_Pelvis", 0),
            ArmorPart_Torso = root.GetFieldValue<byte>("ArmorPart_Torso", 0),
            ArmorPart_Robe = root.GetFieldValue<byte>("ArmorPart_Robe", 0),

            // Ability scores
            Str = root.GetFieldValue<byte>("Str", 10),
            Dex = root.GetFieldValue<byte>("Dex", 10),
            Con = root.GetFieldValue<byte>("Con", 10),
            Int = root.GetFieldValue<byte>("Int", 10),
            Wis = root.GetFieldValue<byte>("Wis", 10),
            Cha = root.GetFieldValue<byte>("Cha", 10),

            // Hit points
            HitPoints = root.GetFieldValue<short>("HitPoints", 0),
            CurrentHitPoints = root.GetFieldValue<short>("CurrentHitPoints", 0),
            MaxHitPoints = root.GetFieldValue<short>("MaxHitPoints", 0),

            // Combat
            NaturalAC = root.GetFieldValue<byte>("NaturalAC", 0),
            ChallengeRating = root.GetFieldValue<float>("ChallengeRating", 0f),
            CRAdjust = root.GetFieldValue<int>("CRAdjust", 0),

            // Saving throws
            FortBonus = root.GetFieldValue<short>("fortbonus", 0),
            RefBonus = root.GetFieldValue<short>("refbonus", 0),
            WillBonus = root.GetFieldValue<short>("willbonus", 0),

            // Alignment
            GoodEvil = root.GetFieldValue<byte>("GoodEvil", 50),
            LawfulChaotic = root.GetFieldValue<byte>("LawfulChaotic", 50),

            // Flags
            Plot = root.GetFieldValue<byte>("Plot", 0) != 0,
            IsImmortal = root.GetFieldValue<byte>("IsImmortal", 0) != 0,
            NoPermDeath = root.GetFieldValue<byte>("NoPermDeath", 0) != 0,
            IsPC = root.GetFieldValue<byte>("IsPC", 0) != 0,
            Disarmable = root.GetFieldValue<byte>("Disarmable", 0) != 0,
            Lootable = root.GetFieldValue<byte>("Lootable", 0) != 0,
            Interruptable = root.GetFieldValue<byte>("Interruptable", 1) != 0,

            // Behavior
            FactionID = root.GetFieldValue<ushort>("FactionID", 0),
            PerceptionRange = root.GetFieldValue<byte>("PerceptionRange", 11),
            WalkRate = root.GetFieldValue<int>("WalkRate", 0),
            SoundSetFile = root.GetFieldValue<ushort>("SoundSetFile", 0),
            DecayTime = root.GetFieldValue<uint>("DecayTime", 5000),
            StartingPackage = root.GetFieldValue<byte>("StartingPackage", 0),

            // Conversation
            Conversation = root.GetFieldValue<string>("Conversation", string.Empty),

            // Scripts
            ScriptAttacked = root.GetFieldValue<string>("ScriptAttacked", string.Empty),
            ScriptDamaged = root.GetFieldValue<string>("ScriptDamaged", string.Empty),
            ScriptDeath = root.GetFieldValue<string>("ScriptDeath", string.Empty),
            ScriptDialogue = root.GetFieldValue<string>("ScriptDialogue", string.Empty),
            ScriptDisturbed = root.GetFieldValue<string>("ScriptDisturbed", string.Empty),
            ScriptEndRound = root.GetFieldValue<string>("ScriptEndRound", string.Empty),
            ScriptHeartbeat = root.GetFieldValue<string>("ScriptHeartbeat", string.Empty),
            ScriptOnBlocked = root.GetFieldValue<string>("ScriptOnBlocked", string.Empty),
            ScriptOnNotice = root.GetFieldValue<string>("ScriptOnNotice", string.Empty),
            ScriptRested = root.GetFieldValue<string>("ScriptRested", string.Empty),
            ScriptSpawn = root.GetFieldValue<string>("ScriptSpawn", string.Empty),
            ScriptSpellAt = root.GetFieldValue<string>("ScriptSpellAt", string.Empty),
            ScriptUserDefine = root.GetFieldValue<string>("ScriptuserDefine", string.Empty)
        };

        // Localized strings
        utc.FirstName = ParseLocString(root, "FirstName") ?? new CExoLocString();
        utc.LastName = ParseLocString(root, "LastName") ?? new CExoLocString();
        utc.Description = ParseLocString(root, "Description") ?? new CExoLocString();

        // Lists
        ParseClassList(root, utc);
        ParseFeatList(root, utc);
        ParseSkillList(root, utc);
        ParseSpecAbilityList(root, utc);
        ParseItemList(root, utc);
        ParseEquipItemList(root, utc);

        // VarTable (local variables)
        utc.VarTable = VarTableHelper.ReadVarTable(root);

        return utc;
    }

    private static CExoLocString? ParseLocString(GffStruct root, string fieldName)
    {
        var field = root.GetField(fieldName);
        if (field == null || !field.IsCExoLocString || field.Value is not CExoLocString locString)
            return null;

        return locString;
    }

    private static void ParseClassList(GffStruct root, UtcFile utc)
    {
        var classListField = root.GetField("ClassList");
        if (classListField == null || !classListField.IsList || classListField.Value is not GffList classList)
            return;

        foreach (var classStruct in classList.Elements)
        {
            var creatureClass = new CreatureClass
            {
                Class = classStruct.GetFieldValue<int>("Class", 0),
                ClassLevel = classStruct.GetFieldValue<short>("ClassLevel", 1)
            };

            // Parse known spells (KnownList0-9) - used by Bards, Sorcerers, PC Wizards
            for (int level = 0; level < 10; level++)
            {
                ParseKnownSpellList(classStruct, creatureClass, level);
            }

            // Parse memorized spells (MemorizedList0-9) - used by Wizards, Clerics, etc.
            for (int level = 0; level < 10; level++)
            {
                ParseMemorizedSpellList(classStruct, creatureClass, level);
            }

            utc.ClassList.Add(creatureClass);
        }
    }

    private static void ParseKnownSpellList(GffStruct classStruct, CreatureClass creatureClass, int spellLevel)
    {
        var fieldName = $"KnownList{spellLevel}";
        var listField = classStruct.GetField(fieldName);
        if (listField == null || !listField.IsList || listField.Value is not GffList spellList)
            return;

        foreach (var spellStruct in spellList.Elements)
        {
            var knownSpell = new KnownSpell
            {
                Spell = spellStruct.GetFieldValue<ushort>("Spell", 0),
                SpellFlags = spellStruct.GetFieldValue<byte>("SpellFlags", 0x01),
                SpellMetaMagic = spellStruct.GetFieldValue<byte>("SpellMetaMagic", 0)
            };
            creatureClass.KnownSpells[spellLevel].Add(knownSpell);
        }
    }

    private static void ParseMemorizedSpellList(GffStruct classStruct, CreatureClass creatureClass, int spellLevel)
    {
        var fieldName = $"MemorizedList{spellLevel}";
        var listField = classStruct.GetField(fieldName);
        if (listField == null || !listField.IsList || listField.Value is not GffList spellList)
            return;

        foreach (var spellStruct in spellList.Elements)
        {
            var memorizedSpell = new MemorizedSpell
            {
                Spell = spellStruct.GetFieldValue<ushort>("Spell", 0),
                SpellFlags = spellStruct.GetFieldValue<byte>("SpellFlags", 0x01),
                SpellMetaMagic = spellStruct.GetFieldValue<byte>("SpellMetaMagic", 0),
                Ready = spellStruct.GetFieldValue<int>("Ready", 1)
            };
            creatureClass.MemorizedSpells[spellLevel].Add(memorizedSpell);
        }
    }

    private static void ParseFeatList(GffStruct root, UtcFile utc)
    {
        var featListField = root.GetField("FeatList");
        if (featListField == null || !featListField.IsList || featListField.Value is not GffList featList)
            return;

        foreach (var featStruct in featList.Elements)
        {
            var feat = featStruct.GetFieldValue<ushort>("Feat", 0);
            utc.FeatList.Add(feat);
        }
    }

    private static void ParseSkillList(GffStruct root, UtcFile utc)
    {
        var skillListField = root.GetField("SkillList");
        if (skillListField == null || !skillListField.IsList || skillListField.Value is not GffList skillList)
            return;

        foreach (var skillStruct in skillList.Elements)
        {
            var rank = skillStruct.GetFieldValue<byte>("Rank", 0);
            utc.SkillList.Add(rank);
        }
    }

    private static void ParseSpecAbilityList(GffStruct root, UtcFile utc)
    {
        var specAbilListField = root.GetField("SpecAbilityList");
        if (specAbilListField == null || !specAbilListField.IsList || specAbilListField.Value is not GffList specAbilList)
            return;

        foreach (var abilStruct in specAbilList.Elements)
        {
            var ability = new SpecialAbility
            {
                Spell = abilStruct.GetFieldValue<ushort>("Spell", 0),
                SpellCasterLevel = abilStruct.GetFieldValue<byte>("SpellCasterLevel", 1),
                SpellFlags = abilStruct.GetFieldValue<byte>("SpellFlags", 0x01)
            };
            utc.SpecAbilityList.Add(ability);
        }
    }

    private static void ParseItemList(GffStruct root, UtcFile utc)
    {
        var itemListField = root.GetField("ItemList");
        if (itemListField == null || !itemListField.IsList || itemListField.Value is not GffList itemList)
            return;

        foreach (var itemStruct in itemList.Elements)
        {
            var item = new InventoryItem
            {
                InventoryRes = itemStruct.GetFieldValue<string>("InventoryRes", string.Empty),
                Repos_PosX = itemStruct.GetFieldValue<ushort>("Repos_PosX", 0),
                Repos_PosY = itemStruct.GetFieldValue<ushort>("Repos_PosY", 0),
                Dropable = itemStruct.GetFieldValue<byte>("Dropable", 1) != 0,
                Pickpocketable = itemStruct.GetFieldValue<byte>("Pickpocketable", 0) != 0
            };
            utc.ItemList.Add(item);
        }
    }

    private static void ParseEquipItemList(GffStruct root, UtcFile utc)
    {
        var equipListField = root.GetField("Equip_ItemList");
        if (equipListField == null || !equipListField.IsList || equipListField.Value is not GffList equipList)
            return;

        foreach (var equipStruct in equipList.Elements)
        {
            // Aurora Toolset uses "EquippedRes" field for equipment (verified against clean UTC files).
            // The BioWare documentation mentions "EquipRes" but actual toolset output uses "EquippedRes".
            // Try EquippedRes first (correct), then EquipRes as fallback for compatibility.
            var resRef = equipStruct.GetFieldValue<string>("EquippedRes", string.Empty);
            if (string.IsNullOrEmpty(resRef))
            {
                resRef = equipStruct.GetFieldValue<string>("EquipRes", string.Empty);
            }

            var item = new EquippedItem
            {
                Slot = (int)equipStruct.Type,
                EquipRes = resRef
            };
            utc.EquipItemList.Add(item);
        }
    }
}
