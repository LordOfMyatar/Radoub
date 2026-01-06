// MDL ASCII format parser for Neverwinter Nights
// Based on format documentation from NWN Wiki MDL ASCII specification
// License: BSD 3-Clause (compatible with nwnexplorer reference)

using System.Numerics;

namespace Radoub.Formats.Mdl;

/// <summary>
/// Parses NWN MDL files in ASCII format.
/// Split into partial classes for maintainability.
/// </summary>
public partial class MdlAsciiReader
{
    private string[] _lines = Array.Empty<string>();
    private int _currentLine;
    private MdlModel? _model;

    /// <summary>
    /// Parse an MDL file from a string.
    /// </summary>
    public MdlModel Parse(string content)
    {
        _lines = content.Split('\n')
            .Select(l => l.Trim('\r'))
            .ToArray();
        _currentLine = 0;
        _model = new MdlModel { IsBinary = false };

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

            switch (tokens[0].ToLowerInvariant())
            {
                case "newmodel":
                    if (tokens.Length > 1)
                        _model.Name = tokens[1];
                    _currentLine++;
                    break;

                case "filedependancy":
                case "filedependency":
                    _currentLine++;
                    break;

                case "setsupermodel":
                    if (tokens.Length > 2)
                        _model.SuperModel = tokens[2];
                    _currentLine++;
                    break;

                case "classification":
                    if (tokens.Length > 1)
                        _model.Classification = ParseClassification(tokens[1]);
                    _currentLine++;
                    break;

                case "setanimationscale":
                    if (tokens.Length > 1)
                        _model.AnimationScale = ParseFloat(tokens[1]);
                    _currentLine++;
                    break;

                case "beginmodelgeom":
                    _currentLine++;
                    _model.GeometryRoot = ParseGeometry();
                    break;

                case "newanim":
                    _currentLine++;
                    var anim = ParseAnimation(tokens.Length > 1 ? tokens[1] : "");
                    if (anim != null)
                        _model.Animations.Add(anim);
                    break;

                case "donemodel":
                    _currentLine++;
                    break;

                default:
                    _currentLine++;
                    break;
            }
        }

        CalculateBounds();
        return _model;
    }

    /// <summary>
    /// Parse an MDL file from a stream.
    /// </summary>
    public MdlModel Parse(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    private string CurrentLine() => _currentLine < _lines.Length ? _lines[_currentLine] : "";

    private MdlNode? ParseGeometry()
    {
        // NWN ASCII MDL uses a FLAT node list with "parent" properties, NOT nested nodes.
        var nodesByName = new Dictionary<string, MdlNode>(StringComparer.OrdinalIgnoreCase);
        var parentNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG, "[MDL-ASCII] ParseGeometry START");

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

            switch (tokens[0].ToLowerInvariant())
            {
                case "endmodelgeom":
                    _currentLine++;
                    return LinkNodeHierarchy(nodesByName, parentNames);

                case "node":
                    if (tokens.Length >= 3)
                    {
                        var nodeType = tokens[1];
                        var nodeName = tokens[2];
                        var (node, parentName) = ParseNodeWithParent(nodeType, nodeName);

                        nodesByName[nodeName] = node;
                        if (!string.IsNullOrEmpty(parentName) && !parentName.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                        {
                            parentNames[nodeName] = parentName;
                        }

                        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
                            $"[MDL-ASCII] Parsed node: name='{nodeName}', type='{nodeType}', parent='{parentName ?? "NULL"}'");
                    }
                    else
                    {
                        _currentLine++;
                    }
                    break;

                case "endnode":
                    _currentLine++;
                    break;

                default:
                    _currentLine++;
                    break;
            }
        }

        return LinkNodeHierarchy(nodesByName, parentNames);
    }

    /// <summary>
    /// Link nodes into hierarchy based on parent names, returns root node.
    /// </summary>
    private MdlNode? LinkNodeHierarchy(Dictionary<string, MdlNode> nodesByName, Dictionary<string, string> parentNames)
    {
        MdlNode? root = null;

        foreach (var kvp in parentNames)
        {
            var childName = kvp.Key;
            var parentName = kvp.Value;

            if (nodesByName.TryGetValue(childName, out var child) &&
                nodesByName.TryGetValue(parentName, out var parent))
            {
                child.Parent = parent;
                parent.Children.Add(child);
            }
        }

        foreach (var node in nodesByName.Values)
        {
            if (node.Parent == null)
            {
                if (node.Children.Count > 0 || root == null)
                {
                    root = node;
                }
            }
        }

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
            $"[MDL-ASCII] ParseGeometry END: root={root?.Name ?? "null"}, rootChildren={root?.Children.Count ?? 0}, totalNodes={nodesByName.Count}");

        return root;
    }

    private void CalculateBounds()
    {
        if (_model == null) return;

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        var hasAnyVerts = false;

        foreach (var mesh in _model.GetMeshNodes())
        {
            foreach (var v in mesh.Vertices)
            {
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
                hasAnyVerts = true;
            }
        }

        if (hasAnyVerts)
        {
            _model.BoundingMin = min;
            _model.BoundingMax = max;
            var center = (min + max) * 0.5f;
            _model.Radius = Vector3.Distance(center, max);
        }
    }
}
