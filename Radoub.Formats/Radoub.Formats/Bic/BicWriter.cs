using Radoub.Formats.Gff;
using Radoub.Formats.Utc;
using static Radoub.Formats.Gff.GffFieldBuilder;

namespace Radoub.Formats.Bic;

/// <summary>
/// Writes BIC (Player Character) files to binary format.
/// BIC files are GFF-based with file type "BIC ".
/// Reference: BioWare Aurora Creature Format specification (Section 2.6), neverwinter.nim
/// </summary>
public static class BicWriter
{
    /// <summary>
    /// Write a BIC file to a file path.
    /// </summary>
    public static void Write(BicFile bic, string filePath)
    {
        var buffer = Write(bic);
        File.WriteAllBytes(filePath, buffer);
    }

    /// <summary>
    /// Write a BIC file to a stream.
    /// </summary>
    public static void Write(BicFile bic, Stream stream)
    {
        var buffer = Write(bic);
        stream.Write(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// Write a BIC file to a byte buffer.
    /// </summary>
    public static byte[] Write(BicFile bic)
    {
        var gff = BuildGffFile(bic);
        return GffWriter.Write(gff);
    }

    private static GffFile BuildGffFile(BicFile bic)
    {
        var gff = new GffFile
        {
            FileType = bic.FileType,
            FileVersion = bic.FileVersion,
            RootStruct = BuildRootStruct(bic)
        };

        return gff;
    }

    private static GffStruct BuildRootStruct(BicFile bic)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        // Note: BIC doesn't have TemplateResRef, Comment, ChallengeRating, Conversation per Table 2.6.2

        // Blueprint fields
        AddByteField(root, "PaletteID", bic.PaletteID);

        // Identity fields (Table 2.1.1)
        AddLocStringField(root, "FirstName", bic.FirstName);
        AddLocStringField(root, "LastName", bic.LastName);
        AddCExoStringField(root, "Tag", bic.Tag);
        AddLocStringField(root, "Description", bic.Description);

        // Basic info
        AddByteField(root, "Race", bic.Race);
        AddByteField(root, "Gender", bic.Gender);
        if (!string.IsNullOrEmpty(bic.Subrace))
            AddCExoStringField(root, "Subrace", bic.Subrace);
        if (!string.IsNullOrEmpty(bic.Deity))
            AddCExoStringField(root, "Deity", bic.Deity);

        // Appearance
        AddWordField(root, "Appearance_Type", bic.AppearanceType);
        AddIntField(root, "Phenotype", bic.Phenotype);
        AddWordField(root, "PortraitId", bic.PortraitId);
        AddByteField(root, "Tail", bic.Tail);
        AddByteField(root, "Wings", bic.Wings);
        AddByteField(root, "BodyBag", bic.BodyBag);

        // Body parts (for part-based appearances)
        // Only write if non-zero to preserve file compatibility
        if (bic.AppearanceHead != 0)
            AddByteField(root, "Appearance_Head", bic.AppearanceHead);
        if (bic.BodyPart_Belt != 0)
            AddByteField(root, "BodyPart_Belt", bic.BodyPart_Belt);
        if (bic.BodyPart_LBicep != 0)
            AddByteField(root, "BodyPart_LBicep", bic.BodyPart_LBicep);
        if (bic.BodyPart_RBicep != 0)
            AddByteField(root, "BodyPart_RBicep", bic.BodyPart_RBicep);
        if (bic.BodyPart_LFArm != 0)
            AddByteField(root, "BodyPart_LFArm", bic.BodyPart_LFArm);
        if (bic.BodyPart_RFArm != 0)
            AddByteField(root, "BodyPart_RFArm", bic.BodyPart_RFArm);
        if (bic.BodyPart_LFoot != 0)
            AddByteField(root, "BodyPart_LFoot", bic.BodyPart_LFoot);
        if (bic.BodyPart_RFoot != 0)
            AddByteField(root, "BodyPart_RFoot", bic.BodyPart_RFoot);
        if (bic.BodyPart_LHand != 0)
            AddByteField(root, "BodyPart_LHand", bic.BodyPart_LHand);
        if (bic.BodyPart_RHand != 0)
            AddByteField(root, "BodyPart_RHand", bic.BodyPart_RHand);
        if (bic.BodyPart_LShin != 0)
            AddByteField(root, "BodyPart_LShin", bic.BodyPart_LShin);
        if (bic.BodyPart_RShin != 0)
            AddByteField(root, "BodyPart_RShin", bic.BodyPart_RShin);
        if (bic.BodyPart_LShoul != 0)
            AddByteField(root, "BodyPart_LShoul", bic.BodyPart_LShoul);
        if (bic.BodyPart_RShoul != 0)
            AddByteField(root, "BodyPart_RShoul", bic.BodyPart_RShoul);
        if (bic.BodyPart_LThigh != 0)
            AddByteField(root, "BodyPart_LThigh", bic.BodyPart_LThigh);
        if (bic.BodyPart_RThigh != 0)
            AddByteField(root, "BodyPart_RThigh", bic.BodyPart_RThigh);
        if (bic.BodyPart_Neck != 0)
            AddByteField(root, "BodyPart_Neck", bic.BodyPart_Neck);
        if (bic.BodyPart_Pelvis != 0)
            AddByteField(root, "BodyPart_Pelvis", bic.BodyPart_Pelvis);
        if (bic.BodyPart_Torso != 0)
            AddByteField(root, "BodyPart_Torso", bic.BodyPart_Torso);

        // Colors (for part-based appearances)
        // Only write if non-zero to preserve file compatibility
        if (bic.Color_Skin != 0)
            AddByteField(root, "Color_Skin", bic.Color_Skin);
        if (bic.Color_Hair != 0)
            AddByteField(root, "Color_Hair", bic.Color_Hair);
        if (bic.Color_Tattoo1 != 0)
            AddByteField(root, "Color_Tattoo1", bic.Color_Tattoo1);
        if (bic.Color_Tattoo2 != 0)
            AddByteField(root, "Color_Tattoo2", bic.Color_Tattoo2);

        // Armor part appearance (game instance fields - from equipped armor)
        // Only write if non-zero to preserve file compatibility
        if (bic.ArmorPart_Belt != 0)
            AddByteField(root, "ArmorPart_Belt", bic.ArmorPart_Belt);
        if (bic.ArmorPart_LBicep != 0)
            AddByteField(root, "ArmorPart_LBicep", bic.ArmorPart_LBicep);
        if (bic.ArmorPart_RBicep != 0)
            AddByteField(root, "ArmorPart_RBicep", bic.ArmorPart_RBicep);
        if (bic.ArmorPart_LFArm != 0)
            AddByteField(root, "ArmorPart_LFArm", bic.ArmorPart_LFArm);
        if (bic.ArmorPart_RFArm != 0)
            AddByteField(root, "ArmorPart_RFArm", bic.ArmorPart_RFArm);
        if (bic.ArmorPart_LFoot != 0)
            AddByteField(root, "ArmorPart_LFoot", bic.ArmorPart_LFoot);
        if (bic.ArmorPart_RFoot != 0)
            AddByteField(root, "ArmorPart_RFoot", bic.ArmorPart_RFoot);
        if (bic.ArmorPart_LHand != 0)
            AddByteField(root, "ArmorPart_LHand", bic.ArmorPart_LHand);
        if (bic.ArmorPart_RHand != 0)
            AddByteField(root, "ArmorPart_RHand", bic.ArmorPart_RHand);
        if (bic.ArmorPart_LShin != 0)
            AddByteField(root, "ArmorPart_LShin", bic.ArmorPart_LShin);
        if (bic.ArmorPart_RShin != 0)
            AddByteField(root, "ArmorPart_RShin", bic.ArmorPart_RShin);
        if (bic.ArmorPart_LShoul != 0)
            AddByteField(root, "ArmorPart_LShoul", bic.ArmorPart_LShoul);
        if (bic.ArmorPart_RShoul != 0)
            AddByteField(root, "ArmorPart_RShoul", bic.ArmorPart_RShoul);
        if (bic.ArmorPart_LThigh != 0)
            AddByteField(root, "ArmorPart_LThigh", bic.ArmorPart_LThigh);
        if (bic.ArmorPart_RThigh != 0)
            AddByteField(root, "ArmorPart_RThigh", bic.ArmorPart_RThigh);
        if (bic.ArmorPart_Neck != 0)
            AddByteField(root, "ArmorPart_Neck", bic.ArmorPart_Neck);
        if (bic.ArmorPart_Pelvis != 0)
            AddByteField(root, "ArmorPart_Pelvis", bic.ArmorPart_Pelvis);
        if (bic.ArmorPart_Torso != 0)
            AddByteField(root, "ArmorPart_Torso", bic.ArmorPart_Torso);
        if (bic.ArmorPart_Robe != 0)
            AddByteField(root, "ArmorPart_Robe", bic.ArmorPart_Robe);

        // Ability scores
        AddByteField(root, "Str", bic.Str);
        AddByteField(root, "Dex", bic.Dex);
        AddByteField(root, "Con", bic.Con);
        AddByteField(root, "Int", bic.Int);
        AddByteField(root, "Wis", bic.Wis);
        AddByteField(root, "Cha", bic.Cha);

        // Hit points
        AddShortField(root, "HitPoints", bic.HitPoints);
        AddShortField(root, "CurrentHitPoints", bic.CurrentHitPoints);
        AddShortField(root, "MaxHitPoints", bic.MaxHitPoints);

        // Combat (ChallengeRating excluded per Table 2.6.2)
        AddByteField(root, "NaturalAC", bic.NaturalAC);
        AddIntField(root, "CRAdjust", bic.CRAdjust);

        // Saving throws
        AddShortField(root, "fortbonus", bic.FortBonus);
        AddShortField(root, "refbonus", bic.RefBonus);
        AddShortField(root, "willbonus", bic.WillBonus);

        // Alignment
        AddByteField(root, "GoodEvil", bic.GoodEvil);
        AddByteField(root, "LawfulChaotic", bic.LawfulChaotic);

        // Flags
        AddByteField(root, "Plot", (byte)(bic.Plot ? 1 : 0));
        AddByteField(root, "IsImmortal", (byte)(bic.IsImmortal ? 1 : 0));
        AddByteField(root, "NoPermDeath", (byte)(bic.NoPermDeath ? 1 : 0));
        AddByteField(root, "IsPC", (byte)(bic.IsPC ? 1 : 0));
        AddByteField(root, "Disarmable", (byte)(bic.Disarmable ? 1 : 0));
        AddByteField(root, "Lootable", (byte)(bic.Lootable ? 1 : 0));
        AddByteField(root, "Interruptable", (byte)(bic.Interruptable ? 1 : 0));

        // Behavior
        AddWordField(root, "FactionID", bic.FactionID);
        AddByteField(root, "PerceptionRange", bic.PerceptionRange);
        AddIntField(root, "WalkRate", bic.WalkRate);
        AddWordField(root, "SoundSetFile", bic.SoundSetFile);
        AddDwordField(root, "DecayTime", bic.DecayTime);
        AddByteField(root, "StartingPackage", bic.StartingPackage);

        // Conversation excluded per Table 2.6.2

        // Scripts
        AddScriptFields(root, bic);

        // Player-specific fields (Table 2.6.1)
        AddIntField(root, "Age", bic.Age);
        AddDwordField(root, "Experience", bic.Experience);
        AddDwordField(root, "Gold", bic.Gold);

        // UTC Lists
        AddClassList(root, bic.ClassList);
        AddFeatList(root, bic.FeatList);
        AddSkillList(root, bic.SkillList);
        AddSpecAbilityList(root, bic.SpecAbilityList);
        AddItemList(root, bic.ItemList);
        AddEquipItemList(root, bic.EquipItemList);

        // BIC-specific lists
        AddQBList(root, bic.QBList);
        AddReputationList(root, bic.ReputationList);

        return root;
    }

    private static void AddScriptFields(GffStruct root, BicFile bic)
    {
        // Always write script fields (even if empty) for Aurora Toolset compatibility
        // Empty scripts are written with length 0
        AddCResRefField(root, "ScriptAttacked", bic.ScriptAttacked ?? "");
        AddCResRefField(root, "ScriptDamaged", bic.ScriptDamaged ?? "");
        AddCResRefField(root, "ScriptDeath", bic.ScriptDeath ?? "");
        AddCResRefField(root, "ScriptDialogue", bic.ScriptDialogue ?? "");
        AddCResRefField(root, "ScriptDisturbed", bic.ScriptDisturbed ?? "");
        AddCResRefField(root, "ScriptEndRound", bic.ScriptEndRound ?? "");
        AddCResRefField(root, "ScriptHeartbeat", bic.ScriptHeartbeat ?? "");
        AddCResRefField(root, "ScriptOnBlocked", bic.ScriptOnBlocked ?? "");
        AddCResRefField(root, "ScriptOnNotice", bic.ScriptOnNotice ?? "");
        AddCResRefField(root, "ScriptRested", bic.ScriptRested ?? "");
        AddCResRefField(root, "ScriptSpawn", bic.ScriptSpawn ?? "");
        AddCResRefField(root, "ScriptSpellAt", bic.ScriptSpellAt ?? "");
        AddCResRefField(root, "ScriptUserDefine", bic.ScriptUserDefine ?? "");
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
            var equipStruct = new GffStruct { Type = (uint)item.Slot };
            // BIC files use "EquippedRes" not "EquipRes" (which is used in UTC files)
            AddCResRefField(equipStruct, "EquippedRes", item.EquipRes);
            list.Elements.Add(equipStruct);
        }
        AddListField(root, "Equip_ItemList", list);
    }

