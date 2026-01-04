// MDL ASCII format parser for Neverwinter Nights
// Based on format documentation from NWN Wiki MDL ASCII specification
// License: BSD 3-Clause (compatible with nwnexplorer reference)

using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Radoub.Formats.Mdl;

/// <summary>
/// Parses NWN MDL files in ASCII format.
/// </summary>
public class MdlAsciiReader
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
                    // Ignored - just move on
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

        // Calculate bounding box from mesh nodes
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

    private static string[] Tokenize(string line)
    {
        // Handle quoted strings and regular tokens
        var result = new List<string>();
        var i = 0;

        while (i < line.Length)
        {
            // Skip whitespace
            while (i < line.Length && char.IsWhiteSpace(line[i]))
                i++;

            if (i >= line.Length) break;

            if (line[i] == '"')
            {
                // Quoted string
                var end = line.IndexOf('"', i + 1);
                if (end < 0) end = line.Length;
                result.Add(line.Substring(i + 1, end - i - 1));
                i = end + 1;
            }
            else
            {
                // Regular token
                var start = i;
                while (i < line.Length && !char.IsWhiteSpace(line[i]))
                    i++;
                result.Add(line.Substring(start, i - start));
            }
        }

        return result.ToArray();
    }

    private MdlNode? ParseGeometry()
    {
        MdlNode? root = null;
        var nodeStack = new Stack<MdlNode>();

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
                    return root;

                case "node":
                    if (tokens.Length >= 3)
                    {
                        var node = ParseNode(tokens[1], tokens[2]);
                        if (root == null)
                        {
                            root = node;
                        }
                        else if (nodeStack.Count > 0)
                        {
                            var parent = nodeStack.Peek();
                            node.Parent = parent;
                            parent.Children.Add(node);
                        }
                        nodeStack.Push(node);
                    }
                    else
                    {
                        _currentLine++;
                    }
                    break;

                case "endnode":
                    if (nodeStack.Count > 0)
                        nodeStack.Pop();
                    _currentLine++;
                    break;

                default:
                    _currentLine++;
                    break;
            }
        }

        return root;
    }

    private MdlNode ParseNode(string nodeType, string nodeName)
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
        _currentLine++;

        // Parse node properties
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

            // Check for node end or new node (which parent handles)
            if (prop == "endnode" || prop == "node")
                break;

            // Parse property based on node type
            ParseNodeProperty(node, prop, tokens);
            _currentLine++;
        }

        return node;
    }

    private void ParseNodeProperty(MdlNode node, string prop, string[] tokens)
    {
        // Common properties
        switch (prop)
        {
            case "parent":
                // Parent relationship is handled by node hierarchy
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

    private void ParseTrimeshProperty(MdlTrimeshNode mesh, string prop, string[] tokens)
    {
        switch (prop)
        {
            case "bitmap":
                if (tokens.Length >= 2)
                    mesh.Bitmap = tokens[1];
                break;

            case "texture0":
            case "texture1":
                if (tokens.Length >= 2 && string.IsNullOrEmpty(mesh.Bitmap))
                    mesh.Bitmap = tokens[1];
                break;

            case "ambient":
                if (tokens.Length >= 4)
                    mesh.Ambient = new Vector3(
                        ParseFloat(tokens[1]),
                        ParseFloat(tokens[2]),
                        ParseFloat(tokens[3]));
                break;

            case "diffuse":
                if (tokens.Length >= 4)
                    mesh.Diffuse = new Vector3(
                        ParseFloat(tokens[1]),
                        ParseFloat(tokens[2]),
                        ParseFloat(tokens[3]));
                break;

            case "specular":
                if (tokens.Length >= 4)
                    mesh.Specular = new Vector3(
                        ParseFloat(tokens[1]),
                        ParseFloat(tokens[2]),
                        ParseFloat(tokens[3]));
                break;

            case "shininess":
                if (tokens.Length >= 2)
                    mesh.Shininess = ParseFloat(tokens[1]);
                break;

            case "alpha":
                if (tokens.Length >= 2)
                    mesh.Alpha = ParseFloat(tokens[1]);
                break;

            case "selfillumcolor":
                if (tokens.Length >= 4)
                    mesh.SelfIllumColor = new Vector3(
                        ParseFloat(tokens[1]),
                        ParseFloat(tokens[2]),
                        ParseFloat(tokens[3]));
                break;

            case "render":
                if (tokens.Length >= 2)
                    mesh.Render = ParseInt(tokens[1]) != 0;
                break;

            case "shadow":
                if (tokens.Length >= 2)
                    mesh.Shadow = ParseInt(tokens[1]) != 0;
                break;

            case "beaming":
                if (tokens.Length >= 2)
                    mesh.Beaming = ParseInt(tokens[1]) != 0;
                break;

            case "inheritcolor":
                if (tokens.Length >= 2)
                    mesh.InheritColor = ParseInt(tokens[1]) != 0;
                break;

            case "rotatetexture":
                if (tokens.Length >= 2)
                    mesh.RotateTexture = ParseInt(tokens[1]) != 0;
                break;

            case "transparencyhint":
                if (tokens.Length >= 2)
                    mesh.TransparencyHint = ParseInt(tokens[1]);
                break;

            case "tilefade":
                if (tokens.Length >= 2)
                    mesh.Tilefade = ParseInt(tokens[1]);
                break;

            case "renderorder":
            case "render_order":
                if (tokens.Length >= 2)
                    mesh.RenderOrder = ParseInt(tokens[1]);
                break;

            case "verts":
                ParseVertexList(mesh, tokens);
                break;

            case "tverts":
                ParseTextureVertexList(mesh, tokens, 0);
                break;

            case "tverts1":
            case "tverts2":
            case "tverts3":
                var tvertIndex = prop[^1] - '0';
                ParseTextureVertexList(mesh, tokens, tvertIndex);
                break;

            case "faces":
                ParseFaceList(mesh, tokens);
                break;

            case "colors":
                ParseColorList(mesh, tokens);
                break;

            // Dangly mesh properties
            case "constraints":
                if (mesh is MdlDanglyNode dangly)
                    ParseConstraintList(dangly, tokens);
                break;

            case "displacement":
                if (mesh is MdlDanglyNode d1 && tokens.Length >= 2)
                    d1.Displacement = ParseFloat(tokens[1]);
                break;

            case "tightness":
                if (mesh is MdlDanglyNode d2 && tokens.Length >= 2)
                    d2.Tightness = ParseFloat(tokens[1]);
                break;

            case "period":
                if (mesh is MdlDanglyNode d3 && tokens.Length >= 2)
                    d3.Period = ParseFloat(tokens[1]);
                break;

            // Skin mesh properties
            case "weights":
                if (mesh is MdlSkinNode skin)
                    ParseWeightList(skin, tokens);
                break;
        }
    }

    private void ParseVertexList(MdlTrimeshNode mesh, string[] tokens)
    {
        if (tokens.Length < 2) return;
        var count = ParseInt(tokens[1]);
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();

        _currentLine++;
        while (vertices.Count < count && _currentLine < _lines.Length)
        {
            var line = CurrentLine().Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                _currentLine++;
                continue;
            }

            var parts = Tokenize(line);
            if (parts.Length >= 3)
            {
                vertices.Add(new Vector3(
                    ParseFloat(parts[0]),
                    ParseFloat(parts[1]),
                    ParseFloat(parts[2])));

                // Some formats include normals inline
                if (parts.Length >= 6)
                {
                    normals.Add(new Vector3(
                        ParseFloat(parts[3]),
                        ParseFloat(parts[4]),
                        ParseFloat(parts[5])));
                }
            }

            _currentLine++;
        }

        mesh.Vertices = vertices.ToArray();
        if (normals.Count > 0)
            mesh.Normals = normals.ToArray();

        _currentLine--; // Back up so main loop can advance
    }

    private void ParseTextureVertexList(MdlTrimeshNode mesh, string[] tokens, int setIndex)
    {
        if (tokens.Length < 2) return;
        var count = ParseInt(tokens[1]);
        var tverts = new List<Vector2>();

        _currentLine++;
        while (tverts.Count < count && _currentLine < _lines.Length)
        {
            var line = CurrentLine().Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                _currentLine++;
                continue;
            }

            var parts = Tokenize(line);
            if (parts.Length >= 2)
            {
                tverts.Add(new Vector2(
                    ParseFloat(parts[0]),
                    ParseFloat(parts[1])));
            }

            _currentLine++;
        }

        // Ensure texture coord array is large enough
        if (mesh.TextureCoords.Length <= setIndex)
        {
            var newArray = new Vector2[setIndex + 1][];
            Array.Copy(mesh.TextureCoords, newArray, mesh.TextureCoords.Length);
            mesh.TextureCoords = newArray;
        }

        mesh.TextureCoords[setIndex] = tverts.ToArray();
        _currentLine--; // Back up so main loop can advance
    }

    private void ParseFaceList(MdlTrimeshNode mesh, string[] tokens)
    {
        if (tokens.Length < 2) return;
        var count = ParseInt(tokens[1]);
        var faces = new List<MdlFace>();

        _currentLine++;
        while (faces.Count < count && _currentLine < _lines.Length)
        {
            var line = CurrentLine().Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                _currentLine++;
                continue;
            }

            var parts = Tokenize(line);
            // Format: v1 v2 v3 shading_group tv1 tv2 tv3 material
            if (parts.Length >= 3)
            {
                var face = new MdlFace
                {
                    VertexIndex0 = ParseInt(parts[0]),
                    VertexIndex1 = ParseInt(parts[1]),
                    VertexIndex2 = ParseInt(parts[2]),
                    SurfaceId = parts.Length >= 8 ? ParseInt(parts[7]) : 0
                };

                // Calculate face normal from vertices if available
                if (mesh.Vertices.Length > face.VertexIndex2)
                {
                    var v0 = mesh.Vertices[face.VertexIndex0];
                    var v1 = mesh.Vertices[face.VertexIndex1];
                    var v2 = mesh.Vertices[face.VertexIndex2];
                    var edge1 = v1 - v0;
                    var edge2 = v2 - v0;
                    face.PlaneNormal = Vector3.Normalize(Vector3.Cross(edge1, edge2));
                    face.PlaneDistance = -Vector3.Dot(face.PlaneNormal, v0);
                }

                faces.Add(face);
            }

            _currentLine++;
        }

        mesh.Faces = faces.ToArray();
        _currentLine--; // Back up so main loop can advance
    }

    private void ParseColorList(MdlTrimeshNode mesh, string[] tokens)
    {
        if (tokens.Length < 2) return;
        var count = ParseInt(tokens[1]);
        var colors = new List<uint>();

        _currentLine++;
        while (colors.Count < count && _currentLine < _lines.Length)
        {
            var line = CurrentLine().Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                _currentLine++;
                continue;
            }

            var parts = Tokenize(line);
            if (parts.Length >= 3)
            {
                var r = (byte)(ParseFloat(parts[0]) * 255);
                var g = (byte)(ParseFloat(parts[1]) * 255);
                var b = (byte)(ParseFloat(parts[2]) * 255);
                var a = parts.Length >= 4 ? (byte)(ParseFloat(parts[3]) * 255) : (byte)255;
                colors.Add((uint)((a << 24) | (b << 16) | (g << 8) | r));
            }

            _currentLine++;
        }

        mesh.VertexColors = colors.ToArray();
        _currentLine--; // Back up so main loop can advance
    }

    private void ParseConstraintList(MdlDanglyNode dangly, string[] tokens)
    {
        if (tokens.Length < 2) return;
        var count = ParseInt(tokens[1]);
        var constraints = new List<float>();

        _currentLine++;
        while (constraints.Count < count && _currentLine < _lines.Length)
        {
            var line = CurrentLine().Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                _currentLine++;
                continue;
            }

            constraints.Add(ParseFloat(line));
            _currentLine++;
        }

        dangly.Constraints = constraints.ToArray();
        _currentLine--; // Back up so main loop can advance
    }

    private void ParseWeightList(MdlSkinNode skin, string[] tokens)
    {
        if (tokens.Length < 2) return;
        var count = ParseInt(tokens[1]);
        var weights = new List<MdlBoneWeight>();

        _currentLine++;
        while (weights.Count < count && _currentLine < _lines.Length)
        {
            var line = CurrentLine().Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                _currentLine++;
                continue;
            }

            var parts = Tokenize(line);
            // Format: bone0 weight0 [bone1 weight1] [bone2 weight2] [bone3 weight3]
            var bw = new MdlBoneWeight { Bone0 = -1, Bone1 = -1, Bone2 = -1, Bone3 = -1 };

            for (int i = 0; i + 1 < parts.Length; i += 2)
            {
                var bone = ParseInt(parts[i]);
                var weight = ParseFloat(parts[i + 1]);

                switch (i / 2)
                {
                    case 0: bw.Bone0 = bone; bw.Weight0 = weight; break;
                    case 1: bw.Bone1 = bone; bw.Weight1 = weight; break;
                    case 2: bw.Bone2 = bone; bw.Weight2 = weight; break;
                    case 3: bw.Bone3 = bone; bw.Weight3 = weight; break;
                }
            }

            weights.Add(bw);
            _currentLine++;
        }

        skin.BoneWeights = weights.ToArray();
        _currentLine--; // Back up so main loop can advance
    }

    private void ParseLightProperty(MdlLightNode light, string prop, string[] tokens)
    {
        switch (prop)
        {
            case "color":
                if (tokens.Length >= 4)
                    light.Color = new Vector3(
                        ParseFloat(tokens[1]),
                        ParseFloat(tokens[2]),
                        ParseFloat(tokens[3]));
                break;

            case "radius":
                if (tokens.Length >= 2)
                    light.Radius = ParseFloat(tokens[1]);
                break;

            case "multiplier":
                if (tokens.Length >= 2)
                    light.Multiplier = ParseFloat(tokens[1]);
                break;

            case "isdynamic":
                if (tokens.Length >= 2)
                    light.IsDynamic = ParseInt(tokens[1]) != 0;
                break;

            case "affectdynamic":
                if (tokens.Length >= 2)
                    light.AffectDynamic = ParseInt(tokens[1]) != 0;
                break;

            case "shadow":
                if (tokens.Length >= 2)
                    light.Shadow = ParseInt(tokens[1]) != 0;
                break;

            case "flareradius":
                if (tokens.Length >= 2)
                    light.FlareRadius = ParseFloat(tokens[1]);
                break;

            case "priority":
                if (tokens.Length >= 2)
                    light.Priority = ParseInt(tokens[1]);
                break;

            case "ambientonly":
                if (tokens.Length >= 2)
                    light.AmbientOnly = ParseInt(tokens[1]) != 0;
                break;

            case "fading":
                if (tokens.Length >= 2)
                    light.Fading = ParseInt(tokens[1]) != 0;
                break;
        }
    }

    private void ParseEmitterProperty(MdlEmitterNode emitter, string prop, string[] tokens)
    {
        switch (prop)
        {
            case "update":
                if (tokens.Length >= 2)
                    emitter.Update = tokens[1];
                break;

            case "render":
                if (tokens.Length >= 2)
                    emitter.RenderMethod = tokens[1];
                break;

            case "blend":
                if (tokens.Length >= 2)
                    emitter.Blend = tokens[1];
                break;

            case "texture":
                if (tokens.Length >= 2)
                    emitter.Texture = tokens[1];
                break;

            case "spawntype":
                if (tokens.Length >= 2)
                    emitter.SpawnType = tokens[1];
                break;

            case "xgrid":
                if (tokens.Length >= 2)
                    emitter.XGrid = ParseInt(tokens[1]);
                break;

            case "ygrid":
                if (tokens.Length >= 2)
                    emitter.YGrid = ParseInt(tokens[1]);
                break;

            case "renderorder":
            case "render_order":
                if (tokens.Length >= 2)
                    emitter.RenderOrder = ParseInt(tokens[1]);
                break;

            case "inherit":
                if (tokens.Length >= 2)
                    emitter.Inherit = ParseInt(tokens[1]) != 0;
                break;

            case "inheritlocal":
                if (tokens.Length >= 2)
                    emitter.InheritLocal = ParseInt(tokens[1]) != 0;
                break;

            case "inheritpart":
                if (tokens.Length >= 2)
                    emitter.InheritPart = ParseInt(tokens[1]) != 0;
                break;

            case "affectedbywind":
                if (tokens.Length >= 2)
                    emitter.AffectedByWind = ParseInt(tokens[1]) != 0;
                break;

            case "m_issplat":
            case "issplat":
                if (tokens.Length >= 2)
                    emitter.IsSplat = ParseInt(tokens[1]) != 0;
                break;

            case "bounce":
                if (tokens.Length >= 2)
                    emitter.Bounce = ParseInt(tokens[1]) != 0;
                break;

            case "random":
                if (tokens.Length >= 2)
                    emitter.Random = ParseInt(tokens[1]) != 0;
                break;

            case "loop":
                if (tokens.Length >= 2)
                    emitter.Loop = ParseInt(tokens[1]) != 0;
                break;

            case "p2p":
                if (tokens.Length >= 2)
                    emitter.P2P = ParseInt(tokens[1]) != 0;
                break;

            case "p2p_bezier":
                if (tokens.Length >= 2)
                    emitter.P2PBezier = ParseInt(tokens[1]) != 0;
                break;
        }
    }

    private void ParseReferenceProperty(MdlReferenceNode reference, string prop, string[] tokens)
    {
        switch (prop)
        {
            case "refmodel":
                if (tokens.Length >= 2)
                    reference.RefModel = tokens[1];
                break;

            case "reattachable":
                if (tokens.Length >= 2)
                    reference.Reattachable = ParseInt(tokens[1]) != 0;
                break;
        }
    }

    private MdlAnimation? ParseAnimation(string animName)
    {
        var anim = new MdlAnimation { Name = animName };

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
                case "doneanim":
                    _currentLine++;
                    return anim;

                case "length":
                    if (tokens.Length >= 2)
                        anim.Length = ParseFloat(tokens[1]);
                    _currentLine++;
                    break;

                case "transtime":
                    if (tokens.Length >= 2)
                        anim.TransitionTime = ParseFloat(tokens[1]);
                    _currentLine++;
                    break;

                case "animroot":
                    if (tokens.Length >= 2)
                        anim.AnimRoot = tokens[1];
                    _currentLine++;
                    break;

                case "event":
                    if (tokens.Length >= 3)
                    {
                        anim.Events.Add(new MdlAnimationEvent
                        {
                            Time = ParseFloat(tokens[1]),
                            EventName = tokens[2]
                        });
                    }
                    _currentLine++;
                    break;

                case "node":
                    // Animation nodes - simplified handling
                    _currentLine++;
                    SkipToEndNode();
                    break;

                default:
                    _currentLine++;
                    break;
            }
        }

        return anim;
    }

    private void SkipToEndNode()
    {
        int depth = 1;
        while (_currentLine < _lines.Length && depth > 0)
        {
            var line = CurrentLine().Trim();
            if (line.StartsWith("node ", StringComparison.OrdinalIgnoreCase))
                depth++;
            else if (line.Equals("endnode", StringComparison.OrdinalIgnoreCase))
                depth--;
            _currentLine++;
        }
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

    private static MdlClassification ParseClassification(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "effect" => MdlClassification.Effect,
            "tile" => MdlClassification.Tile,
            "character" => MdlClassification.Character,
            "door" => MdlClassification.Door,
            _ => MdlClassification.None
        };
    }

    private static float ParseFloat(string value)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;
        return 0f;
    }

    private static int ParseInt(string value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;
        return 0;
    }

    private static Quaternion QuaternionFromAxisAngle(float x, float y, float z, float angle)
    {
        var axis = new Vector3(x, y, z);
        if (axis.LengthSquared() < 0.0001f)
            return Quaternion.Identity;

        axis = Vector3.Normalize(axis);
        return Quaternion.CreateFromAxisAngle(axis, angle);
    }
}
