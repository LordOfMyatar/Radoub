using Radoub.Formats.Gff;

namespace Radoub.Formats.Fac;

/// <summary>
/// Writes FAC (Faction) files to binary GFF format.
/// FAC files are GFF-based with file type "FAC ".
/// </summary>
public static class FacWriter
{
    /// <summary>
    /// Write a FAC file to a file path.
    /// </summary>
    public static void Write(FacFile fac, string filePath)
    {
        var buffer = Write(fac);
        File.WriteAllBytes(filePath, buffer);
    }

    /// <summary>
    /// Write a FAC file to a stream.
    /// </summary>
    public static void Write(FacFile fac, Stream stream)
    {
        var buffer = Write(fac);
        stream.Write(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// Write a FAC file to a byte buffer.
    /// </summary>
    public static byte[] Write(FacFile fac)
    {
        var gff = new GffFile
        {
            FileType = fac.FileType.PadRight(4).Substring(0, 4),
            FileVersion = fac.FileVersion.PadRight(4).Substring(0, 4)
        };

        gff.RootStruct = new GffStruct { Type = 0xFFFFFFFF };

        // Build FactionList
        var factionStructs = new List<GffStruct>();
        foreach (var faction in fac.FactionList)
        {
            var s = new GffStruct { Type = 0 };
            GffFieldBuilder.AddCExoStringField(s, "FactionName", faction.FactionName);
            GffFieldBuilder.AddWordField(s, "FactionGlobal", faction.FactionGlobal);
            GffFieldBuilder.AddDwordField(s, "FactionParentID", faction.FactionParentID);
            factionStructs.Add(s);
        }
        GffFieldBuilder.AddListField(gff.RootStruct, "FactionList", factionStructs);

        // Build RepList
        var repStructs = new List<GffStruct>();
        foreach (var rep in fac.RepList)
        {
            var s = new GffStruct { Type = 0 };
            GffFieldBuilder.AddDwordField(s, "FactionID1", rep.FactionID1);
            GffFieldBuilder.AddDwordField(s, "FactionID2", rep.FactionID2);
            GffFieldBuilder.AddDwordField(s, "FactionRep", rep.FactionRep);
            repStructs.Add(s);
        }
        GffFieldBuilder.AddListField(gff.RootStruct, "RepList", repStructs);

        return GffWriter.Write(gff);
    }
}
