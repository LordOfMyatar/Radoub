using System.Numerics;
using Radoub.Formats.Common;
using Radoub.Formats.Mdl;
using Radoub.Formats.Resolver;
using Radoub.Formats.Services;
using Xunit;

namespace Radoub.Formats.Tests.Mdl;

/// <summary>
/// Tests for standalone (MODELTYPE=S) creature models like bat and giant.
/// Diagnostic tests to identify rendering issues (#1754).
/// </summary>
public class StandaloneModelTests
{
    private readonly ITestOutputHelper _output;
    private readonly IGameDataService? _gameDataService;

    private static readonly string DefaultGamePath = @"D:\SteamLibrary\steamapps\common\Neverwinter Nights";

    public StandaloneModelTests(ITestOutputHelper output)
    {
        _output = output;

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
            // Game data not available
        }
    }

    [Theory]
    [InlineData("c_a_bat")]        // Bat - MODELTYPE=S
    [InlineData("c_bathorror")]    // Bat Horror - MODELTYPE=F
    [InlineData("c_gnthill")]      // Giant Hill - MODELTYPE=L
    [InlineData("c_gntmount")]     // Giant Mountain - MODELTYPE=L
    [InlineData("c_gntfire")]      // Giant Fire - MODELTYPE=L
    [InlineData("c_gntfrost")]     // Giant Frost - MODELTYPE=L
    [InlineData("c_allip")]        // Allip - MODELTYPE=S (known working reference)
    public void DiagnoseStandaloneModel(string modelName)
    {
        if (_gameDataService == null)
        {
            _output.WriteLine("Skipping: Game data not available");
            return;
        }

        var mdlData = _gameDataService.FindResource(modelName, ResourceTypes.Mdl);
        if (mdlData == null)
        {
            _output.WriteLine($"Skipping: {modelName} not found in game data");
            return;
        }

        _output.WriteLine($"=== Model: {modelName} ({mdlData.Length} bytes) ===");

        var reader = new MdlBinaryReader();
        var model = reader.Parse(mdlData);

        Assert.NotNull(model);
        _output.WriteLine($"Classification: {model.Classification}");
        _output.WriteLine($"BoundingMin: {model.BoundingMin}");
        _output.WriteLine($"BoundingMax: {model.BoundingMax}");

        var meshes = model.GetMeshNodes().ToList();
        _output.WriteLine($"Total mesh nodes: {meshes.Count}");

        int nonRenderCount = 0;
        int nanVertexCount = 0;
        int emptyMeshCount = 0;

        foreach (var mesh in meshes)
        {
            bool isSkin = mesh is MdlSkinNode;
            string meshType = isSkin ? "SKIN" : "TRIMESH";

            _output.WriteLine($"\n  Mesh '{mesh.Name}' [{meshType}]:");
            _output.WriteLine($"    Render: {mesh.Render}");
            _output.WriteLine($"    Vertices: {mesh.Vertices.Length}");
            _output.WriteLine($"    Faces: {mesh.Faces.Length}");
            _output.WriteLine($"    Normals: {mesh.Normals.Length}");
            _output.WriteLine($"    Bitmap: '{mesh.Bitmap}'");
            _output.WriteLine($"    Shadow: {mesh.Shadow}, Beaming: {mesh.Beaming}");
            _output.WriteLine($"    TransparencyHint: {mesh.TransparencyHint}");

            if (!mesh.Render)
            {
                nonRenderCount++;
                _output.WriteLine($"    *** RENDER=FALSE — would be invisible in nwnexplorer ***");
            }

            if (mesh.Vertices.Length == 0 || mesh.Faces.Length == 0)
            {
                emptyMeshCount++;
                _output.WriteLine($"    *** EMPTY MESH — skipped by renderer ***");
                continue;
            }

            // Check for NaN vertices
            int meshNanCount = 0;
            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                var v = mesh.Vertices[i];
                if (float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Z))
                    meshNanCount++;
            }
            if (meshNanCount > 0)
            {
                nanVertexCount += meshNanCount;
                _output.WriteLine($"    *** {meshNanCount}/{mesh.Vertices.Length} NaN vertices ***");
            }

            // Report vertex bounds
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            foreach (var v in mesh.Vertices)
            {
                if (float.IsNaN(v.X)) continue;
                minX = Math.Min(minX, v.X); maxX = Math.Max(maxX, v.X);
                minY = Math.Min(minY, v.Y); maxY = Math.Max(maxY, v.Y);
                minZ = Math.Min(minZ, v.Z); maxZ = Math.Max(maxZ, v.Z);
            }
            if (minX != float.MaxValue)
            {
                _output.WriteLine($"    Vertex bounds: ({minX:F3},{minY:F3},{minZ:F3}) to ({maxX:F3},{maxY:F3},{maxZ:F3})");
                float sizeX = maxX - minX, sizeY = maxY - minY, sizeZ = maxZ - minZ;
                _output.WriteLine($"    Size: {sizeX:F3} x {sizeY:F3} x {sizeZ:F3}");
            }

            // Report node hierarchy position/orientation
            _output.WriteLine($"    Position: {mesh.Position}");
            _output.WriteLine($"    Orientation: {mesh.Orientation}");

            // Check UV data
            if (mesh.TextureCoords.Length == 0)
                _output.WriteLine($"    *** NO UV COORDINATES ***");
            else if (mesh.TextureCoords[0].Length != mesh.Vertices.Length)
                _output.WriteLine($"    *** UV COUNT MISMATCH: {mesh.TextureCoords[0].Length} UVs vs {mesh.Vertices.Length} verts ***");

            // For skin meshes, report bone data
            if (mesh is MdlSkinNode skin)
            {
                _output.WriteLine($"    BoneWeights: {skin.BoneWeights.Length}");
                _output.WriteLine($"    BoneQuaternions: {skin.BoneQuaternions.Length}");
                _output.WriteLine($"    BoneTranslations: {skin.BoneTranslations.Length}");
                _output.WriteLine($"    NodeToBoneMap: {skin.NodeToBoneMap.Length}");
            }
        }

        _output.WriteLine($"\n=== Summary for {modelName} ===");
        _output.WriteLine($"  Total meshes: {meshes.Count}");
        _output.WriteLine($"  Non-render (Render=false): {nonRenderCount}");
        _output.WriteLine($"  Empty meshes: {emptyMeshCount}");
        _output.WriteLine($"  Total NaN vertices: {nanVertexCount}");

        // The model should have at least some renderable meshes
        int renderableMeshes = meshes.Count(m => m.Render && m.Vertices.Length > 0 && m.Faces.Length > 0);
        _output.WriteLine($"  Renderable meshes: {renderableMeshes}");
        Assert.True(renderableMeshes > 0, $"{modelName} should have at least one renderable mesh");

        // Dump full node hierarchy with transforms
        _output.WriteLine($"\n=== Node Hierarchy for {modelName} ===");
        _output.WriteLine($"  AnimationScale: {model.AnimationScale}");
        _output.WriteLine($"  SuperModel: '{model.SuperModel}'");
        if (model.GeometryRoot != null)
            DumpNodeTree(model.GeometryRoot, 0);
    }

    private void DumpNodeTree(MdlNode node, int depth)
    {
        string indent = new string(' ', depth * 4);
        string nodeType = node switch
        {
            MdlSkinNode => "SKIN",
            MdlTrimeshNode t => $"TRIMESH(R={t.Render})",
            _ => node.NodeType.ToString()
        };

        string scaleInfo = node.Scale != 1.0f ? $" SCALE={node.Scale:F4}" : "";
        string posInfo = node.Position != System.Numerics.Vector3.Zero ? $" pos=({node.Position.X:F3},{node.Position.Y:F3},{node.Position.Z:F3})" : "";
        string oriInfo = node.Orientation != System.Numerics.Quaternion.Identity ? $" ori=({node.Orientation.X:F4},{node.Orientation.Y:F4},{node.Orientation.Z:F4},{node.Orientation.W:F4})" : "";

        _output.WriteLine($"{indent}{node.Name} [{nodeType}]{scaleInfo}{posInfo}{oriInfo}");

        foreach (var child in node.Children)
            DumpNodeTree(child, depth + 1);
    }

    [Theory]
    [InlineData("c_a_bat")]
    [InlineData("c_gntfire")]
    public void RenderFlagIsRespected(string modelName)
    {
        if (_gameDataService == null)
        {
            _output.WriteLine("Skipping: Game data not available");
            return;
        }

        var mdlData = _gameDataService.FindResource(modelName, ResourceTypes.Mdl);
        if (mdlData == null)
        {
            _output.WriteLine($"Skipping: {modelName} not found");
            return;
        }

        var reader = new MdlBinaryReader();
        var model = reader.Parse(mdlData);

        var meshes = model.GetMeshNodes().ToList();
        var nonRenderMeshes = meshes.Where(m => !m.Render).ToList();

        _output.WriteLine($"{modelName}: {meshes.Count} meshes, {nonRenderMeshes.Count} with Render=false");
        foreach (var mesh in nonRenderMeshes)
        {
            _output.WriteLine($"  Non-render mesh: '{mesh.Name}' verts={mesh.Vertices.Length} faces={mesh.Faces.Length}");
        }

        // This test documents which meshes have Render=false
        // The fix should filter these out in ModelPreviewGLControl
    }

    [Fact]
    public void FindGiantModelNames()
    {
        if (_gameDataService == null)
        {
            _output.WriteLine("Skipping: Game data not available");
            return;
        }

        // Search appearance.2da for giant-related entries
        var appearance2da = _gameDataService.Get2DA("appearance");
        if (appearance2da == null)
        {
            _output.WriteLine("Skipping: appearance.2da not available");
            return;
        }

        _output.WriteLine("Searching appearance.2da for giant/bat entries...\n");

        for (int i = 0; i < appearance2da.RowCount; i++)
        {
            var label = appearance2da.GetValue(i, "LABEL");
            if (label == null) continue;

            if (label.IndexOf("giant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("bat", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var modelType = appearance2da.GetValue(i, "MODELTYPE");
                var race = appearance2da.GetValue(i, "RACE");
                _output.WriteLine($"  Row {i}: LABEL={label}, MODELTYPE={modelType}, RACE={race}");
            }
        }
    }

    [Fact]
    public void DiagnoseAllStandaloneModels()
    {
        if (_gameDataService == null)
        {
            _output.WriteLine("Skipping: Game data not available");
            return;
        }

        var appearance2da = _gameDataService.Get2DA("appearance");
        if (appearance2da == null)
        {
            _output.WriteLine("Skipping: appearance.2da not available");
            return;
        }

        // Collect all standalone (non-P) model names
        var standaloneModels = new Dictionary<string, string>(); // race -> label
        for (int i = 0; i < appearance2da.RowCount; i++)
        {
            var label = appearance2da.GetValue(i, "LABEL");
            var modelType = appearance2da.GetValue(i, "MODELTYPE");
            var race = appearance2da.GetValue(i, "RACE");

            if (label == null || race == null || race == "****") continue;
            if (modelType?.ToUpperInvariant().Contains("P") == true) continue;

            if (!standaloneModels.ContainsKey(race.ToLowerInvariant()))
                standaloneModels[race.ToLowerInvariant()] = label;
        }

        _output.WriteLine($"Found {standaloneModels.Count} unique standalone model names\n");

        int loaded = 0, failed = 0, hasNonRender = 0, hasNan = 0;
        var reader = new MdlBinaryReader();

        foreach (var (modelName, label) in standaloneModels.OrderBy(kv => kv.Key))
        {
            var mdlData = _gameDataService.FindResource(modelName, ResourceTypes.Mdl);
            if (mdlData == null)
            {
                _output.WriteLine($"  MISSING: {modelName} ({label})");
                continue;
            }

            try
            {
                var model = reader.Parse(mdlData);
                var meshes = model.GetMeshNodes().ToList();
                int nonRender = meshes.Count(m => !m.Render);
                int nanVerts = meshes.Sum(m => m.Vertices.Count(v =>
                    float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Z)));
                int emptyMeshes = meshes.Count(m => m.Vertices.Length == 0 || m.Faces.Length == 0);

                loaded++;
                if (nonRender > 0) hasNonRender++;
                if (nanVerts > 0) hasNan++;

                string issues = "";
                if (nonRender > 0) issues += $" RENDER=false:{nonRender}";
                if (nanVerts > 0) issues += $" NaN:{nanVerts}";
                if (emptyMeshes > 0) issues += $" empty:{emptyMeshes}";

                if (issues.Length > 0)
                    _output.WriteLine($"  ISSUES: {modelName} ({label}) meshes={meshes.Count}{issues}");
                else
                    _output.WriteLine($"  OK: {modelName} ({label}) meshes={meshes.Count}");
            }
            catch (Exception ex)
            {
                failed++;
                _output.WriteLine($"  PARSE FAIL: {modelName} ({label}) — {ex.GetType().Name}: {ex.Message}");
            }
        }

        _output.WriteLine($"\n=== Summary ===");
        _output.WriteLine($"  Loaded: {loaded}, Failed: {failed}");
        _output.WriteLine($"  Has Render=false meshes: {hasNonRender}");
        _output.WriteLine($"  Has NaN vertices: {hasNan}");
    }
}
