using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Xunit.Abstractions;

namespace Quartermaster.Tests;

/// <summary>
/// Diagnostic tests to analyze appearance.2da MODELTYPE categories.
/// Run these to understand the scope of static vs dynamic models.
/// </summary>
public class AppearanceAnalysisTests
{
    private readonly ITestOutputHelper _output;

    public AppearanceAnalysisTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Analyze all appearances in appearance.2da and categorize by MODELTYPE.
    /// This helps scope out static model fixes for #1174.
    /// </summary>
    [Fact]
    public void AnalyzeAppearanceModelTypes()
    {
        // Skip if no game configured
        if (!RadoubSettings.Instance.HasGamePaths)
        {
            _output.WriteLine("SKIP: No game paths configured in RadoubSettings");
            return;
        }

        using var gameData = new GameDataService();
        if (!gameData.IsConfigured)
        {
            _output.WriteLine("SKIP: GameDataService not configured");
            return;
        }

        var twoDA = gameData.Get2DA("appearance");
        if (twoDA == null)
        {
            _output.WriteLine("SKIP: Could not load appearance.2da");
            return;
        }

        // Group appearances by MODELTYPE
        var byModelType = new Dictionary<string, List<(int id, string label, string race)>>();
        var partBased = new List<(int id, string label, string race)>();

        for (int i = 0; i < twoDA.RowCount; i++)
        {
            var label = twoDA.GetValue(i, "LABEL");
            if (string.IsNullOrEmpty(label) || label == "****")
                continue;

            var modelType = twoDA.GetValue(i, "MODELTYPE") ?? "(null)";
            var race = twoDA.GetValue(i, "RACE") ?? "(null)";

            if (modelType.ToUpperInvariant() == "P")
            {
                partBased.Add((i, label, race));
            }
            else
            {
                if (!byModelType.TryGetValue(modelType, out var list))
                {
                    list = new List<(int id, string label, string race)>();
                    byModelType[modelType] = list;
                }
                list.Add((i, label, race));
            }
        }

        // Output results
        _output.WriteLine($"=== APPEARANCE.2DA ANALYSIS ===");
        _output.WriteLine($"Total rows: {twoDA.RowCount}");
        _output.WriteLine($"Part-based (MODELTYPE=P): {partBased.Count}");
        _output.WriteLine($"Static model types: {byModelType.Count}");
        _output.WriteLine("");

        // Part-based summary
        _output.WriteLine($"--- PART-BASED MODELS (MODELTYPE=P) ---");
        _output.WriteLine($"Count: {partBased.Count}");
        var raceGroups = partBased.GroupBy(x => x.race).OrderBy(g => g.Key);
        foreach (var rg in raceGroups)
        {
            _output.WriteLine($"  Race '{rg.Key}': {rg.Count()} appearances");
            foreach (var app in rg.Take(5))
            {
                _output.WriteLine($"    [{app.id}] {app.label}");
            }
            if (rg.Count() > 5)
                _output.WriteLine($"    ... and {rg.Count() - 5} more");
        }
        _output.WriteLine("");

        // Static model types
        _output.WriteLine($"--- STATIC MODEL TYPES ---");
        foreach (var kvp in byModelType.OrderBy(x => x.Key))
        {
            _output.WriteLine($"MODELTYPE='{kvp.Key}': {kvp.Value.Count} appearances");

            // Categorize by model prefix
            var prefixes = kvp.Value
                .Select(x => GetModelPrefix(x.race))
                .GroupBy(p => p)
                .OrderByDescending(g => g.Count())
                .Take(10);

            foreach (var prefix in prefixes)
            {
                _output.WriteLine($"  Prefix '{prefix.Key}': {prefix.Count()}");
            }

            // Sample entries
            _output.WriteLine($"  Sample entries:");
            foreach (var app in kvp.Value.Take(10))
            {
                _output.WriteLine($"    [{app.id}] {app.label} -> model: {app.race}");
            }
            if (kvp.Value.Count > 10)
                _output.WriteLine($"    ... and {kvp.Value.Count - 10} more");
            _output.WriteLine("");
        }
    }

