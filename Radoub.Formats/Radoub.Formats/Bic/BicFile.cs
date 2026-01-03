using Radoub.Formats.Gff;
using Radoub.Formats.Utc;

namespace Radoub.Formats.Bic;

/// <summary>
/// Represents a BIC (Player Character) file used by Aurora Engine games.
/// BIC files extend UTC format with player-specific fields.
/// Reference: BioWare Aurora Creature Format specification (Section 2.6), neverwinter.nim
/// </summary>
public class BicFile : UtcFile
{
    /// <summary>
    /// Creates a new BicFile with default values.
    /// </summary>
    public BicFile()
    {
        FileType = "BIC ";
        IsPC = true;
    }

    /// <summary>
    /// Creates a BicFile from a UtcFile, copying all base properties
    /// and initializing BIC-specific fields with defaults.
    /// Use this to convert a creature blueprint to a player character.
    /// </summary>
    /// <param name="utc">Source UtcFile to convert</param>
    /// <returns>New BicFile with copied properties</returns>
    public static BicFile FromUtcFile(UtcFile utc)
    {
        if (utc is BicFile existingBic)
        {
            // Already a BicFile, just ensure FileType is correct
            existingBic.FileType = "BIC ";
            return existingBic;
        }

        var bic = new BicFile();
        CopyBaseProperties(utc, bic);
        bic.FileType = "BIC ";
        bic.IsPC = true;

        // Calculate Experience from total class levels
        // NWN XP formula: level N requires (N-1)*N/2 * 1000 XP
        // We need XP for the character's current total level
        int totalLevel = bic.ClassList.Sum(c => c.ClassLevel);
        if (totalLevel > 0)
        {
            // XP required for current level (minimum XP to be this level)
            bic.Experience = (uint)((totalLevel - 1) * totalLevel / 2 * 1000);
        }

        return bic;
    }

    /// <summary>
    /// Creates a UtcFile from this BicFile, copying all base properties.
    /// BIC-specific fields (Age, Experience, Gold, QBList, ReputationList) are not copied.
    /// Use this to convert a player character to a creature blueprint.
    /// </summary>
    /// <returns>New UtcFile with copied properties</returns>
    public UtcFile ToUtcFile()
    {
        var utc = new UtcFile();
        CopyBaseProperties(this, utc);
        utc.FileType = "UTC ";
        utc.IsPC = false;
        return utc;
    }

