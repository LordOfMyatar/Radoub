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
        // Always write - 0 is a valid body part value
        AddByteField(root, "Appearance_Head", utc.AppearanceHead);
        AddByteField(root, "BodyPart_Belt", utc.BodyPart_Belt);
        AddByteField(root, "BodyPart_LBicep", utc.BodyPart_LBicep);
        AddByteField(root, "BodyPart_RBicep", utc.BodyPart_RBicep);
        AddByteField(root, "BodyPart_LFArm", utc.BodyPart_LFArm);
        AddByteField(root, "BodyPart_RFArm", utc.BodyPart_RFArm);
        AddByteField(root, "BodyPart_LFoot", utc.BodyPart_LFoot);
        AddByteField(root, "BodyPart_RFoot", utc.BodyPart_RFoot);
        AddByteField(root, "BodyPart_LHand", utc.BodyPart_LHand);
        AddByteField(root, "BodyPart_RHand", utc.BodyPart_RHand);
        AddByteField(root, "BodyPart_LShin", utc.BodyPart_LShin);
        AddByteField(root, "BodyPart_RShin", utc.BodyPart_RShin);
        AddByteField(root, "BodyPart_LShoul", utc.BodyPart_LShoul);
        AddByteField(root, "BodyPart_RShoul", utc.BodyPart_RShoul);
        AddByteField(root, "BodyPart_LThigh", utc.BodyPart_LThigh);
        AddByteField(root, "BodyPart_RThigh", utc.BodyPart_RThigh);
        AddByteField(root, "BodyPart_Neck", utc.BodyPart_Neck);
        AddByteField(root, "BodyPart_Pelvis", utc.BodyPart_Pelvis);
        AddByteField(root, "BodyPart_Torso", utc.BodyPart_Torso);

        // Colors (for part-based appearances)
        // Always write - 0 is a valid color index
        AddByteField(root, "Color_Skin", utc.Color_Skin);
        AddByteField(root, "Color_Hair", utc.Color_Hair);
        AddByteField(root, "Color_Tattoo1", utc.Color_Tattoo1);
        AddByteField(root, "Color_Tattoo2", utc.Color_Tattoo2);

        // Armor part appearance (game instance fields - from equipped armor)
        // Always write - 0 is a valid value
        AddByteField(root, "ArmorPart_Belt", utc.ArmorPart_Belt);
        AddByteField(root, "ArmorPart_LBicep", utc.ArmorPart_LBicep);
        AddByteField(root, "ArmorPart_RBicep", utc.ArmorPart_RBicep);
        AddByteField(root, "ArmorPart_LFArm", utc.ArmorPart_LFArm);
        AddByteField(root, "ArmorPart_RFArm", utc.ArmorPart_RFArm);
        AddByteField(root, "ArmorPart_LFoot", utc.ArmorPart_LFoot);
        AddByteField(root, "ArmorPart_RFoot", utc.ArmorPart_RFoot);
        AddByteField(root, "ArmorPart_LHand", utc.ArmorPart_LHand);
        AddByteField(root, "ArmorPart_RHand", utc.ArmorPart_RHand);
        AddByteField(root, "ArmorPart_LShin", utc.ArmorPart_LShin);
        AddByteField(root, "ArmorPart_RShin", utc.ArmorPart_RShin);
        AddByteField(root, "ArmorPart_LShoul", utc.ArmorPart_LShoul);
        AddByteField(root, "ArmorPart_RShoul", utc.ArmorPart_RShoul);
        AddByteField(root, "ArmorPart_LThigh", utc.ArmorPart_LThigh);
        AddByteField(root, "ArmorPart_RThigh", utc.ArmorPart_RThigh);
        AddByteField(root, "ArmorPart_Neck", utc.ArmorPart_Neck);
        AddByteField(root, "ArmorPart_Pelvis", utc.ArmorPart_Pelvis);
        AddByteField(root, "ArmorPart_Torso", utc.ArmorPart_Torso);
        AddByteField(root, "ArmorPart_Robe", utc.ArmorPart_Robe);

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
        // Always write script fields (even if empty) for Aurora Toolset compatibility
        // Empty scripts are written with length 0
        AddCResRefField(root, "ScriptAttacked", utc.ScriptAttacked ?? "");
        AddCResRefField(root, "ScriptDamaged", utc.ScriptDamaged ?? "");
        AddCResRefField(root, "ScriptDeath", utc.ScriptDeath ?? "");
        AddCResRefField(root, "ScriptDialogue", utc.ScriptDialogue ?? "");
        AddCResRefField(root, "ScriptDisturbed", utc.ScriptDisturbed ?? "");
        AddCResRefField(root, "ScriptEndRound", utc.ScriptEndRound ?? "");
        AddCResRefField(root, "ScriptHeartbeat", utc.ScriptHeartbeat ?? "");
        AddCResRefField(root, "ScriptOnBlocked", utc.ScriptOnBlocked ?? "");
        AddCResRefField(root, "ScriptOnNotice", utc.ScriptOnNotice ?? "");
        AddCResRefField(root, "ScriptRested", utc.ScriptRested ?? "");
        AddCResRefField(root, "ScriptSpawn", utc.ScriptSpawn ?? "");
        AddCResRefField(root, "ScriptSpellAt", utc.ScriptSpellAt ?? "");
        AddCResRefField(root, "ScriptUserDefine", utc.ScriptUserDefine ?? "");
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
            // UTC files use "EquippedRes" (same as BIC files, verified against Aurora Toolset output)
            AddCResRefField(equipStruct, "EquippedRes", item.EquipRes);
            list.Elements.Add(equipStruct);
        }
        AddListField(root, "Equip_ItemList", list);
    }

}
