using System.Numerics;
using Radoub.Formats.Common;
using Radoub.Formats.Mdl;
using Radoub.Formats.Resolver;
using Radoub.Formats.Services;
using Xunit;

namespace Radoub.Formats.Tests.Mdl;

/// <summary>
/// Tests for the fresh MdlBinaryReader2 implementation.
/// Compares output with expected values to validate parsing correctness.
/// </summary>
public class MdlBinaryReader2Tests
{
    private readonly ITestOutputHelper _output;
    private readonly IGameDataService? _gameDataService;

    // Default game path - adjust if your NWN is installed elsewhere
    private static readonly string DefaultGamePath = @"D:\SteamLibrary\steamapps\common\Neverwinter Nights";

    public MdlBinaryReader2Tests(ITestOutputHelper output)
    {
        _output = output;

        // Try to initialize game data service for resource loading
        try
        {
            if (Directory.Exists(DefaultGamePath))
            {
                var config = GameResourceConfig.ForNwnEE(DefaultGamePath);
                var service = new GameDataService(config);
                if (service.IsConfigured)
                    _gameDataService = service;
            }
        }
        catch
        {
            // Game data not available - tests requiring it will skip
        }
    }

    [Fact]
    public void ParsesHeadModelStructure()
    {
        // Skip if game data not available
        if (_gameDataService == null)
        {
            _output.WriteLine("Skipping: Game data not available");
            return;
        }

        // Load pmh0_head001 - a known good head model
        var mdlData = _gameDataService.FindResource("pmh0_head001", ResourceTypes.Mdl);
        Assert.NotNull(mdlData);
        Assert.True(mdlData.Length > 0, "MDL data should not be empty");

        _output.WriteLine($"Loaded pmh0_head001.mdl: {mdlData.Length} bytes");

        // Parse with new reader
        var reader = new MdlBinaryReader2();
        var model = reader.Parse(mdlData);

        Assert.NotNull(model);
        Assert.Equal("pmh0_head001", model.Name);

        _output.WriteLine($"Model name: {model.Name}");
        _output.WriteLine($"Root node: {model.GeometryRoot?.Name}");
        _output.WriteLine($"Bounds: {model.BoundingMin} to {model.BoundingMax}");

        // The head model should have geometry
        var meshes = model.GetMeshNodes().ToList();
        Assert.NotEmpty(meshes);

        _output.WriteLine($"Found {meshes.Count} mesh(es)");

        foreach (var mesh in meshes)
        {
            _output.WriteLine($"  Mesh '{mesh.Name}':");
            _output.WriteLine($"    Texture: '{mesh.Bitmap}'");
            _output.WriteLine($"    Vertices: {mesh.Vertices?.Length ?? 0}");
            _output.WriteLine($"    Normals: {mesh.Normals?.Length ?? 0}");
            _output.WriteLine($"    Faces: {mesh.Faces?.Length ?? 0}");
            _output.WriteLine($"    UV sets: {mesh.TextureCoords?.Length ?? 0}");

            if (mesh.Vertices?.Length > 0)
            {
                _output.WriteLine($"    First vertex: {mesh.Vertices[0]}");
                _output.WriteLine($"    Last vertex: {mesh.Vertices[^1]}");

                // Head vertices should be small (within ~0.2 meters of origin)
                float maxCoord = 0;
                foreach (var v in mesh.Vertices)
                {
                    maxCoord = Math.Max(maxCoord, Math.Max(Math.Abs(v.X), Math.Max(Math.Abs(v.Y), Math.Abs(v.Z))));
                }
                _output.WriteLine($"    Max coordinate magnitude: {maxCoord}");

                // Head should be small - less than 0.5m in any direction
                Assert.True(maxCoord < 0.5f, $"Head vertices should be small, but max={maxCoord}");
            }

            if (mesh.TextureCoords?.Length > 0 && mesh.TextureCoords[0]?.Length > 0)
            {
                var uvs = mesh.TextureCoords[0];
                _output.WriteLine($"    First UV: {uvs[0]}");

                // UVs should be in 0-1 range (or slightly outside for tiling)
                int outOfRange = uvs.Count(uv => uv.X < -0.5f || uv.X > 1.5f || uv.Y < -0.5f || uv.Y > 1.5f);
                _output.WriteLine($"    UVs out of range: {outOfRange}/{uvs.Length}");
            }

            if (mesh.Faces?.Length > 0)
            {
                var f = mesh.Faces[0];
                _output.WriteLine($"    First face: [{f.VertexIndex0}, {f.VertexIndex1}, {f.VertexIndex2}]");

                // Face indices should be valid
                foreach (var face in mesh.Faces)
                {
                    Assert.True(face.VertexIndex0 < mesh.Vertices!.Length, "Face index 0 out of range");
                    Assert.True(face.VertexIndex1 < mesh.Vertices!.Length, "Face index 1 out of range");
                    Assert.True(face.VertexIndex2 < mesh.Vertices!.Length, "Face index 2 out of range");
                }
            }
        }
    }

