using Radoub.Formats.Gff;
using Radoub.Formats.Utc;

namespace Radoub.Formats.Bic;

/// <summary>
/// Reads BIC (Player Character) files from binary format.
/// BIC files are GFF-based with file type "BIC ".
/// Reference: BioWare Aurora Creature Format specification (Section 2.6), neverwinter.nim
/// </summary>
public static class BicReader
{
    /// <summary>
    /// Read a BIC file from a file path.
    /// </summary>
    public static BicFile Read(string filePath)
    {
        var buffer = File.ReadAllBytes(filePath);
        return Read(buffer);
    }

    /// <summary>
    /// Read a BIC file from a stream.
    /// </summary>
    public static BicFile Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Read(ms.ToArray());
    }

    /// <summary>
    /// Read a BIC file from a byte buffer.
    /// </summary>
    public static BicFile Read(byte[] buffer)
    {
        // Parse as GFF first
        var gff = GffReader.Read(buffer);

        // Validate file type
        if (gff.FileType.TrimEnd() != "BIC")
        {
            throw new InvalidDataException(
                $"Invalid BIC file type: '{gff.FileType}' (expected 'BIC ')");
        }

        return ParseBicFile(gff);
    }

    private static BicFile ParseBicFile(GffFile gff)
    {
        var root = gff.RootStruct;

        var bic = new BicFile
        {
            FileType = gff.FileType,
            FileVersion = gff.FileVersion,

            // Blueprint fields - Note: BIC doesn't have TemplateResRef, Comment per Table 2.6.2
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
            BodyPart_RFoot = root.GetFieldValue<byte>("BodyPart_RFoot", 0),
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

            // Combat - Note: ChallengeRating not in BIC per Table 2.6.2
            NaturalAC = root.GetFieldValue<byte>("NaturalAC", 0),
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
            IsPC = root.GetFieldValue<byte>("IsPC", 1) != 0, // Default true for BIC
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

            // Conversation - Note: Not in BIC per Table 2.6.2
            // (Conversation field intentionally omitted)

            // Scripts (still valid for PCs)
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
            ScriptUserDefine = root.GetFieldValue<string>("ScriptuserDefine", string.Empty),

            // Player-specific fields (Table 2.6.1)
            Age = root.GetFieldValue<int>("Age", 0),
            Experience = root.GetFieldValue<uint>("Experience", 0),
            Gold = root.GetFieldValue<uint>("Gold", 0)
        };

        // Localized strings
        bic.FirstName = ParseLocString(root, "FirstName") ?? new CExoLocString();
        bic.LastName = ParseLocString(root, "LastName") ?? new CExoLocString();
        bic.Description = ParseLocString(root, "Description") ?? new CExoLocString();

        // UTC Lists
        ParseClassList(root, bic);
        ParseFeatList(root, bic);
        ParseSkillList(root, bic);
        ParseSpecAbilityList(root, bic);
        ParseItemList(root, bic);
        ParseEquipItemList(root, bic);

        // BIC-specific lists
        ParseQBList(root, bic);
        ParseReputationList(root, bic);

        return bic;
    }

    private static CExoLocString? ParseLocString(GffStruct root, string fieldName)
    {
        var field = root.GetField(fieldName);
        if (field == null || !field.IsCExoLocString || field.Value is not CExoLocString locString)
            return null;

        return locString;
    }

    private static void ParseClassList(GffStruct root, BicFile bic)
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
            bic.ClassList.Add(creatureClass);
        }
    }

    private static void ParseFeatList(GffStruct root, BicFile bic)
    {
        var featListField = root.GetField("FeatList");
        if (featListField == null || !featListField.IsList || featListField.Value is not GffList featList)
            return;

        foreach (var featStruct in featList.Elements)
        {
            var feat = featStruct.GetFieldValue<ushort>("Feat", 0);
            bic.FeatList.Add(feat);
        }
    }

    private static void ParseSkillList(GffStruct root, BicFile bic)
    {
        var skillListField = root.GetField("SkillList");
        if (skillListField == null || !skillListField.IsList || skillListField.Value is not GffList skillList)
            return;

        foreach (var skillStruct in skillList.Elements)
        {
            var rank = skillStruct.GetFieldValue<byte>("Rank", 0);
            bic.SkillList.Add(rank);
        }
    }

    private static void ParseSpecAbilityList(GffStruct root, BicFile bic)
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
            bic.SpecAbilityList.Add(ability);
        }
    }

    private static void ParseItemList(GffStruct root, BicFile bic)
    {
        var itemListField = root.GetField("ItemList");
        if (itemListField == null || !itemListField.IsList || itemListField.Value is not GffList itemList)
            return;

        foreach (var itemStruct in itemList.Elements)
        {
            // BIC files can store items in two formats:
            // 1. Simple ResRef format: "InventoryRes" field (like blueprints)
            // 2. Embedded item format: Full item struct with "TemplateResRef" field
            // Try InventoryRes first, fall back to TemplateResRef for embedded items
            var resRef = itemStruct.GetFieldValue<string>("InventoryRes", string.Empty);
            if (string.IsNullOrEmpty(resRef))
            {
                resRef = itemStruct.GetFieldValue<string>("TemplateResRef", string.Empty);
            }

            var item = new InventoryItem
            {
                InventoryRes = resRef,
                Repos_PosX = itemStruct.GetFieldValue<ushort>("Repos_PosX", 0),
                Repos_PosY = itemStruct.GetFieldValue<ushort>("Repos_PosY", 0),
                Dropable = itemStruct.GetFieldValue<byte>("Dropable", 1) != 0,
                Pickpocketable = itemStruct.GetFieldValue<byte>("Pickpocketable", 0) != 0
            };
            bic.ItemList.Add(item);
        }
    }

    private static void ParseEquipItemList(GffStruct root, BicFile bic)
    {
        var equipListField = root.GetField("Equip_ItemList");
        if (equipListField == null || !equipListField.IsList || equipListField.Value is not GffList equipList)
            return;

        foreach (var equipStruct in equipList.Elements)
        {
            // BIC files can store equipped items in multiple formats:
            // 1. "EquippedRes" field (standard BIC format from toolset)
            // 2. "TemplateResRef" field (saved game with embedded item data)
            // 3. "EquipRes" field (legacy/compatibility - UTC format field name)
            // Try EquippedRes first (correct BIC format), then fallbacks
            var resRef = equipStruct.GetFieldValue<string>("EquippedRes", string.Empty);
            if (string.IsNullOrEmpty(resRef))
            {
                resRef = equipStruct.GetFieldValue<string>("EquipRes", string.Empty);
            }
            if (string.IsNullOrEmpty(resRef))
            {
                resRef = equipStruct.GetFieldValue<string>("TemplateResRef", string.Empty);
            }

            var item = new EquippedItem
            {
                Slot = (int)equipStruct.Type,
                EquipRes = resRef
            };
            bic.EquipItemList.Add(item);
        }
    }

    private static void ParseQBList(GffStruct root, BicFile bic)
    {
        var qbListField = root.GetField("QBList");
        if (qbListField == null || !qbListField.IsList || qbListField.Value is not GffList qbList)
            return;

        foreach (var qbStruct in qbList.Elements)
        {
            var slot = new QuickBarSlot
            {
                ObjectType = qbStruct.GetFieldValue<byte>("QBObjectType", 0)
            };

            // Only parse additional fields if slot is not empty
            if (slot.ObjectType != QuickBarObjectType.Empty)
            {
                // Common fields
                slot.INTParam1 = qbStruct.GetFieldValue<int>("QBINTParam1", 0);
                slot.SecondaryItem = qbStruct.GetFieldValue<int>("QBSecondaryItem", 0);

                // Item-specific fields
                slot.ItemInvSlot = qbStruct.GetFieldValue<uint>("QBItemInvSlot", 0);
                slot.ItemReposX = qbStruct.GetFieldValue<byte>("QBItemReposX", 0);
                slot.ItemReposY = qbStruct.GetFieldValue<byte>("QBItemReposY", 0);
                slot.ContReposX = qbStruct.GetFieldValue<byte>("QBContReposX", 0xFF);
                slot.ContReposY = qbStruct.GetFieldValue<byte>("QBContReposY", 0xFF);
                slot.CastPropIndex = qbStruct.GetFieldValue<byte>("QBCastPropIndex", 0xFF);
                slot.CastSubPropIdx = qbStruct.GetFieldValue<byte>("QBCastSubPropIdx", 0xFF);

                // Spell-specific fields
                slot.MultiClass = qbStruct.GetFieldValue<byte>("QBMultiClass", 0);
                slot.MetaType = qbStruct.GetFieldValue<byte>("QBMetaType", 0);
                slot.DomainLevel = qbStruct.GetFieldValue<byte>("QBDomainLevel", 0);

                // Associate command fields
                slot.CommandSubType = qbStruct.GetFieldValue<int>("QBCommandSubType", 0);
                slot.CommandLabel = qbStruct.GetFieldValue<string>("QBCommandLabel", string.Empty);
            }

            bic.QBList.Add(slot);
        }
    }

    private static void ParseReputationList(GffStruct root, BicFile bic)
    {
        var repListField = root.GetField("ReputationList");
        if (repListField == null || !repListField.IsList || repListField.Value is not GffList repList)
            return;

        // Each struct has StructID 47837 (0xBABD) and a single INT field "Amount"
        foreach (var repStruct in repList.Elements)
        {
            var amount = repStruct.GetFieldValue<int>("Amount", 50);
            bic.ReputationList.Add(amount);
        }
    }
}
