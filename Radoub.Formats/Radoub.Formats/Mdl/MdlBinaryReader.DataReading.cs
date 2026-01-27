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
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
                $"[MDL] ReadVertices BOUNDS CHECK FAILED: offset={offset}, count={count}, rawDataLen={_rawData.Length}, needed={(uint)(offset + count * 12)}");
            return vertices;
        }

        using var stream = new MemoryStream(_rawData);
        using var reader = new BinaryReader(stream);

        stream.Position = offset;
        for (int i = 0; i < count; i++)
        {
            vertices[i] = ReadVector3(reader);
        }

        // Debug: log first few vertices and check for suspicious values
        if (count > 0)
        {
            var sb = new StringBuilder();
            sb.Append($"[MDL] ReadVertices: offset={offset}, count={count}, rawDataLen={_rawData.Length}, first 3: ");
            float maxCoord = 0;
            for (int i = 0; i < Math.Min(3, count); i++)
            {
                sb.Append($"[{vertices[i].X:F3},{vertices[i].Y:F3},{vertices[i].Z:F3}] ");
                maxCoord = Math.Max(maxCoord, Math.Max(Math.Abs(vertices[i].X), Math.Max(Math.Abs(vertices[i].Y), Math.Abs(vertices[i].Z))));
            }
            // Check all vertices for extreme values that might indicate wrong data
            for (int i = 0; i < count; i++)
            {
                maxCoord = Math.Max(maxCoord, Math.Max(Math.Abs(vertices[i].X), Math.Max(Math.Abs(vertices[i].Y), Math.Abs(vertices[i].Z))));
            }
            sb.Append($"maxCoord={maxCoord:F2}");
            if (maxCoord > 10.0f)
            {
                sb.Append(" [SUSPICIOUS - body parts should be < 2m]");
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

        // Debug: log first few UVs to verify they're in valid range [0,1]
        if (count > 0)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"[MDL] ReadTexCoords: offset={offset}, count={count}, first 3: ");
            int outOfRange = 0;
            for (int i = 0; i < Math.Min(3, count); i++)
            {
                sb.Append($"[{coords[i].X:F3},{coords[i].Y:F3}] ");
            }
            // Check for out-of-range UVs
            for (int i = 0; i < count; i++)
            {
                if (coords[i].X < -1 || coords[i].X > 2 || coords[i].Y < -1 || coords[i].Y > 2)
                    outOfRange++;
            }
            if (outOfRange > 0)
            {
                sb.Append($"[WARN: {outOfRange} UVs out of range]");
            }
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG, sb.ToString());
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
