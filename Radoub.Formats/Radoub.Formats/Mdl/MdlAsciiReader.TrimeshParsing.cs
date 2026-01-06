// MDL ASCII Reader - Trimesh property parsing
// Partial class for trimesh/mesh-related properties

using System.Numerics;

namespace Radoub.Formats.Mdl;

public partial class MdlAsciiReader
{
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
}
