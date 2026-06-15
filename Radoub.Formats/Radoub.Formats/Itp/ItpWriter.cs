using Radoub.Formats.Gff;
using static Radoub.Formats.Gff.GffFieldBuilder;

namespace Radoub.Formats.Itp;

/// <summary>
/// Writes ITP (Palette) files to GFF binary. Mirrors ItpReader field conventions:
/// STRREF as DWORD, NAME/DELETE_ME/RESREF/FACTION as CExoString, CR as FLOAT,
/// ID/TYPE as BYTE, RESTYPE as WORD, NEXT_USEABLE_ID as BYTE. Every node struct is Type=0.
/// Reference: BioWare Aurora ITP format, neverwinter.nim. See ItpReader for the inverse mapping.
/// </summary>
public static class ItpWriter
{
    /// <summary>
    /// Write an ITP file to a file path.
    /// </summary>
    public static void Write(ItpFile itp, string filePath)
        => File.WriteAllBytes(filePath, Write(itp));

    /// <summary>
    /// Write an ITP file to a stream.
    /// </summary>
    public static void Write(ItpFile itp, Stream stream)
    {
        var buffer = Write(itp);
        stream.Write(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// Write an ITP file to a byte buffer.
    /// </summary>
    public static byte[] Write(ItpFile itp)
    {
        var gff = new GffFile
        {
            FileType = itp.FileType,
            FileVersion = itp.FileVersion,
            RootStruct = BuildRoot(itp)
        };
        return GffWriter.Write(gff);
    }

    private static GffStruct BuildRoot(ItpFile itp)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        // Skeleton-palette root fields (optional).
        if (itp.ResType is ushort resType) AddWordField(root, "RESTYPE", resType);
        if (itp.NextUseableId is byte nextId) AddByteField(root, "NEXT_USEABLE_ID", nextId);

        var main = new GffList();
        foreach (var node in itp.MainNodes)
            main.Elements.Add(BuildNode(node));
        AddListField(root, "MAIN", main);

        return root;
    }

    private static GffStruct BuildNode(PaletteNode node)
    {
        var s = new GffStruct { Type = 0 };
        WriteCommon(s, node);

        switch (node)
        {
            case PaletteBlueprintNode bp:
                AddCExoStringField(s, "RESREF", bp.ResRef);
                if (bp.ChallengeRating is float cr) AddFloatField(s, "CR", cr);
                if (!string.IsNullOrEmpty(bp.Faction)) AddCExoStringField(s, "FACTION", bp.Faction!);
                break;

            case PaletteCategoryNode cat:
                AddByteField(s, "ID", cat.Id);
                // A category's LIST holds blueprints first, then nested categories/branches.
                if (cat.Blueprints.Count > 0 || cat.Children.Count > 0)
                {
                    var list = new GffList();
                    foreach (var b in cat.Blueprints) list.Elements.Add(BuildNode(b));
                    foreach (var child in cat.Children) list.Elements.Add(BuildNode(child));
                    AddListField(s, "LIST", list);
                }
                break;

            case PaletteBranchNode br:
                var branchList = new GffList();
                foreach (var child in br.Children) branchList.Elements.Add(BuildNode(child));
                AddListField(s, "LIST", branchList);
                break;
        }

        return s;
    }

    private static void WriteCommon(GffStruct s, PaletteNode node)
    {
        if (node.StrRef is uint strRef) AddDwordField(s, "STRREF", strRef);
        if (!string.IsNullOrEmpty(node.Name)) AddCExoStringField(s, "NAME", node.Name!);
        if (!string.IsNullOrEmpty(node.DeleteMe)) AddCExoStringField(s, "DELETE_ME", node.DeleteMe!);
        if (node.DisplayType is byte displayType) AddByteField(s, "TYPE", displayType);
    }
}
