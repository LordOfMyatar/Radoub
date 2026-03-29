using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Ifo;
using Radoub.Formats.Mdl;
using Radoub.Formats.Resolver;
using Radoub.Formats.Services;
using Xunit;

namespace Radoub.Formats.Tests.Mdl;

/// <summary>
/// Extract CEP models from HAK files for binary analysis.
/// These tests dump raw MDL bytes for hex comparison with BIF versions.
/// Investigation spike for #1676.
/// </summary>
public class CepModelExtractionTests
{
    private readonly ITestOutputHelper _output;
    private readonly IGameDataService? _gameDataService;

    // CEP HAK directory — standard NWN:EE location
    private static readonly string HakDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Neverwinter Nights", "hak");

    // Default game path for BIF access
    private static readonly string DefaultGamePath = @"D:\SteamLibrary\steamapps\common\Neverwinter Nights";

    // Models to extract for analysis
    private static readonly string[] DualSourceModels = { "c_cow", "c_blinkdog", "pmh0" };
    private static readonly string[] CepOnlyModels = { "c_firebeetl", "c_brownie", "c_feypixy", "c_dwduegar" };
    private static readonly string[] AllTargetModels = DualSourceModels.Concat(CepOnlyModels).ToArray();

    public CepModelExtractionTests(ITestOutputHelper output)
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
            // Game data not available - tests requiring it will skip
        }
    }

    [Fact]
    public void ExtractCepModelsFromHaks()
    {
        if (!Directory.Exists(HakDir))
        {
            _output.WriteLine($"SKIP: HAK directory not found: {HakDir}");
            return;
        }

        var found = ExtractFromHaks(AllTargetModels);

        _output.WriteLine($"\n=== Results: found {found.Count}/{AllTargetModels.Length} models ===\n");

        foreach (var target in AllTargetModels)
        {
            if (found.TryGetValue(target, out var result))
            {
                _output.WriteLine($"{target}: {result.data.Length} bytes from {result.hakFile}");
                DumpFileHeader(target, result.data);
            }
            else
            {
                _output.WriteLine($"{target}: NOT FOUND in any HAK");
            }
        }
    }

    [Fact]
    public void CompareBifVsHakModels()
    {
        if (!Directory.Exists(HakDir))
        {
            _output.WriteLine("SKIP: HAK directory not found");
            return;
        }
        if (_gameDataService == null)
        {
            _output.WriteLine("SKIP: Game data not available");
            return;
        }

        var hakModels = ExtractFromHaks(DualSourceModels);

        foreach (var resRef in DualSourceModels)
        {
            _output.WriteLine($"\n{"=== " + resRef + " ==="}");

            // Get BIF version
            var bifData = _gameDataService.FindBaseResource(resRef, ResourceTypes.Mdl);
            if (bifData == null)
            {
                _output.WriteLine("  BIF: NOT FOUND");
                continue;
            }
            _output.WriteLine($"  BIF: {bifData.Length} bytes");
            DumpFileHeader("  BIF", bifData);

            // Get HAK version
            if (!hakModels.TryGetValue(resRef, out var hakResult))
            {
                _output.WriteLine("  HAK: NOT FOUND");
                continue;
            }
            _output.WriteLine($"  HAK: {hakResult.data.Length} bytes (from {hakResult.hakFile})");
            DumpFileHeader("  HAK", hakResult.data);

            // Compare raw data offset interpretation
            var bifRawOffset = BitConverter.ToUInt32(bifData, 4);
            var hakRawOffset = BitConverter.ToUInt32(hakResult.data, 4);
            _output.WriteLine($"\n  rawDataOffset comparison:");
            _output.WriteLine($"    BIF: {bifRawOffset} (Reader1 modelData={bifRawOffset}, Reader2 modelData={bifRawOffset - 12})");
            _output.WriteLine($"    HAK: {hakRawOffset} (Reader1 modelData={hakRawOffset}, Reader2 modelData={hakRawOffset - 12})");
            _output.WriteLine($"    BIF file size: {bifData.Length}, expected raw start: Reader1={bifRawOffset + 12}, Reader2={bifRawOffset}");
            _output.WriteLine($"    HAK file size: {hakResult.data.Length}, expected raw start: Reader1={hakRawOffset + 12}, Reader2={hakRawOffset}");

            // Try Reader1 on both
            TryParse("Reader1-BIF", resRef, bifData, useReader2: false);
            TryParse("Reader1-HAK", resRef, hakResult.data, useReader2: false);

            // Try Reader2 on both
            TryParse("Reader2-BIF", resRef, bifData, useReader2: true);
            TryParse("Reader2-HAK", resRef, hakResult.data, useReader2: true);
        }
    }

    [Fact]
    public void TryCepOnlyModelsWithBothReaders()
    {
        if (!Directory.Exists(HakDir))
        {
            _output.WriteLine("SKIP: HAK directory not found");
            return;
        }

        var hakModels = ExtractFromHaks(CepOnlyModels);

        foreach (var resRef in CepOnlyModels)
        {
            _output.WriteLine($"\n{"=== " + resRef + " (CEP-only) ==="}");

            if (!hakModels.TryGetValue(resRef, out var hakResult))
            {
                _output.WriteLine("  NOT FOUND in any HAK");
                continue;
            }

            _output.WriteLine($"  Found in {hakResult.hakFile}: {hakResult.data.Length} bytes");
            DumpFileHeader(resRef, hakResult.data);

            TryParse("Reader1", resRef, hakResult.data, useReader2: false);
            TryParse("Reader2", resRef, hakResult.data, useReader2: true);
        }
    }

    [Theory]
    [InlineData("LNS_DLG", "CEP3 module")]
    [InlineData("Prophet - Chapter III - That Which is Destined", "CEP2 module")]
    public void ScanModuleHaksForFailingModels(string moduleName, string description)
    {
        var modulesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Neverwinter Nights", "modules");
        var moduleDir = Path.Combine(modulesDir, moduleName);
        var ifoPath = Path.Combine(moduleDir, "module.ifo");

        if (!File.Exists(ifoPath))
        {
            _output.WriteLine($"SKIP: {moduleName} IFO not found");
            return;
        }

        var ifo = IfoReader.Read(ifoPath);
        _output.WriteLine($"Module: {moduleName} ({description})");
        _output.WriteLine($"HAK list ({ifo.HakList.Count} HAKs): {string.Join(", ", ifo.HakList)}");

        var targets = new[] { "c_cow", "c_blinkdog", "pmh0", "c_firebeetl", "c_brownie", "c_feypixy", "c_dwduegar" };
        var mdlReader = new MdlReader();

        foreach (var hakName in ifo.HakList)
        {
            var hakPath = Path.Combine(HakDir, hakName + ".hak");
            if (!File.Exists(hakPath))
            {
                _output.WriteLine($"\n  {hakName}.hak: NOT FOUND on disk");
                continue;
            }

            ErfFile erf;
            try { erf = ErfReader.ReadMetadataOnly(hakPath); }
            catch { _output.WriteLine($"\n  {hakName}.hak: UNREADABLE"); continue; }

            // Check for target models
            foreach (var target in targets)
            {
                var entry = erf.FindResource(target, ResourceTypes.Mdl);
                if (entry == null) continue;

                var data = ErfReader.ExtractResource(hakPath, entry);
                var isBinary = data.Length >= 4 && BitConverter.ToUInt32(data, 0) == 0;

                _output.WriteLine($"\n  FOUND: {target}.mdl in {hakName}.hak — {data.Length} bytes, {(isBinary ? "BINARY" : "ASCII")}");

                // Try parsing with MdlReader (auto-detect)
                try
                {
                    var model = mdlReader.Parse(data);
                    var root = model.GeometryRoot;
                    var nodes = CountAllNodes(root);
                    var meshes = CountMeshNodes(root);
                    var hasNaN = HasNaNVertices(root);
                    var hasGarbage = HasGarbageNodeNames(root);
                    _output.WriteLine($"    MdlReader: OK — {nodes} nodes, {meshes} meshes, NaN={hasNaN}, garbage={hasGarbage}");
                    DumpNodeTree(root, "      ", 0);
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"    MdlReader: CRASH — {ex.GetType().Name}: {ex.Message}");
                }
            }

            // Also count total MDL files and format breakdown
            var allMdls = erf.Resources.Where(r => r.ResourceType == ResourceTypes.Mdl).ToList();
            if (allMdls.Count > 0)
            {
                int bin = 0, asc = 0;
                foreach (var e in allMdls.Take(50)) // Sample 50 for format check
                {
                    var d = ErfReader.ExtractResource(hakPath, e);
                    if (d.Length >= 4 && BitConverter.ToUInt32(d, 0) == 0) bin++; else asc++;
                }
                _output.WriteLine($"\n  {hakName}.hak: {allMdls.Count} MDL total (sample: {bin} binary, {asc} ASCII)");
            }
        }
    }

    [Fact]
    public void ValidatePmh0FromCep3ArmorHak()
    {
        // This is the exact model that #1676 reported as producing garbage:
        // "HAK version: 10036 bytes, parses to 4 nodes with garbage names, NaN-like positions, 0 meshes"
        var hakPath = Path.Combine(HakDir, "cep3_armor.hak");
        if (!File.Exists(hakPath))
        {
            _output.WriteLine("SKIP: cep3_armor.hak not found");
            return;
        }

        var erf = ErfReader.ReadMetadataOnly(hakPath);
        var entry = erf.FindResource("pmh0", ResourceTypes.Mdl);
        Assert.NotNull(entry);

        var data = ErfReader.ExtractResource(hakPath, entry);
        _output.WriteLine($"pmh0.mdl: {data.Length} bytes");
        Assert.Equal(10036, data.Length); // Match the size from #1676

        var isBinary = data.Length >= 4 && BitConverter.ToUInt32(data, 0) == 0;
        _output.WriteLine($"Format: {(isBinary ? "BINARY" : "ASCII")}");

        // Parse with MdlReader (same path QM uses)
        var mdlReader = new MdlReader();
        var model = mdlReader.Parse(data);
        var root = model.GeometryRoot;

        var totalNodes = CountAllNodes(root);
        var meshNodes = CountMeshNodes(root);

        _output.WriteLine($"Nodes: {totalNodes}, Meshes: {meshNodes}");
        _output.WriteLine($"NaN vertices: {HasNaNVertices(root)}");
        _output.WriteLine($"Garbage names: {HasGarbageNodeNames(root)}");

        // The original issue said: "4 nodes with garbage names, NaN-like positions, 0 meshes"
        // If fixed, we should see: many valid bone nodes with clean names
        // pmh0 is a skeleton model, so 0 meshes is expected — but should have >10 bone nodes
        Assert.True(totalNodes > 10, $"Expected >10 nodes for pmh0 skeleton, got {totalNodes}");
        Assert.False(HasNaNVertices(root), "pmh0 should have no NaN vertices");
        Assert.False(HasGarbageNodeNames(root), "pmh0 should have valid node names");

        // Validate that node names are recognizable NWN bone names
        var allNames = CollectNodeNames(root);
        _output.WriteLine($"\nAll node names ({allNames.Count}): {string.Join(", ", allNames)}");

        // Known NWN skeleton bone names
        var expectedBones = new[] { "rootdummy", "torso_g", "neck_g", "head_g", "pelvis_g" };
        foreach (var bone in expectedBones)
        {
            Assert.Contains(bone, allNames, StringComparer.OrdinalIgnoreCase);
            _output.WriteLine($"  Found expected bone: {bone}");
        }

        // Also try parsing with both binary readers directly to compare
        if (isBinary)
        {
            _output.WriteLine("\n--- Reader1 (MdlBinaryReader) ---");
            try
            {
                var r1 = new MdlBinaryReader();
                var m1 = r1.Parse(data);
                var n1 = CountAllNodes(m1.GeometryRoot);
                _output.WriteLine($"  Reader1: {n1} nodes, NaN={HasNaNVertices(m1.GeometryRoot)}, garbage={HasGarbageNodeNames(m1.GeometryRoot)}");
                var names1 = CollectNodeNames(m1.GeometryRoot);
                _output.WriteLine($"  Names: {string.Join(", ", names1.Take(10))}...");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  Reader1: CRASH — {ex.GetType().Name}: {ex.Message}");
            }

            _output.WriteLine("\n--- Reader2 (MdlBinaryReader2) ---");
            try
            {
                var r2 = new MdlBinaryReader2();
                var m2 = r2.Parse(data);
                var n2 = CountAllNodes(m2.GeometryRoot);
                _output.WriteLine($"  Reader2: {n2} nodes, NaN={HasNaNVertices(m2.GeometryRoot)}, garbage={HasGarbageNodeNames(m2.GeometryRoot)}");
                var names2 = CollectNodeNames(m2.GeometryRoot);
                _output.WriteLine($"  Names: {string.Join(", ", names2.Take(10))}...");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  Reader2: CRASH — {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    [Fact]
    public void ValidateBlinkdogFromCep3Hak()
    {
        // c_blinkdog from HAK was reported as "body parts separated/mispositioned" in #1676
        var hakPath = Path.Combine(HakDir, "cep3_core3.hak");
        if (!File.Exists(hakPath))
        {
            _output.WriteLine("SKIP: cep3_core3.hak not found");
            return;
        }

        var erf = ErfReader.ReadMetadataOnly(hakPath);
        var entry = erf.FindResource("c_blinkdog", ResourceTypes.Mdl);
        Assert.NotNull(entry);

        var data = ErfReader.ExtractResource(hakPath, entry);
        var isBinary = data.Length >= 4 && BitConverter.ToUInt32(data, 0) == 0;
        _output.WriteLine($"c_blinkdog.mdl: {data.Length} bytes, {(isBinary ? "BINARY" : "ASCII")}");

        // Compare BIF vs HAK parse results
        var mdlReader = new MdlReader();

        // HAK version
        var hakModel = mdlReader.Parse(data);
        var hakRoot = hakModel.GeometryRoot;
        var hakNodes = CountAllNodes(hakRoot);
        var hakMeshes = CountMeshNodes(hakRoot);
        var hakSkins = CountNodesByType<MdlSkinNode>(hakRoot);
        _output.WriteLine($"\nHAK parse: {hakNodes} nodes, {hakMeshes} meshes, {hakSkins} skins");
        _output.WriteLine($"  NaN: {HasNaNVertices(hakRoot)}, garbage: {HasGarbageNodeNames(hakRoot)}");

        Assert.True(hakNodes > 10, "Expected >10 nodes for blinkdog");
        Assert.False(HasNaNVertices(hakRoot));
        Assert.False(HasGarbageNodeNames(hakRoot));

        // BIF version for comparison
        if (_gameDataService != null)
        {
            var bifData = _gameDataService.FindBaseResource("c_blinkdog", ResourceTypes.Mdl);
            if (bifData != null)
            {
                var bifModel = mdlReader.Parse(bifData);
                var bifRoot = bifModel.GeometryRoot;
                var bifNodes = CountAllNodes(bifRoot);
                var bifMeshes = CountMeshNodes(bifRoot);
                _output.WriteLine($"\nBIF parse: {bifNodes} nodes, {bifMeshes} meshes");
                _output.WriteLine($"  NaN: {HasNaNVertices(bifRoot)}, garbage: {HasGarbageNodeNames(bifRoot)}");

                // HAK version (ASCII from CEP3) has skin meshes that BIF version doesn't
                _output.WriteLine($"\nDifferences: HAK has {hakSkins} skins, BIF has {CountNodesByType<MdlSkinNode>(bifRoot)} skins");
                _output.WriteLine($"  HAK: {hakNodes} nodes vs BIF: {bifNodes} nodes");
            }
        }
    }

    [Fact]
    public void ComparePmh0BifVsHakSkeletonStructure()
    {
        // Compare BIF pmh0 skeleton vs HAK pmh0 skeleton to understand structural differences
        if (_gameDataService == null)
        {
            _output.WriteLine("SKIP: Game data not available");
            return;
        }

        var hakPath = Path.Combine(HakDir, "cep3_armor.hak");
        if (!File.Exists(hakPath))
        {
            _output.WriteLine("SKIP: cep3_armor.hak not found");
            return;
        }

        var mdlReader = new MdlReader();

        // BIF version
        var bifData = _gameDataService.FindBaseResource("pmh0", ResourceTypes.Mdl);
        Assert.NotNull(bifData);
        var bifModel = mdlReader.Parse(bifData);
        var bifNames = CollectNodeNames(bifModel.GeometryRoot);

        // HAK version
        var erf = ErfReader.ReadMetadataOnly(hakPath);
        var entry = erf.FindResource("pmh0", ResourceTypes.Mdl);
        Assert.NotNull(entry);
        var hakData = ErfReader.ExtractResource(hakPath, entry);
        var hakModel = mdlReader.Parse(hakData);
        var hakNames = CollectNodeNames(hakModel.GeometryRoot);

        _output.WriteLine($"BIF pmh0: {bifData.Length} bytes, {bifNames.Count} nodes");
        _output.WriteLine($"HAK pmh0: {hakData.Length} bytes, {hakNames.Count} nodes");

        // Nodes in BIF but not HAK
        var bifOnly = bifNames.Except(hakNames, StringComparer.OrdinalIgnoreCase).ToList();
        _output.WriteLine($"\nBIF-only nodes ({bifOnly.Count}): {string.Join(", ", bifOnly)}");

        // Nodes in HAK but not BIF
        var hakOnly = hakNames.Except(bifNames, StringComparer.OrdinalIgnoreCase).ToList();
        _output.WriteLine($"HAK-only nodes ({hakOnly.Count}): {string.Join(", ", hakOnly)}");

        // Common nodes
        var common = bifNames.Intersect(hakNames, StringComparer.OrdinalIgnoreCase).ToList();
        _output.WriteLine($"Common nodes ({common.Count}): {string.Join(", ", common)}");

        // Critical body part attachment points that must exist in both
        var criticalNodes = new[] { "rootdummy", "torso_g", "neck_g", "head_g", "pelvis_g",
            "Lbicep_g", "Rbicep_g", "lthigh_g", "rthigh_g", "lshin_g", "rshin_g",
            "lfoot_g", "rfoot_g", "lhand_g", "rhand_g" };

        _output.WriteLine("\nCritical attachment point check:");
        foreach (var node in criticalNodes)
        {
            var inBif = bifNames.Contains(node, StringComparer.OrdinalIgnoreCase);
            var inHak = hakNames.Contains(node, StringComparer.OrdinalIgnoreCase);
            var status = (inBif, inHak) switch
            {
                (true, true) => "OK",
                (true, false) => "MISSING IN HAK",
                (false, true) => "HAK-ONLY",
                _ => "MISSING IN BOTH"
            };
            _output.WriteLine($"  {node}: {status}");
        }
    }

    [Fact]
    public void TryParseAsciiCepModelsWithMdlReader()
    {
        if (!Directory.Exists(HakDir))
        {
            _output.WriteLine("SKIP: HAK directory not found");
            return;
        }

        // Find ASCII creature models from CEP HAKs
        var coreHaks = Directory.GetFiles(HakDir, "cep*_core*.hak").OrderBy(f => f).ToArray();
        var mdlReader = new MdlReader();
        int tested = 0, passed = 0, failed = 0;

        foreach (var hakFile in coreHaks)
        {
            var erf = ErfReader.ReadMetadataOnly(hakFile);
            var creatureMdls = erf.Resources
                .Where(r => r.ResourceType == ResourceTypes.Mdl &&
                       (r.ResRef.StartsWith("c_") || r.ResRef.StartsWith("pmh") || r.ResRef.StartsWith("pmf")))
                .ToList();

            foreach (var entry in creatureMdls)
            {
                var data = ErfReader.ExtractResource(hakFile, entry);
                var isBinary = data.Length >= 4 && BitConverter.ToUInt32(data, 0) == 0;
                if (isBinary) continue; // Skip binary — tested separately

                tested++;
                try
                {
                    var model = mdlReader.Parse(data);
                    var root = model.GeometryRoot;
                    var nodes = CountAllNodes(root);
                    var meshes = CountMeshNodes(root);
                    var hasNaN = HasNaNVertices(root);
                    var hasGarbage = HasGarbageNodeNames(root);

                    if (hasNaN || hasGarbage)
                    {
                        _output.WriteLine($"  {entry.ResRef} ({Path.GetFileName(hakFile)}): GARBAGE — {nodes} nodes, NaN={hasNaN}, garbage={hasGarbage}");
                        failed++;
                    }
                    else
                    {
                        if (tested <= 10 || nodes == 0)
                            _output.WriteLine($"  {entry.ResRef} ({Path.GetFileName(hakFile)}): OK — {nodes} nodes, {meshes} meshes");
                        passed++;
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"  {entry.ResRef} ({Path.GetFileName(hakFile)}): CRASH — {ex.GetType().Name}: {ex.Message}");
                    failed++;
                    if (failed >= 20) break; // Don't flood output
                }
            }
            if (failed >= 20) break;
        }

        _output.WriteLine($"\nASCII creature models: {tested} tested, {passed} passed, {failed} failed");
        _output.WriteLine($"Pass rate: {(tested > 0 ? 100.0 * passed / tested : 0):F1}%");
    }

    [Fact]
    public void TryParseBinaryCepModelsWithReader1()
    {
        if (!Directory.Exists(HakDir))
        {
            _output.WriteLine("SKIP: HAK directory not found");
            return;
        }

        // Grab binary creature models from the first CEP core HAK
        var coreHak = Directory.GetFiles(HakDir, "cep2_core0.hak").FirstOrDefault();
        if (coreHak == null)
        {
            _output.WriteLine("SKIP: cep2_core0.hak not found");
            return;
        }

        var erf = ErfReader.ReadMetadataOnly(coreHak);
        var creatureMdls = erf.Resources
            .Where(r => r.ResourceType == ResourceTypes.Mdl && r.ResRef.StartsWith("c_"))
            .Take(20) // Sample 20 creature models
            .ToList();

        _output.WriteLine($"Testing {creatureMdls.Count} binary creature models from cep2_core0.hak\n");

        int passed = 0, failed = 0, ascii = 0;
        var reader1 = new MdlBinaryReader();
        var mdlReader = new MdlReader();

        foreach (var entry in creatureMdls)
        {
            var data = ErfReader.ExtractResource(coreHak, entry);
            var isBinary = data.Length >= 4 && BitConverter.ToUInt32(data, 0) == 0;
            if (!isBinary) { ascii++; continue; }

            try
            {
                // Try with MdlReader (auto-detect, uses Reader1 for binary)
                var model = mdlReader.Parse(data);
                var root = model.GeometryRoot;
                var nodes = CountAllNodes(root);
                var meshes = CountMeshNodes(root);
                var hasNaN = HasNaNVertices(root);
                var hasGarbage = HasGarbageNodeNames(root);

                if (hasNaN || hasGarbage || nodes < 2)
                {
                    _output.WriteLine($"  {entry.ResRef}: GARBAGE — {nodes} nodes, {meshes} meshes, NaN={hasNaN}, garbage={hasGarbage}");
                    failed++;
                }
                else
                {
                    _output.WriteLine($"  {entry.ResRef}: OK — {nodes} nodes, {meshes} meshes");
                    passed++;
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  {entry.ResRef}: CRASH — {ex.GetType().Name}: {ex.Message}");
                failed++;
            }
        }

        _output.WriteLine($"\nResults: {passed} passed, {failed} failed, {ascii} skipped (ASCII)");
        _output.WriteLine($"Pass rate: {(creatureMdls.Count - ascii > 0 ? 100.0 * passed / (creatureMdls.Count - ascii) : 0):F1}%");
    }

    [Fact]
    public void ScanAllCepHaksForMdlFormat()
    {
        if (!Directory.Exists(HakDir))
        {
            _output.WriteLine("SKIP: HAK directory not found");
            return;
        }

        var coreHaks = Directory.GetFiles(HakDir, "cep*_core*.hak").OrderBy(f => f).ToArray();
        _output.WriteLine($"Scanning {coreHaks.Length} CEP core HAKs for MDL files...\n");

        int totalMdl = 0, binaryCount = 0, asciiCount = 0;

        foreach (var hakFile in coreHaks)
        {
            var erf = ErfReader.ReadMetadataOnly(hakFile);
            var mdls = erf.Resources.Where(r => r.ResourceType == ResourceTypes.Mdl).ToList();
            if (mdls.Count == 0) continue;

            _output.WriteLine($"{Path.GetFileName(hakFile)}: {mdls.Count} MDL files");

            foreach (var entry in mdls)
            {
                var data = ErfReader.ExtractResource(hakFile, entry);
                var isBinary = data.Length >= 4 && BitConverter.ToUInt32(data, 0) == 0;
                if (isBinary) binaryCount++; else asciiCount++;

                // Report all creature models and our targets
                if (entry.ResRef.StartsWith("c_") || entry.ResRef.StartsWith("pmh") || entry.ResRef.StartsWith("pmf"))
                {
                    _output.WriteLine($"  {entry.ResRef}.mdl: {data.Length} bytes, {(isBinary ? "BINARY" : "ASCII")}");
                }
            }

            totalMdl += mdls.Count;
        }

        _output.WriteLine($"\nTotal: {totalMdl} MDL files — {binaryCount} binary, {asciiCount} ASCII");
    }

    #region Helpers

    private void TryParse(string label, string resRef, byte[] data, bool useReader2)
    {
        try
        {
            MdlModel model;
            if (useReader2)
            {
                var reader = new MdlBinaryReader2();
                model = reader.Parse(data);
            }
            else
            {
                var reader = new MdlBinaryReader();
                model = reader.Parse(data);
            }

            var root = model.GeometryRoot;
            var meshNodes = CountMeshNodes(root);
            var skinNodes = CountNodesByType<MdlSkinNode>(root);
            var totalNodes = CountAllNodes(root);

            _output.WriteLine($"  {label}: OK — {totalNodes} nodes, {meshNodes} meshes, {skinNodes} skins");

            // Check for garbage indicators
            var hasNaN = HasNaNVertices(root);
            var hasGarbageNames = HasGarbageNodeNames(root);
            if (hasNaN) _output.WriteLine($"  {label}: ⚠ NaN vertices detected");
            if (hasGarbageNames) _output.WriteLine($"  {label}: ⚠ garbage node names detected");

            DumpNodeTree(root, "    ", 0);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"  {label}: FAILED — {ex.GetType().Name}: {ex.Message}");
        }
    }

    private Dictionary<string, (string hakFile, byte[] data)> ExtractFromHaks(string[] targets)
    {
        var found = new Dictionary<string, (string hakFile, byte[] data)>();
        var hakFiles = Directory.GetFiles(HakDir, "*.hak", SearchOption.TopDirectoryOnly);

        _output.WriteLine($"Scanning {hakFiles.Length} HAK files...");

        foreach (var hakFile in hakFiles.OrderBy(f => f))
        {
            try
            {
                var erf = ErfReader.ReadMetadataOnly(hakFile);

                foreach (var target in targets)
                {
                    if (found.ContainsKey(target)) continue;
                    var entry = erf.FindResource(target, ResourceTypes.Mdl);
                    if (entry != null)
                    {
                        var data = ErfReader.ExtractResource(hakFile, entry);
                        found[target] = (Path.GetFileName(hakFile), data);
                    }
                }
            }
            catch
            {
                // Skip unreadable HAKs
            }
        }

        return found;
    }

    private void DumpFileHeader(string name, byte[] data)
    {
        if (data.Length < 12)
        {
            _output.WriteLine($"    {name}: too small ({data.Length} bytes)");
            return;
        }

        var zero = BitConverter.ToUInt32(data, 0);
        var rawDataOffset = BitConverter.ToUInt32(data, 4);
        var rawDataSize = BitConverter.ToUInt32(data, 8);

        _output.WriteLine($"    Header: zero=0x{zero:X8}, rawDataOffset={rawDataOffset}, rawDataSize={rawDataSize}");
        _output.WriteLine($"    File size: {data.Length}");

        // Check if rawDataOffset makes more sense as file offset or model data size
        var reader1ModelSize = rawDataOffset;
        var reader2ModelSize = rawDataOffset > 12 ? rawDataOffset - 12 : 0;
        var expectedFileSize1 = 12 + rawDataOffset + rawDataSize; // Reader1: header + modelData + rawData
        var expectedFileSize2 = rawDataOffset + rawDataSize; // Reader2: rawDataOffset is file offset

        _output.WriteLine($"    Reader1 interpretation: modelDataSize={reader1ModelSize}, expected file size={expectedFileSize1}");
        _output.WriteLine($"    Reader2 interpretation: modelDataSize={reader2ModelSize}, expected file size={expectedFileSize2}");

        var matchesReader1 = Math.Abs((long)expectedFileSize1 - data.Length) <= 4;
        var matchesReader2 = Math.Abs((long)expectedFileSize2 - data.Length) <= 4;
        _output.WriteLine($"    Matches Reader1: {matchesReader1}, Matches Reader2: {matchesReader2}");

        // Dump first 64 bytes as hex
        var hexLen = Math.Min(64, data.Length);
        _output.WriteLine($"    First {hexLen} bytes:");
        for (int i = 0; i < hexLen; i += 16)
        {
            var hex = "";
            var ascii = "";
            for (int j = 0; j < 16 && i + j < hexLen; j++)
            {
                var b = data[i + j];
                hex += $"{b:X2} ";
                ascii += (b >= 0x20 && b <= 0x7E) ? (char)b : '.';
            }
            _output.WriteLine($"      {i:X4}: {hex,-48} {ascii}");
        }
    }

    private static int CountMeshNodes(MdlNode? node)
    {
        if (node == null) return 0;
        int count = node is MdlTrimeshNode ? 1 : 0;
        foreach (var child in node.Children)
            count += CountMeshNodes(child);
        return count;
    }

    private static int CountNodesByType<T>(MdlNode? node) where T : MdlNode
    {
        if (node == null) return 0;
        int count = node is T ? 1 : 0;
        foreach (var child in node.Children)
            count += CountNodesByType<T>(child);
        return count;
    }

    private static int CountAllNodes(MdlNode? node)
    {
        if (node == null) return 0;
        int count = 1;
        foreach (var child in node.Children)
            count += CountAllNodes(child);
        return count;
    }

    private static bool HasNaNVertices(MdlNode? node)
    {
        if (node == null) return false;
        if (node is MdlTrimeshNode mesh && mesh.Vertices != null)
        {
            foreach (var v in mesh.Vertices)
                if (float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Z))
                    return true;
        }
        return node.Children.Any(c => HasNaNVertices(c));
    }

    private static bool HasGarbageNodeNames(MdlNode? node)
    {
        if (node == null) return false;
        if (!string.IsNullOrEmpty(node.Name))
        {
            if (node.Name.Any(c => c < 0x20 || c > 0x7E))
                return true;
        }
        return node.Children.Any(c => HasGarbageNodeNames(c));
    }

    private static List<string> CollectNodeNames(MdlNode? node)
    {
        var names = new List<string>();
        CollectNodeNamesRecursive(node, names);
        return names;
    }

    private static void CollectNodeNamesRecursive(MdlNode? node, List<string> names)
    {
        if (node == null) return;
        if (!string.IsNullOrEmpty(node.Name))
            names.Add(node.Name);
        foreach (var child in node.Children)
            CollectNodeNamesRecursive(child, names);
    }

    private void DumpNodeTree(MdlNode? node, string indent, int depth)
    {
        if (node == null || depth > 10) return;
        var type = node.GetType().Name.Replace("Mdl", "").Replace("Node", "");
        var extra = "";
        if (node is MdlTrimeshNode mesh)
            extra = $" verts={mesh.Vertices?.Length ?? 0} faces={mesh.Faces?.Length ?? 0}";
        _output.WriteLine($"{indent}{type}: \"{node.Name}\"{extra}");
        foreach (var child in node.Children)
            DumpNodeTree(child, indent + "  ", depth + 1);
    }

    #endregion
}