    [Fact]
    public void ComparesOldAndNewReaders()
    {
        if (_gameDataService == null)
        {
            _output.WriteLine("Skipping: Game data not available");
            return;
        }

        var mdlData = _gameDataService.FindResource("pmh0_head001", ResourceTypes.Mdl);
        if (mdlData == null)
        {
            _output.WriteLine("Skipping: pmh0_head001 not found");
            return;
        }

        // Parse with both readers
        var oldReader = new MdlBinaryReader();
        var newReader = new MdlBinaryReader2();

        var oldModel = oldReader.Parse(mdlData);
        var newModel = newReader.Parse(mdlData);

        var oldMeshes = oldModel.GetMeshNodes().ToList();
        var newMeshes = newModel.GetMeshNodes().ToList();

        _output.WriteLine($"Old reader: {oldMeshes.Count} meshes");
        _output.WriteLine($"New reader: {newMeshes.Count} meshes");

        // Compare mesh data
        for (int i = 0; i < Math.Min(oldMeshes.Count, newMeshes.Count); i++)
        {
            var oldMesh = oldMeshes[i];
            var newMesh = newMeshes[i];

            _output.WriteLine($"\nMesh {i}:");
            _output.WriteLine($"  Old: '{oldMesh.Name}' verts={oldMesh.Vertices?.Length} faces={oldMesh.Faces?.Length}");
            _output.WriteLine($"  New: '{newMesh.Name}' verts={newMesh.Vertices?.Length} faces={newMesh.Faces?.Length}");

            if (oldMesh.Vertices?.Length > 0 && newMesh.Vertices?.Length > 0)
            {
                _output.WriteLine($"  Old first vertex: {oldMesh.Vertices[0]}");
                _output.WriteLine($"  New first vertex: {newMesh.Vertices[0]}");

                // Check if vertices match
                bool verticesMatch = oldMesh.Vertices.Length == newMesh.Vertices.Length;
                if (verticesMatch)
                {
                    for (int v = 0; v < oldMesh.Vertices.Length; v++)
                    {
                        var diff = Vector3.Distance(oldMesh.Vertices[v], newMesh.Vertices[v]);
                        if (diff > 0.001f)
                        {
                            verticesMatch = false;
                            _output.WriteLine($"  Vertex {v} differs: old={oldMesh.Vertices[v]} new={newMesh.Vertices[v]}");
                            break;
                        }
                    }
                }
                _output.WriteLine($"  Vertices match: {verticesMatch}");
            }
        }
    }

