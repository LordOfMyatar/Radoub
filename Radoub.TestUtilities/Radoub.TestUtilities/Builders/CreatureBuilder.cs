using Radoub.Formats.Utc;

namespace Radoub.TestUtilities.Builders;

/// <summary>
/// Fluent builder for constructing UtcFile (creature) instances for testing.
/// </summary>
public class CreatureBuilder
{
    private readonly UtcFile _creature = new();

    /// <summary>
    /// Set basic identity.
    /// </summary>
    public CreatureBuilder WithIdentity(string firstName, string lastName = "", string tag = "")
    {
        _creature.FirstName.LocalizedStrings[0] = firstName;
        if (!string.IsNullOrEmpty(lastName))
            _creature.LastName.LocalizedStrings[0] = lastName;
        if (!string.IsNullOrEmpty(tag))
            _creature.Tag = tag;
        else
            _creature.Tag = firstName.ToLowerInvariant().Replace(" ", "_");
        return this;
    }

    /// <summary>
    /// Set template res ref (blueprint identifier).
    /// </summary>
    public CreatureBuilder WithResRef(string resRef)
    {
        _creature.TemplateResRef = resRef;
        return this;
    }

    /// <summary>
    /// Set race (index into racialtypes.2da).
    /// </summary>
    public CreatureBuilder WithRace(byte raceIndex)
    {
        _creature.Race = raceIndex;
        return this;
    }

    /// <summary>
    /// Set race using common race enum.
    /// </summary>
    public CreatureBuilder WithRace(CommonRace race)
    {
        _creature.Race = (byte)race;
        return this;
    }

    /// <summary>
    /// Set gender (0 = male, 1 = female).
    /// </summary>
    public CreatureBuilder WithGender(byte genderIndex)
    {
        _creature.Gender = genderIndex;
        return this;
    }

    /// <summary>
    /// Set as male.
    /// </summary>
    public CreatureBuilder AsMale()
    {
        _creature.Gender = 0;
        return this;
    }

    /// <summary>
    /// Set as female.
    /// </summary>
    public CreatureBuilder AsFemale()
    {
        _creature.Gender = 1;
        return this;
    }

    /// <summary>
    /// Set appearance type (index into appearance.2da).
    /// </summary>
    public CreatureBuilder WithAppearance(ushort appearanceIndex)
    {
        _creature.AppearanceType = appearanceIndex;
        return this;
    }

    /// <summary>
    /// Set ability scores.
    /// </summary>
    public CreatureBuilder WithAbilities(byte str = 10, byte dex = 10, byte con = 10, byte intel = 10, byte wis = 10, byte cha = 10)
    {
        _creature.Str = str;
        _creature.Dex = dex;
        _creature.Con = con;
        _creature.Int = intel;
        _creature.Wis = wis;
        _creature.Cha = cha;
        return this;
    }

    /// <summary>
    /// Set hit points.
    /// </summary>
    public CreatureBuilder WithHitPoints(short hp)
    {
        _creature.HitPoints = hp;
        _creature.CurrentHitPoints = hp;
        _creature.MaxHitPoints = hp;
        return this;
    }

    /// <summary>
    /// Set alignment.
    /// </summary>
    public CreatureBuilder WithAlignment(byte goodEvil = 50, byte lawfulChaotic = 50)
    {
        _creature.GoodEvil = goodEvil;
        _creature.LawfulChaotic = lawfulChaotic;
        return this;
    }

    /// <summary>
    /// Add a class level.
    /// </summary>
    public CreatureBuilder WithClass(int classIndex, short level)
    {
        _creature.ClassList.Add(new CreatureClass
        {
            Class = classIndex,
            ClassLevel = level
        });
        return this;
    }

    /// <summary>
    /// Add a class using common class enum.
    /// </summary>
    public CreatureBuilder WithClass(CommonClass classType, short level)
    {
        return WithClass((int)classType, level);
    }

    /// <summary>
    /// Add a feat.
    /// </summary>
    public CreatureBuilder WithFeat(ushort featIndex)
    {
        _creature.FeatList.Add(featIndex);
        return this;
    }

    /// <summary>
    /// Add multiple feats.
    /// </summary>
    public CreatureBuilder WithFeats(params ushort[] featIndexes)
    {
        _creature.FeatList.AddRange(featIndexes);
        return this;
    }

