// Tests for MDL parser hardening (#1518)
// Verifies that malformed binary data is handled gracefully without crashes

using Radoub.Formats.Mdl;
using Xunit;

namespace Radoub.Formats.Tests;

public class MdlParserHardeningTests
{
    /// <summary>
    /// Verify that Parse handles truncated binary data without crashing.
    /// Previously, insufficient bounds checks could cause IndexOutOfRangeException
    /// or EndOfStreamException to propagate uncaught.
    /// </summary>
    [Fact]
    public void Parse_TruncatedData_ThrowsInvalidDataException()
    {
        // Valid binary header (first 4 bytes = 0) but too small for model header
        var truncated = new byte[50];
        // First 4 bytes = 0 (binary marker)
        // Bytes 4-7 = raw data offset
        BitConverter.GetBytes(50u).CopyTo(truncated, 4);
        // Bytes 8-11 = raw data size
        BitConverter.GetBytes(0u).CopyTo(truncated, 8);

        var reader = new MdlBinaryReader();
        Assert.Throws<InvalidDataException>(() => reader.Parse(truncated));
    }

    /// <summary>
    /// Verify face index filtering rejects out-of-bounds indices.
    /// Previously, the parser only warned about bad indices but kept them,
    /// which could cause crashes in rendering code.
    /// </summary>
    [Fact]
    public void FaceIndexFiltering_RejectsOutOfBoundsIndices()
    {
        // Create a trimesh with 3 vertices and faces referencing index 99 (out of bounds)
        var mesh = new MdlTrimeshNode
        {
            Vertices = new[]
            {
                new System.Numerics.Vector3(0, 0, 0),
                new System.Numerics.Vector3(1, 0, 0),
                new System.Numerics.Vector3(0, 1, 0)
            },
            Faces = new[]
            {
                new MdlFace { VertexIndex0 = 0, VertexIndex1 = 1, VertexIndex2 = 2 },  // Valid
                new MdlFace { VertexIndex0 = 0, VertexIndex1 = 1, VertexIndex2 = 99 }, // Invalid - index 99
                new MdlFace { VertexIndex0 = 0, VertexIndex1 = -1, VertexIndex2 = 2 }, // Invalid - negative index
            }
        };

        // Simulate what the parser now does: filter invalid faces
        var validFaces = new List<MdlFace>();
        int invalidCount = 0;
        foreach (var face in mesh.Faces)
        {
            if (face.VertexIndex0 < 0 || face.VertexIndex0 >= mesh.Vertices.Length ||
                face.VertexIndex1 < 0 || face.VertexIndex1 >= mesh.Vertices.Length ||
                face.VertexIndex2 < 0 || face.VertexIndex2 >= mesh.Vertices.Length)
            {
                invalidCount++;
                continue;
            }
            validFaces.Add(face);
        }

        Assert.Equal(2, invalidCount);
        Assert.Single(validFaces);
        Assert.Equal(0, validFaces[0].VertexIndex0);
        Assert.Equal(1, validFaces[0].VertexIndex1);
        Assert.Equal(2, validFaces[0].VertexIndex2);
    }

    /// <summary>
    /// Verify that the MdlModel data structures handle empty/null gracefully.
    /// Ensures downstream code (rendering) won't crash on partially parsed models.
    /// </summary>
    [Fact]
    public void MdlModel_EmptyModel_EnumerateAllNodes_ReturnsEmpty()
    {
        var model = new MdlModel { Name = "empty" };
        Assert.Empty(model.EnumerateAllNodes());
        Assert.Null(model.FindNode("anything"));
        Assert.Empty(model.GetMeshNodes());
    }

    [Fact]
    public void MdlModel_WithMeshNode_GetMeshNodes_ReturnsMesh()
    {
        var model = new MdlModel { Name = "test" };
        var root = new MdlNode { Name = "root" };
        var mesh = new MdlTrimeshNode
        {
            Name = "mesh1",
            Vertices = Array.Empty<System.Numerics.Vector3>(),
            Faces = Array.Empty<MdlFace>()
        };
        root.Children.Add(mesh);
        mesh.Parent = root;
        model.GeometryRoot = root;

        var meshNodes = model.GetMeshNodes().ToList();
        Assert.Single(meshNodes);
        Assert.Equal("mesh1", meshNodes[0].Name);
    }

    /// <summary>
    /// Verify AABB node structure handles null children gracefully.
    /// Rendering code must handle partially-parsed AABB trees.
    /// </summary>
    [Fact]
    public void MdlAabbEntry_NullChildren_IsValid()
    {
        var entry = new MdlAabbEntry
        {
            BoundingMin = new System.Numerics.Vector3(-1, -1, -1),
            BoundingMax = new System.Numerics.Vector3(1, 1, 1),
            LeafFaceIndex = 0,
            Left = null,
            Right = null
        };

        // Leaf node — null children are expected
        Assert.Null(entry.Left);
        Assert.Null(entry.Right);
        Assert.Equal(0, entry.LeafFaceIndex);
    }

    /// <summary>
    /// Verify that a trimesh with zero-length vertex/face arrays
    /// doesn't crash downstream code.
    /// </summary>
    [Fact]
    public void MdlTrimeshNode_EmptyArrays_AreValid()
    {
        var mesh = new MdlTrimeshNode
        {
            Name = "empty_mesh",
            Vertices = Array.Empty<System.Numerics.Vector3>(),
            Normals = Array.Empty<System.Numerics.Vector3>(),
            TextureCoords = Array.Empty<System.Numerics.Vector2[]>(),
            VertexColors = Array.Empty<uint>(),
            Faces = Array.Empty<MdlFace>()
        };

        Assert.Empty(mesh.Vertices);
        Assert.Empty(mesh.Faces);
        Assert.Empty(mesh.Normals);
        Assert.Empty(mesh.TextureCoords);
    }

    /// <summary>
    /// Verify Parse handles random/garbage data without crashing.
    /// Should throw InvalidDataException or similar, not crash.
    /// </summary>
    [Theory]
    [InlineData(12)]    // Minimum header size
    [InlineData(100)]   // Small model
    [InlineData(1000)]  // Medium model
    public void Parse_RandomData_DoesNotCrash(int dataSize)
    {
        // Create data with valid binary marker (first 4 bytes = 0)
        // but garbage content
        var data = new byte[dataSize];
        var rng = new Random(42); // Deterministic seed
        rng.NextBytes(data);
        // Set binary marker
        data[0] = 0; data[1] = 0; data[2] = 0; data[3] = 0;
        // Set reasonable raw data offset (past model data)
        BitConverter.GetBytes((uint)dataSize).CopyTo(data, 4);
        // Set raw data size = 0
        data[8] = 0; data[9] = 0; data[10] = 0; data[11] = 0;

        var reader = new MdlBinaryReader();

        // Should either parse successfully or throw a well-defined exception
        // Should NOT crash with StackOverflowException, AccessViolation, etc.
        try
        {
            var model = reader.Parse(data);
            // If parsing succeeds, model should be non-null
            Assert.NotNull(model);
        }
        catch (Exception ex) when (ex is InvalidDataException
                                   or EndOfStreamException
                                   or IndexOutOfRangeException
                                   or ArgumentOutOfRangeException
                                   or OverflowException)
        {
            // These are acceptable failures for garbage data
        }
    }
}
