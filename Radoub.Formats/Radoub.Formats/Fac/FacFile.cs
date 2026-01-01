namespace Radoub.Formats.Fac;

/// <summary>
/// Represents a FAC (Faction) file used by Aurora Engine games.
/// FAC files are GFF-based and store faction definitions and reputation relationships.
/// Reference: BioWare Aurora Faction Format specification
/// </summary>
public class FacFile
{
    /// <summary>
    /// File type signature - should be "FAC "
    /// </summary>
    public string FileType { get; set; } = "FAC ";

    /// <summary>
    /// File version - typically "V3.2"
    /// </summary>
    public string FileVersion { get; set; } = "V3.2";

    /// <summary>
    /// List of factions defined in the module.
    /// Index in this list is the FactionID used by creatures.
    /// </summary>
    public List<Faction> FactionList { get; set; } = new();

    /// <summary>
    /// Reputation matrix - how each faction feels about every other faction.
    /// </summary>
    public List<Reputation> RepList { get; set; } = new();
}

/// <summary>
/// Represents a single faction definition.
/// </summary>
public class Faction
{
    /// <summary>
    /// Name of the faction (e.g., "PC", "Hostile", "Commoner").
    /// </summary>
    public string FactionName { get; set; } = string.Empty;

    /// <summary>
    /// Global effect flag.
    /// If true (1), all members of this faction immediately change standings when one member does.
    /// If false (0), each member maintains individual standings.
    /// Example: Killing one Guard causes all Guards to hate you (global) vs killing a deer doesn't affect other deer (not global).
    /// </summary>
    public ushort FactionGlobal { get; set; }

    /// <summary>
    /// Index into FactionList specifying the parent faction.
    /// The first four standard factions (PC, Hostile, Commoner, Merchant) have no parents
    /// and use 0xFFFFFFFF as their FactionParentID.
    /// </summary>
    public uint FactionParentID { get; set; } = 0xFFFFFFFF;
}

/// <summary>
/// Represents how one faction perceives another.
/// </summary>
public class Reputation
{
    /// <summary>
    /// Index into FactionList - the faction being perceived.
    /// </summary>
    public uint FactionID1 { get; set; }

    /// <summary>
    /// Index into FactionList - the faction doing the perceiving.
    /// </summary>
    public uint FactionID2 { get; set; }

    /// <summary>
    /// How FactionID2 perceives FactionID1.
    /// 0-10 = Hostile, 11-89 = Neutral, 90-100 = Friendly
    /// </summary>
    public uint FactionRep { get; set; }
}