    /// <summary>
    /// Set skill ranks.
    /// </summary>
    public CreatureBuilder WithSkillRanks(params byte[] ranks)
    {
        _creature.SkillList.AddRange(ranks);
        return this;
    }

    /// <summary>
    /// Add an inventory item.
    /// </summary>
    public CreatureBuilder WithItem(string itemResRef, ushort posX = 0, ushort posY = 0)
    {
        _creature.ItemList.Add(new InventoryItem
        {
            InventoryRes = itemResRef,
            Repos_PosX = posX,
            Repos_PosY = posY
        });
        return this;
    }

    /// <summary>
    /// Add an equipped item.
    /// </summary>
    public CreatureBuilder WithEquippedItem(int slot, string itemResRef)
    {
        _creature.EquipItemList.Add(new EquippedItem
        {
            Slot = slot,
            EquipRes = itemResRef
        });
        return this;
    }

    /// <summary>
    /// Set conversation file.
    /// </summary>
    public CreatureBuilder WithConversation(string dlgResRef)
    {
        _creature.Conversation = dlgResRef;
        return this;
    }

    /// <summary>
    /// Set flag values.
    /// </summary>
    public CreatureBuilder WithFlags(bool plot = false, bool immortal = false, bool disarmable = false, bool lootable = false)
    {
        _creature.Plot = plot;
        _creature.IsImmortal = immortal;
        _creature.Disarmable = disarmable;
        _creature.Lootable = lootable;
        return this;
    }

    /// <summary>
    /// Set faction ID.
    /// </summary>
    public CreatureBuilder WithFaction(ushort factionId)
    {
        _creature.FactionID = factionId;
        return this;
    }

    /// <summary>
    /// Set scripts.
    /// </summary>
    public CreatureBuilder WithScripts(
        string? spawn = null,
        string? heartbeat = null,
        string? death = null,
        string? attacked = null,
        string? damaged = null,
        string? dialogue = null)
    {
        if (spawn != null) _creature.ScriptSpawn = spawn;
        if (heartbeat != null) _creature.ScriptHeartbeat = heartbeat;
        if (death != null) _creature.ScriptDeath = death;
        if (attacked != null) _creature.ScriptAttacked = attacked;
        if (damaged != null) _creature.ScriptDamaged = damaged;
        if (dialogue != null) _creature.ScriptDialogue = dialogue;
        return this;
    }

    /// <summary>
    /// Build the final UtcFile.
    /// </summary>
    public UtcFile Build()
    {
        return _creature;
    }
}

/// <summary>
/// Common race indices matching base game racialtypes.2da.
/// </summary>
public enum CommonRace : byte
{
    Dwarf = 0,
    Elf = 1,
    Gnome = 2,
    Halfling = 3,
    HalfElf = 4,
    HalfOrc = 5,
    Human = 6
}

/// <summary>
/// Common class indices matching base game classes.2da.
/// </summary>
public enum CommonClass
{
    Barbarian = 0,
    Bard = 1,
    Cleric = 2,
    Druid = 3,
    Fighter = 4,
    Monk = 5,
    Paladin = 6,
    Ranger = 7,
    Rogue = 8,
    Sorcerer = 9,
    Wizard = 10
}

/// <summary>
/// Extension methods for common creature patterns.
/// </summary>
public static class CreatureBuilderExtensions
{
    /// <summary>
    /// Create a simple NPC creature with basic stats.
    /// </summary>
    public static UtcFile CreateSimpleNPC(string name, CommonRace race, CommonClass primaryClass, short level)
    {
        return new CreatureBuilder()
            .WithIdentity(name)
            .WithRace(race)
            .WithClass(primaryClass, level)
            .WithHitPoints((short)(level * 8))
            .WithAbilities(12, 12, 12, 12, 12, 12)
            .Build();
    }

    /// <summary>
    /// Create a commoner NPC.
    /// </summary>
    public static UtcFile CreateCommoner(string name)
    {
        return new CreatureBuilder()
            .WithIdentity(name)
            .WithRace(CommonRace.Human)
            .WithHitPoints(4)
            .WithAbilities(10, 10, 10, 10, 10, 10)
            .Build();
    }
}