    /// <summary>
    /// Extract model prefix (e.g., "c_dragon" from "c_dragonred")
    /// </summary>
    private static string GetModelPrefix(string modelName)
    {
        if (string.IsNullOrEmpty(modelName) || modelName == "****")
            return "(none)";

        // Common prefixes: c_ (creature), n_ (NPC), a_ (animal?)
        if (modelName.StartsWith("c_"))
        {
            // Try to extract base creature type
            var parts = modelName.Substring(2).Split('_');
            if (parts.Length > 0)
            {
                // Remove color suffixes (red, blue, etc.)
                var baseName = parts[0];
                foreach (var color in new[] { "red", "blue", "green", "black", "white", "gold", "silver", "bronze", "copper", "brass" })
                {
                    if (baseName.EndsWith(color, StringComparison.OrdinalIgnoreCase))
                    {
                        baseName = baseName.Substring(0, baseName.Length - color.Length);
                        break;
                    }
                }
                return $"c_{baseName}";
            }
        }

        return modelName.Split('_')[0] + "_";
    }

    /// <summary>
    /// List specific model types that are known to have issues.
    /// </summary>
    [Fact]
    public void ListProblematicModelTypes()
    {
        if (!RadoubSettings.Instance.HasGamePaths)
        {
            _output.WriteLine("SKIP: No game paths configured");
            return;
        }

        using var gameData = new GameDataService();
        if (!gameData.IsConfigured)
        {
            _output.WriteLine("SKIP: GameDataService not configured");
            return;
        }

        var twoDA = gameData.Get2DA("appearance");
        if (twoDA == null)
        {
            _output.WriteLine("SKIP: Could not load appearance.2da");
            return;
        }

        _output.WriteLine("=== PROBLEMATIC MODEL CATEGORIES ===");
        _output.WriteLine("");

        // Known issues from user report:
        // 1. Dragons - don't show up at all
        // 2. Static humanoids (bandits) - head in midsection
        // 3. Animals - appendages mispositioned

        var categories = new[]
        {
            ("Dragons", new[] { "dragon", "wyrm" }),
            ("Giants/Humanoids", new[] { "giant", "troll", "ogre", "goblin", "orc", "hobgoblin", "bugbear", "gnoll" }),
            ("Undead", new[] { "skeleton", "zombie", "lich", "vampire", "mummy", "wight", "ghoul", "ghost", "spectre", "wraith" }),
            ("Animals", new[] { "wolf", "bear", "cat", "dog", "bat", "rat", "spider", "beetle", "boar", "deer", "horse" }),
            ("Elementals", new[] { "elemental", "golem" }),
            ("Demons/Devils", new[] { "demon", "devil", "balor", "succubus", "imp", "vrock", "hezrou" }),
            ("Misc Monsters", new[] { "beholder", "mind", "umber", "hook", "basilisk", "cockatrice", "gargoyle", "minotaur" })
        };

        foreach (var (category, keywords) in categories)
        {
            _output.WriteLine($"--- {category} ---");
            var matches = new List<(int id, string label, string race, string modelType)>();

            for (int i = 0; i < twoDA.RowCount; i++)
            {
                var label = twoDA.GetValue(i, "LABEL")?.ToLowerInvariant() ?? "";
                var race = twoDA.GetValue(i, "RACE") ?? "";
                var modelType = twoDA.GetValue(i, "MODELTYPE") ?? "";

                // Skip part-based (already working)
                if (modelType.ToUpperInvariant() == "P")
                    continue;

                if (keywords.Any(k => label.Contains(k) || race.ToLowerInvariant().Contains(k)))
                {
                    matches.Add((i, label, race, modelType));
                }
            }

            if (matches.Count == 0)
            {
                _output.WriteLine("  (no matches)");
            }
            else
            {
                _output.WriteLine($"  Found {matches.Count} appearances:");
                foreach (var m in matches.Take(20))
                {
                    _output.WriteLine($"    [{m.id}] {m.label} | model={m.race} | type={m.modelType}");
                }
                if (matches.Count > 20)
                    _output.WriteLine($"    ... and {matches.Count - 20} more");
            }
            _output.WriteLine("");
        }
    }

