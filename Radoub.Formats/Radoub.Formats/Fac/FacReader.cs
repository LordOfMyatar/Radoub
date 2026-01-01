using Radoub.Formats.Gff;

namespace Radoub.Formats.Fac;

/// <summary>
/// Reads FAC (Faction) files from binary GFF format.
/// Reference: BioWare Aurora Faction Format specification
/// </summary>
public static class FacReader
{
    /// <summary>
    /// Read a FAC file from a file path.
    /// </summary>
    public static FacFile Read(string filePath)
    {
        var gff = GffReader.Read(filePath);
        return ParseFac(gff);
    }

    /// <summary>
    /// Read a FAC file from a stream.
    /// </summary>
    public static FacFile Read(Stream stream)
    {
        var gff = GffReader.Read(stream);
        return ParseFac(gff);
    }

    /// <summary>
    /// Read a FAC file from a byte buffer.
    /// </summary>
    public static FacFile Read(byte[] buffer)
    {
        var gff = GffReader.Read(buffer);
        return ParseFac(gff);
    }

    private static FacFile ParseFac(GffFile gff)
    {
        var fac = new FacFile
        {
            FileType = gff.FileType,
            FileVersion = gff.FileVersion
        };

        var root = gff.RootStruct;

        // Parse FactionList
        var factionListField = root.GetField("FactionList");
        if (factionListField?.Value is GffList factionList)
        {
            foreach (var factionStruct in factionList.Elements)
            {
                var faction = new Faction
                {
                    FactionName = factionStruct.GetFieldValue<string>("FactionName", string.Empty),
                    FactionGlobal = factionStruct.GetFieldValue<ushort>("FactionGlobal", 0),
                    FactionParentID = factionStruct.GetFieldValue<uint>("FactionParentID", 0xFFFFFFFF)
                };
                fac.FactionList.Add(faction);
            }
        }

        // Parse RepList (reputation relationships)
        var repListField = root.GetField("RepList");
        if (repListField?.Value is GffList repList)
        {
            foreach (var repStruct in repList.Elements)
            {
                var rep = new Reputation
                {
                    FactionID1 = repStruct.GetFieldValue<uint>("FactionID1", 0),
                    FactionID2 = repStruct.GetFieldValue<uint>("FactionID2", 0),
                    FactionRep = repStruct.GetFieldValue<uint>("FactionRep", 50)
                };
                fac.RepList.Add(rep);
            }
        }

        return fac;
    }

    /// <summary>
    /// Creates a default faction file with the 5 standard NWN factions.
    /// Useful as a fallback when repute.fac is not available.
    /// </summary>
    public static FacFile CreateDefault()
    {
        var fac = new FacFile();

        // Standard NWN factions (order matters - index is faction ID)
        fac.FactionList.Add(new Faction { FactionName = "PC", FactionGlobal = 0, FactionParentID = 0xFFFFFFFF });
        fac.FactionList.Add(new Faction { FactionName = "Hostile", FactionGlobal = 1, FactionParentID = 0xFFFFFFFF });
        fac.FactionList.Add(new Faction { FactionName = "Commoner", FactionGlobal = 1, FactionParentID = 0xFFFFFFFF });
        fac.FactionList.Add(new Faction { FactionName = "Merchant", FactionGlobal = 1, FactionParentID = 0xFFFFFFFF });
        fac.FactionList.Add(new Faction { FactionName = "Defender", FactionGlobal = 1, FactionParentID = 0xFFFFFFFF });

        return fac;
    }
}