    [Fact]
    public void DumpsRawDataForAnalysis()
    {
        if (_gameDataService == null)
        {
            _output.WriteLine("Skipping: Game data not available");
            return;
        }

        var mdlData = _gameDataService.FindResource("pmh0_head001", ResourceTypes.Mdl);
        if (mdlData == null)
        {
            _output.WriteLine("Skipping: pmh0_head001 not found");
            return;
        }

        _output.WriteLine($"MDL file size: {mdlData.Length} bytes");

        // Dump file header
        uint zero = BitConverter.ToUInt32(mdlData, 0);
        uint rawDataOffset = BitConverter.ToUInt32(mdlData, 4);
        uint rawDataSize = BitConverter.ToUInt32(mdlData, 8);

        _output.WriteLine($"File header:");
        _output.WriteLine($"  [0x00] Zero: {zero}");
        _output.WriteLine($"  [0x04] Raw data offset: {rawDataOffset}");
        _output.WriteLine($"  [0x08] Raw data size: {rawDataSize}");

        // Model data starts at offset 12
        int modelDataSize = (int)rawDataOffset - 12;
        _output.WriteLine($"  Model data size: {modelDataSize}");

        // Dump first 64 bytes of raw data section
        if (rawDataOffset < mdlData.Length && rawDataOffset + 64 <= mdlData.Length)
        {
            _output.WriteLine($"\nFirst 64 bytes of raw data section (at offset {rawDataOffset}):");
            for (int i = 0; i < 64; i += 16)
            {
                var hex = BitConverter.ToString(mdlData, (int)rawDataOffset + i, 16);
                _output.WriteLine($"  {i:X4}: {hex}");
            }

            // Interpret as floats
            _output.WriteLine("\nFirst 8 floats of raw data:");
            for (int i = 0; i < 8; i++)
            {
                float f = BitConverter.ToSingle(mdlData, (int)rawDataOffset + i * 4);
                _output.WriteLine($"  [{i}] = {f:F6}");
            }
        }
    }

    [Fact]
    public void ComparesUVCoordinates()
    {
        if (_gameDataService == null)
        {
            _output.WriteLine("Skipping: Game data not available");
            return;
        }

        var mdlData = _gameDataService.FindResource("pmh0_head001", ResourceTypes.Mdl);
        if (mdlData == null)
        {
            _output.WriteLine("Skipping: pmh0_head001 not found");
            return;
        }

        // Parse with old reader
        var oldReader = new MdlBinaryReader();
        var oldModel = oldReader.Parse(mdlData);
        var oldMeshes = oldModel.GetMeshNodes().ToList();

        _output.WriteLine("=== OLD READER UV ANALYSIS ===");
        foreach (var mesh in oldMeshes)
        {
            _output.WriteLine($"Mesh '{mesh.Name}':");
            _output.WriteLine($"  Vertices: {mesh.Vertices?.Length ?? 0}");
            _output.WriteLine($"  UV sets: {mesh.TextureCoords?.Length ?? 0}");

            if (mesh.TextureCoords?.Length > 0 && mesh.TextureCoords[0] != null)
            {
                var uvs = mesh.TextureCoords[0];
                _output.WriteLine($"  UV0 count: {uvs.Length}");

                // Show first 5 UVs
                for (int i = 0; i < Math.Min(5, uvs.Length); i++)
                {
                    _output.WriteLine($"    UV[{i}] = ({uvs[i].X:F6}, {uvs[i].Y:F6})");
                }

                // Stats
                float minU = uvs.Min(uv => uv.X);
                float maxU = uvs.Max(uv => uv.X);
                float minV = uvs.Min(uv => uv.Y);
                float maxV = uvs.Max(uv => uv.Y);
                _output.WriteLine($"  UV range: U=[{minU:F3}, {maxU:F3}] V=[{minV:F3}, {maxV:F3}]");

                // Count out of normal range
                int outOfRange = uvs.Count(uv => uv.X < 0 || uv.X > 1 || uv.Y < 0 || uv.Y > 1);
                _output.WriteLine($"  Out of [0,1] range: {outOfRange}/{uvs.Length}");
            }

            // Also check vertices for sanity
            if (mesh.Vertices?.Length > 0)
            {
                float minX = mesh.Vertices.Min(v => v.X);
                float maxX = mesh.Vertices.Max(v => v.X);
                float minY = mesh.Vertices.Min(v => v.Y);
                float maxY = mesh.Vertices.Max(v => v.Y);
                float minZ = mesh.Vertices.Min(v => v.Z);
                float maxZ = mesh.Vertices.Max(v => v.Z);
                _output.WriteLine($"  Vertex range: X=[{minX:F3}, {maxX:F3}] Y=[{minY:F3}, {maxY:F3}] Z=[{minZ:F3}, {maxZ:F3}]");
            }

            // Show some vertex/UV pairings with face info
            if (mesh.Vertices?.Length > 0 && mesh.TextureCoords?.Length > 0 && mesh.Faces?.Length > 0)
            {
                _output.WriteLine($"\n  Sample vertex/UV pairings for first 3 faces:");
                for (int fi = 0; fi < Math.Min(3, mesh.Faces.Length); fi++)
                {
                    var face = mesh.Faces[fi];
                    _output.WriteLine($"    Face {fi}: indices=[{face.VertexIndex0}, {face.VertexIndex1}, {face.VertexIndex2}]");

                    int[] indices = { face.VertexIndex0, face.VertexIndex1, face.VertexIndex2 };
                    foreach (var idx in indices)
                    {
                        var v = mesh.Vertices[idx];
                        var uv = mesh.TextureCoords[0][idx];
                        _output.WriteLine($"      [{idx}]: V=({v.X:F3},{v.Y:F3},{v.Z:F3}) UV=({uv.X:F4},{uv.Y:F4})");
                    }
                }
            }
        }
    }