    /// <summary>
    /// Analyze model node hierarchy to understand transform structure.
    /// </summary>
    [Fact]
    public void AnalyzeModelHierarchy()
    {
        if (!RadoubSettings.Instance.HasGamePaths)
        {
            _output.WriteLine("SKIP: No game paths configured");
            return;
        }

        using var gameData = new GameDataService();
        if (!gameData.IsConfigured)
        {
            _output.WriteLine("SKIP: GameDataService not configured");
            return;
        }

        var modelService = new Services.ModelService(gameData);

        // Compare static vs part-based models
        var testModels = new[]
        {
            ("c_DrgRed", "Dragon (static S type)"),
            ("c_bugbearA", "Bugbear (static F type)"),
            ("c_skel_com01", "Skeleton (static F type)"),
            ("c_a_bat", "Bat (static S type - animal)"),
            ("pmh0", "Human male skeleton (part-based)"),
        };

        foreach (var (modelName, description) in testModels)
        {
            _output.WriteLine($"\n=== {description}: {modelName} ===");
            var model = modelService.LoadModel(modelName);
            if (model == null)
            {
                _output.WriteLine("  Model not found");
                continue;
            }

            _output.WriteLine($"  Classification: {model.Classification}");
            _output.WriteLine($"  BoundingMin: {model.BoundingMin}");
            _output.WriteLine($"  BoundingMax: {model.BoundingMax}");
            _output.WriteLine($"  Radius: {model.Radius}");
            _output.WriteLine($"  SuperModel: {model.SuperModel}");
            _output.WriteLine("");

            // Print node hierarchy with transforms
            if (model.GeometryRoot != null)
            {
                PrintNodeHierarchy(model.GeometryRoot, 0);
            }
        }
    }

    private void PrintNodeHierarchy(Radoub.Formats.Mdl.MdlNode node, int depth)
    {
        var indent = new string(' ', depth * 2);
        var nodeType = node.GetType().Name.Replace("Mdl", "").Replace("Node", "");
        var hasMesh = node is Radoub.Formats.Mdl.MdlTrimeshNode trimesh && trimesh.Vertices.Length > 0;
        var meshInfo = hasMesh ? $" [{((Radoub.Formats.Mdl.MdlTrimeshNode)node).Vertices.Length} verts]" : "";

        // Check if node has non-identity transform
        var hasPosition = node.Position != System.Numerics.Vector3.Zero;
        var hasRotation = node.Orientation != System.Numerics.Quaternion.Identity;
        var hasScale = Math.Abs(node.Scale - 1.0f) > 0.001f;

        var transformInfo = "";
        if (hasPosition) transformInfo += $" pos={node.Position}";
        if (hasRotation)
        {
            // Convert quaternion to euler for readability
            var q = node.Orientation;
            var yaw = MathF.Atan2(2 * (q.W * q.Z + q.X * q.Y), 1 - 2 * (q.Y * q.Y + q.Z * q.Z));
            var pitch = MathF.Asin(Math.Clamp(2 * (q.W * q.Y - q.Z * q.X), -1, 1));
            var roll = MathF.Atan2(2 * (q.W * q.X + q.Y * q.Z), 1 - 2 * (q.X * q.X + q.Y * q.Y));
            transformInfo += $" rot=({roll * 180 / MathF.PI:F1}°, {pitch * 180 / MathF.PI:F1}°, {yaw * 180 / MathF.PI:F1}°)";
        }
        if (hasScale) transformInfo += $" scale={node.Scale}";

        _output.WriteLine($"{indent}{node.Name} ({nodeType}){meshInfo}{transformInfo}");

        // Only print first 20 nodes to avoid excessive output
        if (depth > 5) return;
        foreach (var child in node.Children.Take(15))
        {
            PrintNodeHierarchy(child, depth + 1);
        }
        if (node.Children.Count > 15)
        {
            _output.WriteLine($"{indent}  ... and {node.Children.Count - 15} more children");
        }
    }