    private static void AddQBList(GffStruct root, List<QuickBarSlot> slots)
    {
        var list = new GffList();
        foreach (var slot in slots)
        {
            var qbStruct = new GffStruct { Type = 0 };
            AddByteField(qbStruct, "QBObjectType", slot.ObjectType);

            // Only add additional fields if slot is not empty
            if (slot.ObjectType != QuickBarObjectType.Empty)
            {
                // Common fields
                AddIntField(qbStruct, "QBINTParam1", slot.INTParam1);

                switch (slot.ObjectType)
                {
                    case QuickBarObjectType.Item:
                        AddDwordField(qbStruct, "QBItemInvSlot", slot.ItemInvSlot);
                        AddByteField(qbStruct, "QBItemReposX", slot.ItemReposX);
                        AddByteField(qbStruct, "QBItemReposY", slot.ItemReposY);
                        AddByteField(qbStruct, "QBContReposX", slot.ContReposX);
                        AddByteField(qbStruct, "QBContReposY", slot.ContReposY);
                        AddByteField(qbStruct, "QBCastPropIndex", slot.CastPropIndex);
                        AddByteField(qbStruct, "QBCastSubPropIdx", slot.CastSubPropIdx);
                        if (slot.SecondaryItem != 0)
                            AddIntField(qbStruct, "QBSecondaryItem", slot.SecondaryItem);
                        break;

                    case QuickBarObjectType.Spell:
                        AddByteField(qbStruct, "QBMultiClass", slot.MultiClass);
                        AddByteField(qbStruct, "QBMetaType", slot.MetaType);
                        AddByteField(qbStruct, "QBDomainLevel", slot.DomainLevel);
                        break;

                    case QuickBarObjectType.AssociateCommand:
                        AddIntField(qbStruct, "QBCommandSubType", slot.CommandSubType);
                        if (!string.IsNullOrEmpty(slot.CommandLabel))
                            AddCExoStringField(qbStruct, "QBCommandLabel", slot.CommandLabel);
                        break;

                    // Skill, Feat, ModeToggle just use QBINTParam1 (already added)
                }
            }

            list.Elements.Add(qbStruct);
        }
        AddListField(root, "QBList", list);
    }

    private static void AddReputationList(GffStruct root, List<int> reputations)
    {
        var list = new GffList();
        foreach (var amount in reputations)
        {
            // StructID 47837 (0xBABD)
            var repStruct = new GffStruct { Type = 47837 };
            AddIntField(repStruct, "Amount", amount);
            list.Elements.Add(repStruct);
        }
        AddListField(root, "ReputationList", list);
    }
}