    private static void CopyBaseProperties(UtcFile source, UtcFile target)
    {
        // File info (not FileType - that's set by caller)
        target.FileVersion = source.FileVersion;

        // Blueprint-only fields
        target.TemplateResRef = source.TemplateResRef;
        target.Comment = source.Comment;
        target.PaletteID = source.PaletteID;

        // Identity fields
        target.FirstName = source.FirstName;
        target.LastName = source.LastName;
        target.Tag = source.Tag;
        target.Description = source.Description;

        // Basic info fields
        target.Race = source.Race;
        target.Gender = source.Gender;
        target.Subrace = source.Subrace;
        target.Deity = source.Deity;

        // Appearance fields
        target.AppearanceType = source.AppearanceType;
        target.Phenotype = source.Phenotype;
        target.PortraitId = source.PortraitId;
        target.Wings = source.Wings;
        target.Tail = source.Tail;
        target.BodyBag = source.BodyBag;

        // Body parts (part-based appearances)
        target.AppearanceHead = source.AppearanceHead;
        target.BodyPart_Belt = source.BodyPart_Belt;
        target.BodyPart_LBicep = source.BodyPart_LBicep;
        target.BodyPart_RBicep = source.BodyPart_RBicep;
        target.BodyPart_LFArm = source.BodyPart_LFArm;
        target.BodyPart_RFArm = source.BodyPart_RFArm;
        target.BodyPart_LFoot = source.BodyPart_LFoot;
        target.BodyPart_RFoot = source.BodyPart_RFoot;
        target.BodyPart_LHand = source.BodyPart_LHand;
        target.BodyPart_RHand = source.BodyPart_RHand;
        target.BodyPart_LShin = source.BodyPart_LShin;
        target.BodyPart_RShin = source.BodyPart_RShin;
        target.BodyPart_LShoul = source.BodyPart_LShoul;
        target.BodyPart_RShoul = source.BodyPart_RShoul;
        target.BodyPart_LThigh = source.BodyPart_LThigh;
        target.BodyPart_RThigh = source.BodyPart_RThigh;
        target.BodyPart_Neck = source.BodyPart_Neck;
        target.BodyPart_Pelvis = source.BodyPart_Pelvis;
        target.BodyPart_Torso = source.BodyPart_Torso;

        // Colors
        target.Color_Hair = source.Color_Hair;
        target.Color_Skin = source.Color_Skin;
        target.Color_Tattoo1 = source.Color_Tattoo1;
        target.Color_Tattoo2 = source.Color_Tattoo2;

        // Armor parts
        target.ArmorPart_Belt = source.ArmorPart_Belt;
        target.ArmorPart_LBicep = source.ArmorPart_LBicep;
        target.ArmorPart_RBicep = source.ArmorPart_RBicep;
        target.ArmorPart_LFArm = source.ArmorPart_LFArm;
        target.ArmorPart_RFArm = source.ArmorPart_RFArm;
        target.ArmorPart_LFoot = source.ArmorPart_LFoot;
        target.ArmorPart_RFoot = source.ArmorPart_RFoot;
        target.ArmorPart_LHand = source.ArmorPart_LHand;
        target.ArmorPart_RHand = source.ArmorPart_RHand;
        target.ArmorPart_LShin = source.ArmorPart_LShin;
        target.ArmorPart_RShin = source.ArmorPart_RShin;
        target.ArmorPart_LShoul = source.ArmorPart_LShoul;
        target.ArmorPart_RShoul = source.ArmorPart_RShoul;
        target.ArmorPart_LThigh = source.ArmorPart_LThigh;
        target.ArmorPart_RThigh = source.ArmorPart_RThigh;
        target.ArmorPart_Neck = source.ArmorPart_Neck;
        target.ArmorPart_Pelvis = source.ArmorPart_Pelvis;
        target.ArmorPart_Torso = source.ArmorPart_Torso;
        target.ArmorPart_Robe = source.ArmorPart_Robe;

        // Ability scores
        target.Str = source.Str;
        target.Dex = source.Dex;
        target.Con = source.Con;
        target.Int = source.Int;
        target.Wis = source.Wis;
        target.Cha = source.Cha;

        // Combat stats
        target.NaturalAC = source.NaturalAC;
        target.HitPoints = source.HitPoints;
        target.CurrentHitPoints = source.CurrentHitPoints;
        target.MaxHitPoints = source.MaxHitPoints;
        target.ChallengeRating = source.ChallengeRating;
        target.CRAdjust = source.CRAdjust;

        // Saving throw bonuses
        target.FortBonus = source.FortBonus;
        target.RefBonus = source.RefBonus;
        target.WillBonus = source.WillBonus;

        // Alignment
        target.GoodEvil = source.GoodEvil;
        target.LawfulChaotic = source.LawfulChaotic;

        // Behavior
        target.FactionID = source.FactionID;
        target.PerceptionRange = source.PerceptionRange;
        target.WalkRate = source.WalkRate;
        target.SoundSetFile = source.SoundSetFile;
        target.DecayTime = source.DecayTime;
        target.StartingPackage = source.StartingPackage;

        // Flags
        target.Plot = source.Plot;
        target.IsImmortal = source.IsImmortal;
        target.NoPermDeath = source.NoPermDeath;
        target.Disarmable = source.Disarmable;
        target.Lootable = source.Lootable;
        target.Interruptable = source.Interruptable;
        // Note: IsPC is set by caller based on target type

        // Scripts
        target.ScriptAttacked = source.ScriptAttacked;
        target.ScriptDamaged = source.ScriptDamaged;
        target.ScriptDeath = source.ScriptDeath;
        target.ScriptDialogue = source.ScriptDialogue;
        target.ScriptDisturbed = source.ScriptDisturbed;
        target.ScriptEndRound = source.ScriptEndRound;
        target.ScriptHeartbeat = source.ScriptHeartbeat;
        target.ScriptOnBlocked = source.ScriptOnBlocked;
        target.ScriptOnNotice = source.ScriptOnNotice;
        target.ScriptRested = source.ScriptRested;
        target.ScriptSpawn = source.ScriptSpawn;
        target.ScriptSpellAt = source.ScriptSpellAt;
        target.ScriptUserDefine = source.ScriptUserDefine;

        // Conversation
        target.Conversation = source.Conversation;

        // Class list - deep copy including spell lists
        target.ClassList = source.ClassList.Select(c =>
        {
            var newClass = new CreatureClass
            {
                Class = c.Class,
                ClassLevel = c.ClassLevel
            };

            // Copy known spells
            for (int level = 0; level < 10; level++)
            {
                newClass.KnownSpells[level] = c.KnownSpells[level].Select(s => new KnownSpell
                {
                    Spell = s.Spell,
                    SpellFlags = s.SpellFlags,
                    SpellMetaMagic = s.SpellMetaMagic
                }).ToList();
            }

            // Copy memorized spells
            for (int level = 0; level < 10; level++)
            {
                newClass.MemorizedSpells[level] = c.MemorizedSpells[level].Select(s => new MemorizedSpell
                {
                    Spell = s.Spell,
                    SpellFlags = s.SpellFlags,
                    SpellMetaMagic = s.SpellMetaMagic,
                    Ready = s.Ready
                }).ToList();
            }

            return newClass;
        }).ToList();

        // Feats and skills - deep copy
        target.FeatList = new List<ushort>(source.FeatList);
        target.SkillList = new List<byte>(source.SkillList);

        // Special abilities - deep copy
        target.SpecAbilityList = source.SpecAbilityList.Select(a => new SpecialAbility
        {
            Spell = a.Spell,
            SpellCasterLevel = a.SpellCasterLevel,
            SpellFlags = a.SpellFlags
        }).ToList();

        // Inventory - shallow copy (items have their own ResRefs)
        target.ItemList = source.ItemList.Select(i => new InventoryItem
        {
            InventoryRes = i.InventoryRes,
            Repos_PosX = i.Repos_PosX,
            Repos_PosY = i.Repos_PosY,
            Dropable = i.Dropable,
            Pickpocketable = i.Pickpocketable
        }).ToList();

        target.EquipItemList = source.EquipItemList.Select(e => new EquippedItem
        {
            Slot = e.Slot,
            EquipRes = e.EquipRes
        }).ToList();
    }