    [Fact]
    public void DumpsUVOffsetsForAnalysis()
    {
        if (_gameDataService == null)
        {
            _output.WriteLine("Skipping: Game data not available");
            return;
        }

        var mdlData = _gameDataService.FindResource("pmh0_head001", ResourceTypes.Mdl);
        if (mdlData == null)
        {
            _output.WriteLine("Skipping: pmh0_head001 not found");
            return;
        }

        // Parse file structure manually
        uint rawDataOffset = BitConverter.ToUInt32(mdlData, 4);
        uint rawDataSize = BitConverter.ToUInt32(mdlData, 8);

        _output.WriteLine($"MDL file: {mdlData.Length} bytes");
        _output.WriteLine($"Raw data: offset={rawDataOffset}, size={rawDataSize}");

        // We know from the log that:
        // - Vertex data is at raw offset 0 (after 12-byte avg normal skip, actual verts at 12)
        // - UV data is at raw offset 2808
        // - Normals are at raw offset 4680
        // Let's verify by examining the raw data layout

        int vertexCount = 234;  // Known from parser output
        int uvOffset = 2808;    // Known from parser output
        int normalsOffset = 4680; // Known from parser output

        _output.WriteLine($"\nExpected layout:");
        _output.WriteLine($"  [0]: Average normal header (12 bytes)");
        _output.WriteLine($"  [12]: Vertices ({vertexCount} * 12 = {vertexCount * 12} bytes, ends at {12 + vertexCount * 12})");
        _output.WriteLine($"  [2808]: UVs ({vertexCount} * 8 = {vertexCount * 8} bytes, ends at {uvOffset + vertexCount * 8})");
        _output.WriteLine($"  [4680]: Normals ({vertexCount} * 12 = {vertexCount * 12} bytes, ends at {normalsOffset + vertexCount * 12})");

        // Dump data at UV offset to see what's there
        _output.WriteLine($"\nData at UV offset {uvOffset}:");
        for (int i = 0; i < 5; i++)
        {
            float u = BitConverter.ToSingle(mdlData, (int)rawDataOffset + uvOffset + i * 8);
            float v = BitConverter.ToSingle(mdlData, (int)rawDataOffset + uvOffset + i * 8 + 4);
            _output.WriteLine($"  UV[{i}] = ({u:F6}, {v:F6})");
        }

        // Also check what's right before UV offset (in case there's a header)
        _output.WriteLine($"\nData just BEFORE UV offset (checking for header):");
        for (int i = -3; i < 0; i++)
        {
            int offset = uvOffset + i * 8;
            if (offset >= 0)
            {
                float a = BitConverter.ToSingle(mdlData, (int)rawDataOffset + offset);
                float b = BitConverter.ToSingle(mdlData, (int)rawDataOffset + offset + 4);
                _output.WriteLine($"  [{offset}]: ({a:F6}, {b:F6})");
            }
        }

        // Check data at normals offset
        _output.WriteLine($"\nData at normals offset {normalsOffset}:");
        for (int i = 0; i < 3; i++)
        {
            float x = BitConverter.ToSingle(mdlData, (int)rawDataOffset + normalsOffset + i * 12);
            float y = BitConverter.ToSingle(mdlData, (int)rawDataOffset + normalsOffset + i * 12 + 4);
            float z = BitConverter.ToSingle(mdlData, (int)rawDataOffset + normalsOffset + i * 12 + 8);
            var mag = Math.Sqrt(x * x + y * y + z * z);
            _output.WriteLine($"  Normal[{i}] = ({x:F6}, {y:F6}, {z:F6}) mag={mag:F4}");
        }

        // Data just before normals
        _output.WriteLine($"\nData just BEFORE normals offset:");
        for (int offset = normalsOffset - 24; offset < normalsOffset; offset += 12)
        {
            float x = BitConverter.ToSingle(mdlData, (int)rawDataOffset + offset);
            float y = BitConverter.ToSingle(mdlData, (int)rawDataOffset + offset + 4);
            float z = BitConverter.ToSingle(mdlData, (int)rawDataOffset + offset + 8);
            var mag = Math.Sqrt(x * x + y * y + z * z);
            _output.WriteLine($"  [{offset}]: ({x:F6}, {y:F6}, {z:F6}) mag={mag:F4}");
        }
    }

