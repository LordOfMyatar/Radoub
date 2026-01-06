// MDL ASCII Reader - Node parsing
// Partial class for node creation and property routing

using System.Numerics;

namespace Radoub.Formats.Mdl;

public partial class MdlAsciiReader
{
    /// <summary>
    /// Parse a node and return the node along with its declared parent name.
    /// NWN ASCII MDL uses flat node list with explicit "parent" properties.
    /// </summary>
    private (MdlNode node, string? parentName) ParseNodeWithParent(string nodeType, string nodeName)
    {
        MdlNode node = nodeType.ToLowerInvariant() switch
        {
            "trimesh" => new MdlTrimeshNode { NodeType = MdlNodeType.Trimesh },
            "skin" => new MdlSkinNode { NodeType = MdlNodeType.Skin },
            "danglymesh" => new MdlDanglyNode { NodeType = MdlNodeType.Dangly },
            "animmesh" => new MdlAnimNode { NodeType = MdlNodeType.Anim },
            "aabb" => new MdlAabbNode { NodeType = MdlNodeType.Aabb },
            "light" => new MdlLightNode { NodeType = MdlNodeType.Light },
            "emitter" => new MdlEmitterNode { NodeType = MdlNodeType.Emitter },
            "reference" => new MdlReferenceNode { NodeType = MdlNodeType.Reference },
            _ => new MdlNode { NodeType = MdlNodeType.Dummy }
        };

        node.Name = nodeName;
        string? parentName = null;
        _currentLine++;

        while (_currentLine < _lines.Length)
        {
            var line = CurrentLine();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            {
                _currentLine++;
                continue;
            }

            var tokens = Tokenize(line);
            if (tokens.Length == 0)
            {
                _currentLine++;
                continue;
            }

            var prop = tokens[0].ToLowerInvariant();

            if (prop == "endnode" || prop == "node")
                break;

            if (prop == "parent" && tokens.Length >= 2)
            {
                parentName = tokens[1];
            }

            ParseNodeProperty(node, prop, tokens);
            _currentLine++;
        }

        return (node, parentName);
    }

    private void ParseNodeProperty(MdlNode node, string prop, string[] tokens)
    {
        // Common properties
        switch (prop)
        {
            case "parent":
                return;

            case "position":
                if (tokens.Length >= 4)
                    node.Position = new Vector3(
                        ParseFloat(tokens[1]),
                        ParseFloat(tokens[2]),
                        ParseFloat(tokens[3]));
                return;

            case "orientation":
                if (tokens.Length >= 5)
                    node.Orientation = QuaternionFromAxisAngle(
                        ParseFloat(tokens[1]),
                        ParseFloat(tokens[2]),
                        ParseFloat(tokens[3]),
                        ParseFloat(tokens[4]));
                return;

            case "scale":
                if (tokens.Length >= 2)
                    node.Scale = ParseFloat(tokens[1]);
                return;

            case "wirecolor":
                if (tokens.Length >= 4)
                    node.Wirecolor = new Vector3(
                        ParseFloat(tokens[1]),
                        ParseFloat(tokens[2]),
                        ParseFloat(tokens[3]));
                return;
        }

        // Type-specific properties
        if (node is MdlTrimeshNode mesh)
            ParseTrimeshProperty(mesh, prop, tokens);
        else if (node is MdlLightNode light)
            ParseLightProperty(light, prop, tokens);
        else if (node is MdlEmitterNode emitter)
            ParseEmitterProperty(emitter, prop, tokens);
        else if (node is MdlReferenceNode reference)
            ParseReferenceProperty(reference, prop, tokens);
    }
}