    // Player-specific fields (Table 2.6.1)

    /// <summary>
    /// Character's age (entered during character creation).
    /// </summary>
    public int Age { get; set; }

    /// <summary>
    /// Total experience points earned.
    /// </summary>
    public uint Experience { get; set; }

    /// <summary>
    /// Amount of gold carried by the character.
    /// </summary>
    public uint Gold { get; set; }

    /// <summary>
    /// QuickBar slot assignments (36 slots).
    /// Elements 0-11: Normal QuickBar
    /// Elements 12-23: Shift-QuickBar
    /// Elements 24-35: Control-QuickBar
    /// </summary>
    public List<QuickBarSlot> QBList { get; set; } = new();

    /// <summary>
    /// Faction reputation amounts.
    /// Each entry is the player's rating (0-100) with a faction,
    /// in the same order as the module's repute.fac FactionList.
    /// </summary>
    public List<int> ReputationList { get; set; } = new();
}

/// <summary>
/// Represents a QuickBar slot assignment (StructID 0).
/// </summary>
public class QuickBarSlot
{
    /// <summary>
    /// Type of object in this quickbar slot.
    /// 0=empty, 1=item, 2=spell, 3=skill, 4=feat, 5=script,
    /// 6=dialog, 7=attack, 8=emote, 9=castspell, 10=mode toggle,
    /// 38=possess familiar, 39=associate command, 40=examine,
    /// 41=barter, 42=quickchat, 43=cancel polymorph, 44=spell-like ability
    /// </summary>
    public byte ObjectType { get; set; }

