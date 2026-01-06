// MDL ASCII Reader - List data parsing
// Partial class for parsing vertex, face, texture, color, and weight lists

using System.Numerics;

namespace Radoub.Formats.Mdl;

public partial class MdlAsciiReader
{
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
}