    [Fact]
    public void AnalyzesPelvisModel()
    {
        if (_gameDataService == null)
        {
            _output.WriteLine("Skipping: Game data not available");
            return;
        }

        // Load pelvis model - the problematic one
        var mdlData = _gameDataService.FindResource("pmh0_pelvis001", ResourceTypes.Mdl);
        if (mdlData == null)
        {
            _output.WriteLine("Skipping: pmh0_pelvis001 not found");
            return;
        }

        _output.WriteLine($"Loaded pmh0_pelvis001.mdl: {mdlData.Length} bytes");

        var oldReader = new MdlBinaryReader();
        var model = oldReader.Parse(mdlData);

        var meshes = model.GetMeshNodes().ToList();
        _output.WriteLine($"Meshes: {meshes.Count}");

        foreach (var mesh in meshes)
        {
            _output.WriteLine($"\nMesh '{mesh.Name}':");
            _output.WriteLine($"  Texture: '{mesh.Bitmap}'");
            _output.WriteLine($"  Vertices: {mesh.Vertices?.Length ?? 0}");

            if (mesh.TextureCoords?.Length > 0 && mesh.TextureCoords[0] != null)
            {
                var uvs = mesh.TextureCoords[0];
                _output.WriteLine($"  UV count: {uvs.Length}");

                // Show first 10 UVs
                for (int i = 0; i < Math.Min(10, uvs.Length); i++)
                {
                    _output.WriteLine($"    UV[{i}] = ({uvs[i].X:F4}, {uvs[i].Y:F4})");
                }

                float minU = uvs.Min(uv => uv.X);
                float maxU = uvs.Max(uv => uv.X);
                float minV = uvs.Min(uv => uv.Y);
                float maxV = uvs.Max(uv => uv.Y);
                _output.WriteLine($"  UV range: U=[{minU:F3}, {maxU:F3}] V=[{minV:F3}, {maxV:F3}]");

                // Count negative UVs
                int negativeU = uvs.Count(uv => uv.X < 0);
                int negativeV = uvs.Count(uv => uv.Y < 0);
                _output.WriteLine($"  Negative UVs: U<0: {negativeU}, V<0: {negativeV}");
            }
        }
    }

    [Fact]
    public void AnalyzesPelvisPltLayers()
    {
        if (_gameDataService == null)
        {
            _output.WriteLine("Skipping: Game data not available");
            return;
        }

        // Load pelvis PLT texture
        var pltData = _gameDataService.FindResource("pmh0_pelvis001", ResourceTypes.Plt);
        if (pltData == null)
        {
            _output.WriteLine("Skipping: pmh0_pelvis001.plt not found");
            return;
        }

        _output.WriteLine($"Loaded pmh0_pelvis001.plt: {pltData.Length} bytes");

        // Parse PLT header
        string sig = System.Text.Encoding.ASCII.GetString(pltData, 0, 4);
        uint width = BitConverter.ToUInt32(pltData, 16);
        uint height = BitConverter.ToUInt32(pltData, 20);

        _output.WriteLine($"PLT: {width}x{height}, sig='{sig}'");

        // Count layer usage
        var layerCounts = new int[256];
        int offset = 24;
        for (int i = 0; i < width * height; i++)
        {
            byte grayscale = pltData[offset++];
            byte layerId = pltData[offset++];
            layerCounts[layerId]++;
        }

        _output.WriteLine("\nLayer usage:");
        for (int i = 0; i < 16; i++)
        {
            if (layerCounts[i] > 0)
            {
                string layerName = i switch
                {
                    0 => "Skin",
                    1 => "Hair",
                    2 => "Metal1",
                    3 => "Metal2",
                    4 => "Cloth1",
                    5 => "Cloth2",
                    6 => "Leather1",
                    7 => "Leather2",
                    8 => "Tattoo1",
                    9 => "Tattoo2",
                    _ => $"Unknown({i})"
                };
                _output.WriteLine($"  Layer {i} ({layerName}): {layerCounts[i]} pixels ({100.0 * layerCounts[i] / (width * height):F1}%)");
            }
        }
    }