    // Item-specific fields (ObjectType = 1)

    /// <summary>
    /// Object ID of the item in inventory (items only).
    /// </summary>
    public uint ItemInvSlot { get; set; }

    /// <summary>
    /// X position of item in inventory (items only).
    /// </summary>
    public byte ItemReposX { get; set; }

    /// <summary>
    /// Y position of item in inventory (items only).
    /// </summary>
    public byte ItemReposY { get; set; }

    /// <summary>
    /// X position in container (0xFF if not in container).
    /// </summary>
    public byte ContReposX { get; set; } = 0xFF;

    /// <summary>
    /// Y position in container (0xFF if not in container).
    /// </summary>
    public byte ContReposY { get; set; } = 0xFF;

    /// <summary>
    /// Cast property index (0xFF if no cast property).
    /// </summary>
    public byte CastPropIndex { get; set; } = 0xFF;

    /// <summary>
    /// Cast sub-property index (0xFF if no subproperty).
    /// </summary>
    public byte CastSubPropIdx { get; set; } = 0xFF;

    // Spell/Skill/Feat/Mode fields (ObjectType = 2, 3, 4, 10)

    /// <summary>
    /// Index into spells.2da, skills.2da, feat.2da, or mode type.
    /// </summary>
    public int INTParam1 { get; set; }

    /// <summary>
    /// Index into creature's ClassList (spells only).
    /// </summary>
    public byte MultiClass { get; set; }

    /// <summary>
    /// MetaMagic flags on a spell (spells only).
    /// </summary>
    public byte MetaType { get; set; }

    /// <summary>
    /// Domain level for cleric domain spells (0 for most spells).
    /// </summary>
    public byte DomainLevel { get; set; }

    /// <summary>
    /// Secondary parameter (varies by object type).
    /// </summary>
    public int SecondaryItem { get; set; }

    /// <summary>
    /// Command sub-type for associate commands.
    /// </summary>
    public int CommandSubType { get; set; }

    /// <summary>
    /// Command label for associate commands.
    /// </summary>
    public string CommandLabel { get; set; } = string.Empty;

    /// <summary>
    /// Returns true if slot is empty.
    /// </summary>
    public bool IsEmpty => ObjectType == 0;
}

/// <summary>
/// QuickBar object type constants.
/// </summary>
public static class QuickBarObjectType
{
    public const byte Empty = 0;
    public const byte Item = 1;
    public const byte Spell = 2;
    public const byte Skill = 3;
    public const byte Feat = 4;
    public const byte Script = 5;
    public const byte Dialog = 6;
    public const byte Attack = 7;
    public const byte Emote = 8;
    public const byte CastSpell = 9;
    public const byte ModeToggle = 10;
    public const byte PossessFamiliar = 38;
    public const byte AssociateCommand = 39;
    public const byte Examine = 40;
    public const byte Barter = 41;
    public const byte QuickChat = 42;
    public const byte CancelPolymorph = 43;
    public const byte SpellLikeAbility = 44;

    /// <summary>
    /// Get human-readable name for a quickbar object type.
    /// </summary>
    public static string GetTypeName(byte type) => type switch
    {
        Empty => "Empty",
        Item => "Item",
        Spell => "Spell",
        Skill => "Skill",
        Feat => "Feat",
        Script => "Script",
        Dialog => "Dialog",
        Attack => "Attack",
        Emote => "Emote",
        CastSpell => "Cast Spell Property",
        ModeToggle => "Mode Toggle",
        PossessFamiliar => "Possess Familiar",
        AssociateCommand => "Associate Command",
        Examine => "Examine",
        Barter => "Barter",
        QuickChat => "Quick Chat",
        CancelPolymorph => "Cancel Polymorph",
        SpellLikeAbility => "Spell-Like Ability",
        _ => $"Unknown ({type})"
    };
}
