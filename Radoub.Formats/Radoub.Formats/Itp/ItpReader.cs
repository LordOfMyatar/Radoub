using Radoub.Formats.Gff;
using Radoub.Formats.Logging;

namespace Radoub.Formats.Itp;

/// <summary>
/// Reader for ITP (Palette) files.
/// ITP files are GFF-based and define palette categories for the toolset.
/// </summary>
public static class ItpReader
{
    /// <summary>
    /// Read an ITP file from a byte array.
    /// </summary>
    public static ItpFile? Read(byte[] data)
    {
        try
        {
            var gff = GffReader.Read(data);
            if (gff == null)
            {
                UnifiedLogger.Log(LogLevel.WARN, "Failed to parse ITP: GFF read returned null", "ItpReader", "Itp");
                return null;
            }

            return ParseItp(gff);
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.ERROR, $"Failed to read ITP: {ex.GetType().Name}: {ex.Message}", "ItpReader", "Itp");
            return null;
        }
    }

    /// <summary>
    /// Read an ITP file from a file path.
    /// </summary>
    public static ItpFile? Read(string filePath)
    {
        var data = File.ReadAllBytes(filePath);
        return Read(data);
    }

    private static ItpFile ParseItp(GffFile gff)
    {
        var itp = new ItpFile
        {
            FileType = gff.FileType,
            FileVersion = gff.FileVersion
        };

        var root = gff.RootStruct;

        // RESTYPE - only in skeleton palettes
        var resTypeField = root.GetField("RESTYPE");
        if (resTypeField?.Value is ushort resType)
        {
            itp.ResType = resType;
        }

        // NEXT_USEABLE_ID - only in skeleton palettes
        var nextIdField = root.GetField("NEXT_USEABLE_ID");
        if (nextIdField?.Value is byte nextId)
        {
            itp.NextUseableId = nextId;
        }

        // MAIN list - the root of the tree
        var mainField = root.GetField("MAIN");
        if (mainField?.Value is GffList mainList)
        {
            foreach (var nodeStruct in mainList.Elements)
            {
                var node = ParseNode(nodeStruct);
                if (node != null)
                {
                    itp.MainNodes.Add(node);
                }
            }
        }

        return itp;
    }

    private static PaletteNode? ParseNode(GffStruct nodeStruct)
    {
        // Determine if this is a category (has ID field) or branch (has LIST of non-blueprints)
        var idField = nodeStruct.GetField("ID");
        var listField = nodeStruct.GetField("LIST");
        var resRefField = nodeStruct.GetField("RESREF");

        // Blueprint node - has RESREF
        if (resRefField?.Value is string resRef && !string.IsNullOrEmpty(resRef))
        {
            return ParseBlueprintNode(nodeStruct, resRef);
        }

        // Category node - has ID field
        if (idField?.Value is byte categoryId)
        {
            return ParseCategoryNode(nodeStruct, categoryId, listField);
        }

        // Branch node - has LIST without ID
        if (listField?.Value is GffList)
        {
            return ParseBranchNode(nodeStruct, listField);
        }

        // Unknown node type - treat as branch without children
        return ParseBranchNode(nodeStruct, null);
    }

    private static PaletteBlueprintNode ParseBlueprintNode(GffStruct nodeStruct, string resRef)
    {
        var node = new PaletteBlueprintNode
        {
            ResRef = resRef
        };

        ParseCommonFields(nodeStruct, node);

        // CR - creature challenge rating
        var crField = nodeStruct.GetField("CR");
        if (crField?.Value is float cr)
        {
            node.ChallengeRating = cr;
        }

        // FACTION - creature faction
        var factionField = nodeStruct.GetField("FACTION");
        if (factionField?.Value is string faction)
        {
            node.Faction = faction;
        }

        return node;
    }

    private static PaletteCategoryNode ParseCategoryNode(GffStruct nodeStruct, byte id, GffField? listField)
    {
        var node = new PaletteCategoryNode
        {
            Id = id
        };

        ParseCommonFields(nodeStruct, node);

        // Parse child blueprints if present
        if (listField?.Value is GffList list)
        {
            foreach (var childStruct in list.Elements)
            {
                var childNode = ParseNode(childStruct);
                if (childNode is PaletteBlueprintNode blueprint)
                {
                    node.Blueprints.Add(blueprint);
                }
            }
        }

        return node;
    }

    private static PaletteBranchNode ParseBranchNode(GffStruct nodeStruct, GffField? listField)
    {
        var node = new PaletteBranchNode();

        ParseCommonFields(nodeStruct, node);

        // Parse children
        if (listField?.Value is GffList list)
        {
            foreach (var childStruct in list.Elements)
            {
                var childNode = ParseNode(childStruct);
                if (childNode != null)
                {
                    node.Children.Add(childNode);
                }
            }
        }

        return node;
    }

    private static void ParseCommonFields(GffStruct nodeStruct, PaletteNode node)
    {
        // STRREF - TLK reference
        var strRefField = nodeStruct.GetField("STRREF");
        if (strRefField?.Value is uint strRef)
        {
            node.StrRef = strRef;
        }

        // NAME - direct name
        var nameField = nodeStruct.GetField("NAME");
        if (nameField?.Value is string name)
        {
            node.Name = name;
        }

        // DELETE_ME - editing convenience field
        var deleteField = nodeStruct.GetField("DELETE_ME");
        if (deleteField?.Value is string deleteMe)
        {
            node.DeleteMe = deleteMe;
        }

        // TYPE - display type
        var typeField = nodeStruct.GetField("TYPE");
        if (typeField?.Value is byte displayType)
        {
            node.DisplayType = displayType;
        }
    }
}