    [Fact]
    public void AnalyzesBodyModel()
    {
        if (_gameDataService == null)
        {
            _output.WriteLine("Skipping: Game data not available");
            return;
        }

        // Load a body part model (torso)
        var mdlData = _gameDataService.FindResource("pmh0_chest001", ResourceTypes.Mdl);
        if (mdlData == null)
        {
            _output.WriteLine("Skipping: pmh0_chest001 not found");
            return;
        }

        _output.WriteLine($"Loaded pmh0_chest001.mdl: {mdlData.Length} bytes");

        var oldReader = new MdlBinaryReader();
        var model = oldReader.Parse(mdlData);

        _output.WriteLine($"Model: {model.Name}");
        _output.WriteLine($"Bounds: {model.BoundingMin} to {model.BoundingMax}");

        var meshes = model.GetMeshNodes().ToList();
        _output.WriteLine($"Meshes: {meshes.Count}");

        foreach (var mesh in meshes)
        {
            _output.WriteLine($"\nMesh '{mesh.Name}':");
            _output.WriteLine($"  Texture: '{mesh.Bitmap}'");
            _output.WriteLine($"  Vertices: {mesh.Vertices?.Length ?? 0}");
            _output.WriteLine($"  Faces: {mesh.Faces?.Length ?? 0}");

            if (mesh.Vertices?.Length > 0)
            {
                // Show first few vertices
                for (int i = 0; i < Math.Min(3, mesh.Vertices.Length); i++)
                {
                    _output.WriteLine($"    V[{i}] = {mesh.Vertices[i]}");
                }

                // Range check
                float minX = mesh.Vertices.Min(v => v.X);
                float maxX = mesh.Vertices.Max(v => v.X);
                float minY = mesh.Vertices.Min(v => v.Y);
                float maxY = mesh.Vertices.Max(v => v.Y);
                float minZ = mesh.Vertices.Min(v => v.Z);
                float maxZ = mesh.Vertices.Max(v => v.Z);
                _output.WriteLine($"  Vertex range: X=[{minX:F3}, {maxX:F3}] Y=[{minY:F3}, {maxY:F3}] Z=[{minZ:F3}, {maxZ:F3}]");
            }

            if (mesh.TextureCoords?.Length > 0 && mesh.TextureCoords[0] != null)
            {
                var uvs = mesh.TextureCoords[0];
                float minU = uvs.Min(uv => uv.X);
                float maxU = uvs.Max(uv => uv.X);
                float minV = uvs.Min(uv => uv.Y);
                float maxV = uvs.Max(uv => uv.Y);
                _output.WriteLine($"  UV range: U=[{minU:F3}, {maxU:F3}] V=[{minV:F3}, {maxV:F3}]");
            }

            if (mesh.Faces?.Length > 0)
            {
                // Check face index validity
                int maxIdx = 0;
                foreach (var f in mesh.Faces)
                {
                    maxIdx = Math.Max(maxIdx, Math.Max(f.VertexIndex0, Math.Max(f.VertexIndex1, f.VertexIndex2)));
                }
                _output.WriteLine($"  Max face index: {maxIdx} (vertex count: {mesh.Vertices?.Length ?? 0})");

                if (mesh.Vertices != null && maxIdx >= mesh.Vertices.Length)
                {
                    _output.WriteLine($"  *** FACE INDICES OUT OF BOUNDS! ***");
                }
            }
        }
    }
}
