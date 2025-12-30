using Radoub.Formats.Gff;
using static Radoub.Formats.Gff.GffFieldBuilder;

namespace Radoub.Formats.Utc;

/// <summary>
/// Writes UTC (Creature Blueprint) files to binary format.
/// UTC files are GFF-based with file type "UTC ".
/// Reference: BioWare Aurora Creature Format specification, neverwinter.nim
/// </summary>
public static class UtcWriter
{
    /// <summary>
    /// Write a UTC file to a file path.
    /// </summary>
    public static void Write(UtcFile utc, string filePath)
    {
        var buffer = Write(utc);
        File.WriteAllBytes(filePath, buffer);
    }

    /// <summary>
    /// Write a UTC file to a stream.
    /// </summary>
    public static void Write(UtcFile utc, Stream stream)
    {
        var buffer = Write(utc);
        stream.Write(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// Write a UTC file to a byte buffer.
    /// </summary>
    public static byte[] Write(UtcFile utc)
    {
        var gff = BuildGffFile(utc);
        return GffWriter.Write(gff);
    }

    private static GffFile BuildGffFile(UtcFile utc)
    {
        var gff = new GffFile
        {
            FileType = utc.FileType,
            FileVersion = utc.FileVersion,
            RootStruct = BuildRootStruct(utc)
        };

        return gff;
    }

    private static GffStruct BuildRootStruct(UtcFile utc)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        // Blueprint fields (Table 2.2)
        AddCResRefField(root, "TemplateResRef", utc.TemplateResRef);
        if (!string.IsNullOrEmpty(utc.Comment))
            AddCExoStringField(root, "Comment", utc.Comment);
        AddByteField(root, "PaletteID", utc.PaletteID);

        // Identity fields (Table 2.1.1)
        AddLocStringField(root, "FirstName", utc.FirstName);
        AddLocStringField(root, "LastName", utc.LastName);
        AddCExoStringField(root, "Tag", utc.Tag);
        AddLocStringField(root, "Description", utc.Description);

        // Basic info
        AddByteField(root, "Race", utc.Race);
        AddByteField(root, "Gender", utc.Gender);
        if (!string.IsNullOrEmpty(utc.Subrace))
            AddCExoStringField(root, "Subrace", utc.Subrace);
        if (!string.IsNullOrEmpty(utc.Deity))
            AddCExoStringField(root, "Deity", utc.Deity);

        // Appearance
        AddWordField(root, "Appearance_Type", utc.AppearanceType);
        AddIntField(root, "Phenotype", utc.Phenotype);
        AddWordField(root, "PortraitId", utc.PortraitId);
        AddByteField(root, "Tail", utc.Tail);
        AddByteField(root, "Wings", utc.Wings);
        AddByteField(root, "BodyBag", utc.BodyBag);

        // Body parts (for part-based appearances)
        // Only write if non-zero to preserve file compatibility
        if (utc.AppearanceHead != 0)
            AddByteField(root, "Appearance_Head", utc.AppearanceHead);
        if (utc.BodyPart_Belt != 0)
            AddByteField(root, "BodyPart_Belt", utc.BodyPart_Belt);
        if (utc.BodyPart_LBicep != 0)
            AddByteField(root, "BodyPart_LBicep", utc.BodyPart_LBicep);
        if (utc.BodyPart_RBicep != 0)
            AddByteField(root, "BodyPart_RBicep", utc.BodyPart_RBicep);
        if (utc.BodyPart_LFArm != 0)
            AddByteField(root, "BodyPart_LFArm", utc.BodyPart_LFArm);
        if (utc.BodyPart_RFArm != 0)
            AddByteField(root, "BodyPart_RFArm", utc.BodyPart_RFArm);
        if (utc.BodyPart_LFoot != 0)
            AddByteField(root, "BodyPart_LFoot", utc.BodyPart_LFoot);
        if (utc.BodyPart_RFoot != 0)
            AddByteField(root, "BodyPart_RFoot", utc.BodyPart_RFoot);
        if (utc.BodyPart_LHand != 0)
            AddByteField(root, "BodyPart_LHand", utc.BodyPart_LHand);
        if (utc.BodyPart_RHand != 0)
            AddByteField(root, "BodyPart_RHand", utc.BodyPart_RHand);
        if (utc.BodyPart_LShin != 0)
            AddByteField(root, "BodyPart_LShin", utc.BodyPart_LShin);
        if (utc.BodyPart_RShin != 0)
            AddByteField(root, "BodyPart_RShin", utc.BodyPart_RShin);
        if (utc.BodyPart_LShoul != 0)
            AddByteField(root, "BodyPart_LShoul", utc.BodyPart_LShoul);
        if (utc.BodyPart_RShoul != 0)
            AddByteField(root, "BodyPart_RShoul", utc.BodyPart_RShoul);
        if (utc.BodyPart_LThigh != 0)
            AddByteField(root, "BodyPart_LThigh", utc.BodyPart_LThigh);
        if (utc.BodyPart_RThigh != 0)
            AddByteField(root, "BodyPart_RThigh", utc.BodyPart_RThigh);
        if (utc.BodyPart_Neck != 0)
            AddByteField(root, "BodyPart_Neck", utc.BodyPart_Neck);
        if (utc.BodyPart_Pelvis != 0)
            AddByteField(root, "BodyPart_Pelvis", utc.BodyPart_Pelvis);
        if (utc.BodyPart_Torso != 0)
            AddByteField(root, "BodyPart_Torso", utc.BodyPart_Torso);

        // Ability scores
        AddByteField(root, "Str", utc.Str);
        AddByteField(root, "Dex", utc.Dex);
        AddByteField(root, "Con", utc.Con);
        AddByteField(root, "Int", utc.Int);
        AddByteField(root, "Wis", utc.Wis);
        AddByteField(root, "Cha", utc.Cha);

        // Hit points
        AddShortField(root, "HitPoints", utc.HitPoints);
        AddShortField(root, "CurrentHitPoints", utc.CurrentHitPoints);
        AddShortField(root, "MaxHitPoints", utc.MaxHitPoints);

        // Combat
        AddByteField(root, "NaturalAC", utc.NaturalAC);
        AddFloatField(root, "ChallengeRating", utc.ChallengeRating);
        AddIntField(root, "CRAdjust", utc.CRAdjust);

        // Saving throws
        AddShortField(root, "fortbonus", utc.FortBonus);
        AddShortField(root, "refbonus", utc.RefBonus);
        AddShortField(root, "willbonus", utc.WillBonus);

        // Alignment
        AddByteField(root, "GoodEvil", utc.GoodEvil);
        AddByteField(root, "LawfulChaotic", utc.LawfulChaotic);

        // Flags
        AddByteField(root, "Plot", (byte)(utc.Plot ? 1 : 0));
        AddByteField(root, "IsImmortal", (byte)(utc.IsImmortal ? 1 : 0));
        AddByteField(root, "NoPermDeath", (byte)(utc.NoPermDeath ? 1 : 0));
        AddByteField(root, "IsPC", (byte)(utc.IsPC ? 1 : 0));
        AddByteField(root, "Disarmable", (byte)(utc.Disarmable ? 1 : 0));
        AddByteField(root, "Lootable", (byte)(utc.Lootable ? 1 : 0));
        AddByteField(root, "Interruptable", (byte)(utc.Interruptable ? 1 : 0));

        // Behavior
        AddWordField(root, "FactionID", utc.FactionID);
        AddByteField(root, "PerceptionRange", utc.PerceptionRange);
        AddIntField(root, "WalkRate", utc.WalkRate);
        AddWordField(root, "SoundSetFile", utc.SoundSetFile);
        AddDwordField(root, "DecayTime", utc.DecayTime);
        AddByteField(root, "StartingPackage", utc.StartingPackage);

        // Conversation
        if (!string.IsNullOrEmpty(utc.Conversation))
            AddCResRefField(root, "Conversation", utc.Conversation);

        // Scripts
        AddScriptFields(root, utc);

        // Lists
        AddClassList(root, utc.ClassList);
        AddFeatList(root, utc.FeatList);
        AddSkillList(root, utc.SkillList);
        AddSpecAbilityList(root, utc.SpecAbilityList);
        AddItemList(root, utc.ItemList);
        AddEquipItemList(root, utc.EquipItemList);

        return root;
    }

    private static void AddScriptFields(GffStruct root, UtcFile utc)
    {
        if (!string.IsNullOrEmpty(utc.ScriptAttacked))
            AddCResRefField(root, "ScriptAttacked", utc.ScriptAttacked);
        if (!string.IsNullOrEmpty(utc.ScriptDamaged))
            AddCResRefField(root, "ScriptDamaged", utc.ScriptDamaged);
        if (!string.IsNullOrEmpty(utc.ScriptDeath))
            AddCResRefField(root, "ScriptDeath", utc.ScriptDeath);
        if (!string.IsNullOrEmpty(utc.ScriptDialogue))
            AddCResRefField(root, "ScriptDialogue", utc.ScriptDialogue);
        if (!string.IsNullOrEmpty(utc.ScriptDisturbed))
            AddCResRefField(root, "ScriptDisturbed", utc.ScriptDisturbed);
        if (!string.IsNullOrEmpty(utc.ScriptEndRound))
            AddCResRefField(root, "ScriptEndRound", utc.ScriptEndRound);
        if (!string.IsNullOrEmpty(utc.ScriptHeartbeat))
            AddCResRefField(root, "ScriptHeartbeat", utc.ScriptHeartbeat);
        if (!string.IsNullOrEmpty(utc.ScriptOnBlocked))
            AddCResRefField(root, "ScriptOnBlocked", utc.ScriptOnBlocked);
        if (!string.IsNullOrEmpty(utc.ScriptOnNotice))
            AddCResRefField(root, "ScriptOnNotice", utc.ScriptOnNotice);
        if (!string.IsNullOrEmpty(utc.ScriptRested))
            AddCResRefField(root, "ScriptRested", utc.ScriptRested);
        if (!string.IsNullOrEmpty(utc.ScriptSpawn))
            AddCResRefField(root, "ScriptSpawn", utc.ScriptSpawn);
        if (!string.IsNullOrEmpty(utc.ScriptSpellAt))
            AddCResRefField(root, "ScriptSpellAt", utc.ScriptSpellAt);
        if (!string.IsNullOrEmpty(utc.ScriptUserDefine))
            AddCResRefField(root, "ScriptuserDefine", utc.ScriptUserDefine);
    }

    private static void AddClassList(GffStruct root, List<CreatureClass> classes)
    {
        var list = new GffList();
        foreach (var cls in classes)
        {
            var classStruct = new GffStruct { Type = 2 };
            AddIntField(classStruct, "Class", cls.Class);
            AddShortField(classStruct, "ClassLevel", cls.ClassLevel);
            list.Elements.Add(classStruct);
        }
        AddListField(root, "ClassList", list);
    }

    private static void AddFeatList(GffStruct root, List<ushort> feats)
    {
        var list = new GffList();
        foreach (var feat in feats)
        {
            var featStruct = new GffStruct { Type = 1 };
            AddWordField(featStruct, "Feat", feat);
            list.Elements.Add(featStruct);
        }
        AddListField(root, "FeatList", list);
    }

    private static void AddSkillList(GffStruct root, List<byte> skills)
    {
        var list = new GffList();
        foreach (var rank in skills)
        {
            var skillStruct = new GffStruct { Type = 0 };
            AddByteField(skillStruct, "Rank", rank);
            list.Elements.Add(skillStruct);
        }
        AddListField(root, "SkillList", list);
    }

    private static void AddSpecAbilityList(GffStruct root, List<SpecialAbility> abilities)
    {
        var list = new GffList();
        foreach (var abil in abilities)
        {
            var abilStruct = new GffStruct { Type = 4 };
            AddWordField(abilStruct, "Spell", abil.Spell);
            AddByteField(abilStruct, "SpellCasterLevel", abil.SpellCasterLevel);
            AddByteField(abilStruct, "SpellFlags", abil.SpellFlags);
            list.Elements.Add(abilStruct);
        }
        AddListField(root, "SpecAbilityList", list);
    }

    private static void AddItemList(GffStruct root, List<InventoryItem> items)
    {
        var list = new GffList();
        foreach (var item in items)
        {
            var itemStruct = new GffStruct { Type = 0 };
            AddCResRefField(itemStruct, "InventoryRes", item.InventoryRes);
            AddWordField(itemStruct, "Repos_PosX", item.Repos_PosX);
            AddWordField(itemStruct, "Repos_PosY", item.Repos_PosY);
            AddByteField(itemStruct, "Dropable", (byte)(item.Dropable ? 1 : 0));
            AddByteField(itemStruct, "Pickpocketable", (byte)(item.Pickpocketable ? 1 : 0));
            list.Elements.Add(itemStruct);
        }
        AddListField(root, "ItemList", list);
    }

    private static void AddEquipItemList(GffStruct root, List<EquippedItem> items)
    {
        var list = new GffList();
        foreach (var item in items)
        {
            // StructID is the equipment slot bit flag
            var equipStruct = new GffStruct { Type = (uint)item.Slot };
            AddCResRefField(equipStruct, "EquipRes", item.EquipRes);
            list.Elements.Add(equipStruct);
        }
        AddListField(root, "Equip_ItemList", list);
    }

}
