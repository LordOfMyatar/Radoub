namespace Radoub.Formats.Ssf;

/// <summary>
/// Represents a Sound Set File (SSF) containing sound references for creature/character actions.
/// See BioWare Aurora SSF Format documentation for details.
/// </summary>
public class SsfFile
{
    /// <summary>
    /// File version (typically "V1.0").
    /// </summary>
    public string Version { get; set; } = "V1.0";

    /// <summary>
    /// All entries in the soundset. Standard soundsets have 49 entries.
    /// Index corresponds to the sound event type (0=Attack, 34=Hello, etc).
    /// </summary>
    public List<SsfEntry> Entries { get; set; } = new();

    /// <summary>
    /// Get entry by index, or null if out of range.
    /// </summary>
    public SsfEntry? GetEntry(int index) => index >= 0 && index < Entries.Count ? Entries[index] : null;

    /// <summary>
    /// Get entry by sound type enum.
    /// </summary>
    public SsfEntry? GetEntry(SsfSoundType soundType) => GetEntry((int)soundType);
}

/// <summary>
/// Standard SSF sound type indices.
/// These are fixed by the Aurora engine specification.
/// </summary>
public enum SsfSoundType
{
    Attack = 0,
    Battlecry1 = 1,
    Battlecry2 = 2,
    Battlecry3 = 3,
    HealMe = 4,
    Help = 5,
    EnemiesSighted = 6,
    Flee = 7,
    Taunt = 8,
    GuardMe = 9,
    Hold = 10,
    AttackGrunt1 = 11,
    AttackGrunt2 = 12,
    AttackGrunt3 = 13,
    PainGrunt1 = 14,
    PainGrunt2 = 15,
    PainGrunt3 = 16,
    NearDeath = 17,
    Death = 18,
    Poisoned = 19,
    SpellFailed = 20,
    WeaponIneffective = 21,
    FollowMe = 22,
    LookHere = 23,
    GroupParty = 24,
    MoveOver = 25,
    PickLock = 26,
    Search = 27,
    GoStealthy = 28,
    CanDo = 29,
    CannotDo = 30,
    TaskComplete = 31,
    Encumbered = 32,
    Selected = 33,
    Hello = 34,
    Yes = 35,
    No = 36,
    Stop = 37,
    Rest = 38,
    Bored = 39,
    Goodbye = 40,
    ThankYou = 41,
    Laugh = 42,
    Cuss = 43,
    Cheer = 44,
    SomethingToSay = 45,
    GoodIdea = 46,
    BadIdea = 47,
    Threaten = 48
}