    /// <summary>
    /// Debug specific models that have issues to understand parent chain.
    /// Key insight: NWN models can have two patterns:
    /// 1. Vertices in LOCAL space: vertex center near origin, node position provides offset
    /// 2. Vertices in WORLD space: vertex center IS the world position, node position near zero
    /// </summary>
    [Fact]
    public void DebugProblematicModels()
    {
        if (!RadoubSettings.Instance.HasGamePaths)
        {
            _output.WriteLine("SKIP: No game paths configured");
            return;
        }

        using var gameData = new GameDataService();
        if (!gameData.IsConfigured)
        {
            _output.WriteLine("SKIP: GameDataService not configured");
            return;
        }

        var modelService = new Services.ModelService(gameData);

        // Models with known issues (use exact names from appearance.2da RACE column)
        var testModels = new[]
        {
            "c_boar",          // Boar - detached hooves
            "c_beetle",        // Beetle - scattered legs
            "c_frostgia",      // Frost giant - detached lower body
            "c_chicken",       // Chicken - was working, may have regressed
            "c_troll",         // Troll - missing legs (rotation issue?)
        };

        foreach (var modelName in testModels)
        {
            _output.WriteLine($"\n=== {modelName} ===");
            var model = modelService.LoadModel(modelName);
            if (model == null)
            {
                _output.WriteLine("  NOT FOUND");
                continue;
            }

            _output.WriteLine($"  Meshes: {model.GetMeshNodes().Count()}");
            _output.WriteLine($"  Nodes: {model.EnumerateAllNodes().Count()}");

            // Check parent chain and positions for each mesh (only ones with actual vertices)
            foreach (var mesh in model.GetMeshNodes().Where(m => m.Vertices.Length > 2).Take(15))
            {
                var parentChain = new List<string>();
                var current = mesh.Parent;
                while (current != null)
                {
                    var hasParentRot = current.Orientation != System.Numerics.Quaternion.Identity;
                    var rotStr = hasParentRot ? $", rot={current.Orientation}" : "";
                    parentChain.Add($"{current.Name}(pos={current.Position}{rotStr})");
                    current = current.Parent;
                }

                // Calculate vertex bounds
                var verts = mesh.Vertices;
                var nanCount = verts.Count(v => float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Z));
                if (nanCount > 0)
                {
                    _output.WriteLine($"  Mesh '{mesh.Name}': {verts.Length} verts, {nanCount} NaN, faces={mesh.Faces.Length}, type={mesh.GetType().Name}");
                    _output.WriteLine($"    Chain: {string.Join(" <- ", parentChain)}");
                }
                else if (verts.Length > 0)
                {
                    var minX = verts.Min(v => v.X);
                    var maxX = verts.Max(v => v.X);
                    var minY = verts.Min(v => v.Y);
                    var maxY = verts.Max(v => v.Y);
                    var minZ = verts.Min(v => v.Z);
                    var maxZ = verts.Max(v => v.Z);
                    var vertCenter = new System.Numerics.Vector3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
                    var vertSpread = new System.Numerics.Vector3(maxX - minX, maxY - minY, maxZ - minZ);

                    // Check if vertices are centered around origin (local space) or offset (world space)
                    var vertCenterDist = vertCenter.Length();
                    var meshPosDist = mesh.Position.Length();
                    var pattern = vertCenterDist < 0.5f ? "LOCAL" : (meshPosDist < 0.1f ? "WORLD" : "MIXED");

                    // Check for rotation
                    var hasRot = mesh.Orientation != System.Numerics.Quaternion.Identity;
                    var rotInfo = hasRot ? $" ROT={mesh.Orientation}" : "";
                    _output.WriteLine($"  Mesh '{mesh.Name}': nodePos={mesh.Position:F2}, vertCenter={vertCenter:F2} ({pattern}){rotInfo}");
                    _output.WriteLine($"    {verts.Length} verts, {mesh.Faces.Length} faces, type={mesh.GetType().Name}, parentDepth={parentChain.Count}");
                    _output.WriteLine($"    Chain: {string.Join(" <- ", parentChain)}");
                }
            }
            if (model.GetMeshNodes().Count() > 15)
            {
                _output.WriteLine($"  ... and {model.GetMeshNodes().Count() - 15} more meshes");
            }
        }
    }

    /// <summary>
    /// Analyze skin nodes to understand bone/weight data for skeletal meshes.
    /// </summary>
    [Fact]
    public void AnalyzeSkinNodes()
    {
        if (!RadoubSettings.Instance.HasGamePaths)
        {
            _output.WriteLine("SKIP: No game paths configured");
            return;
        }

        using var gameData = new GameDataService();
        if (!gameData.IsConfigured)
        {
            _output.WriteLine("SKIP: GameDataService not configured");
            return;
        }

        var modelService = new Services.ModelService(gameData);

        var model = modelService.LoadModel("c_behold");
        if (model == null)
        {
            _output.WriteLine("c_behold not found");
            return;
        }

        foreach (var mesh in model.GetMeshNodes().OfType<Radoub.Formats.Mdl.MdlSkinNode>().Take(3))
        {
            _output.WriteLine($"\n=== Skin Mesh: {mesh.Name} ===");
            _output.WriteLine($"  Vertices: {mesh.Vertices.Length}");
            _output.WriteLine($"  Faces: {mesh.Faces.Length}");
            _output.WriteLine($"  BoneNames: {mesh.BoneNodeNames.Length} [{string.Join(", ", mesh.BoneNodeNames.Take(5))}]");
            _output.WriteLine($"  BoneQuats: {mesh.BoneQuaternions.Length}");
            _output.WriteLine($"  BoneTrans: {mesh.BoneTranslations.Length}");
            _output.WriteLine($"  BoneWeights: {mesh.BoneWeights.Length}");

            // Show first few vertices
            for (int i = 0; i < Math.Min(5, mesh.Vertices.Length); i++)
            {
                var v = mesh.Vertices[i];
                var hasNaN = float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Z);
                _output.WriteLine($"    v[{i}] = {v} {(hasNaN ? "*** NaN ***" : "")}");
            }

            // Show bone weights for first vertex
            if (mesh.BoneWeights.Length > 0)
            {
                var w = mesh.BoneWeights[0];
                _output.WriteLine($"  BoneWeight[0]: bones=[{w.Bone0}, {w.Bone1}, {w.Bone2}, {w.Bone3}] weights=[{w.Weight0}, {w.Weight1}, {w.Weight2}, {w.Weight3}]");
            }
        }
    }

    /// <summary>
    /// Test loading a few specific models to see what fails.
    /// </summary>
    [Fact]
    public void TestStaticModelLoading()
    {
        if (!RadoubSettings.Instance.HasGamePaths)
        {
            _output.WriteLine("SKIP: No game paths configured");
            return;
        }

        using var gameData = new GameDataService();
        if (!gameData.IsConfigured)
        {
            _output.WriteLine("SKIP: GameDataService not configured");
            return;
        }

        // Test models from known problematic categories
        // Names must match RACE column in appearance.2da exactly
        var testModels = new[]
        {
            // Dragons (from appearance.2da)
            "c_DrgRed",
            "c_DrgBlue",
            "c_DrgBlack",
            "c_wyrmlred",
            // Humanoids (type F and L)
            "c_troll",
            "c_ogreA",
            "c_Ogre35",
            "c_gnthill",
            "c_bugbearA",
            // Animals (type S)
            "c_bearblck",
            "c_a_bat",
            "c_boar",
            "c_a_deer",
            // Undead (type F)
            "c_skel_com01",
            "c_zomb_rot",
            "c_Lich",
            // Elementals (type S)
            "c_air",
            "c_fire",
            "c_golbone"
        };

        _output.WriteLine("=== STATIC MODEL LOADING TEST ===");
        _output.WriteLine("");

        var modelService = new Services.ModelService(gameData);

        foreach (var modelName in testModels)
        {
            var model = modelService.LoadModel(modelName);
            if (model == null)
            {
                _output.WriteLine($"[FAIL] {modelName}: Not found or failed to parse");
            }
            else
            {
                var meshCount = model.GetMeshNodes().Count();
                var hasGeometry = model.GeometryRoot != null;
                var nodeCount = model.EnumerateAllNodes().Count();
                _output.WriteLine($"[OK] {modelName}: meshes={meshCount}, nodes={nodeCount}, hasGeometry={hasGeometry}, bounds={model.BoundingMin}-{model.BoundingMax}");
            }
        }
    }
}
