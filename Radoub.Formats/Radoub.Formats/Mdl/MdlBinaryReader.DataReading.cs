// MDL Binary Reader - Raw data reading
// Partial class for reading vertices, faces, texcoords, colors from raw data

using System.Numerics;
using System.Text;

namespace Radoub.Formats.Mdl;

public partial class MdlBinaryReader
{
    private Vector3[] ReadVertices(uint offset, int count)
    {
        var vertices = new Vector3[count];
        if (_rawData.Length == 0 || offset + count * 12 > _rawData.Length)
        {
            return vertices;
        }

        using var stream = new MemoryStream(_rawData);
        using var reader = new BinaryReader(stream);

        stream.Position = offset;
        for (int i = 0; i < count; i++)
        {
            vertices[i] = ReadVector3(reader);
        }

        // Debug: log first few vertices
        if (count > 0)
        {
            var sb = new StringBuilder();
            sb.Append($"[MDL] ReadVertices: offset={offset}, count={count}, first 3: ");
            for (int i = 0; i < Math.Min(3, count); i++)
            {
                sb.Append($"[{vertices[i].X:F3},{vertices[i].Y:F3},{vertices[i].Z:F3}] ");
            }
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG, sb.ToString());
        }

        return vertices;
    }

    private Vector2[] ReadTexCoords(uint offset, int count)
    {
        var coords = new Vector2[count];
        if (_rawData.Length == 0 || offset + count * 8 > _rawData.Length)
        {
            return coords;
        }

        using var stream = new MemoryStream(_rawData);
        using var reader = new BinaryReader(stream);

        stream.Position = offset;
        for (int i = 0; i < count; i++)
        {
            coords[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        }
        return coords;
    }

    private uint[] ReadColors(uint offset, int count)
    {
        var colors = new uint[count];
        if (_rawData.Length == 0 || offset + count * 4 > _rawData.Length)
        {
            return colors;
        }

        using var stream = new MemoryStream(_rawData);
        using var reader = new BinaryReader(stream);

        stream.Position = offset;
        for (int i = 0; i < count; i++)
        {
            colors[i] = reader.ReadUInt32();
        }
        return colors;
    }

    private MdlFace[] ReadFaces(uint offset, int count)
    {
        var faces = new MdlFace[count];
        if (_modelData.Length == 0 || offset + count * FaceSize > _modelData.Length)
        {
            return faces;
        }

        using var stream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(stream);

        stream.Position = offset;
        for (int i = 0; i < count; i++)
        {
            var face = new MdlFace();
            face.PlaneNormal = ReadVector3(reader);
            face.PlaneDistance = reader.ReadSingle();
            face.SurfaceId = reader.ReadInt32();

            // Adjacent faces (3 x int16)
            face.AdjacentFace0 = reader.ReadInt16();
            face.AdjacentFace1 = reader.ReadInt16();
            face.AdjacentFace2 = reader.ReadInt16();

            // Vertex indices (3 x uint16)
            face.VertexIndex0 = reader.ReadUInt16();
            face.VertexIndex1 = reader.ReadUInt16();
            face.VertexIndex2 = reader.ReadUInt16();

            faces[i] = face;
        }
        return faces;
    }
}
