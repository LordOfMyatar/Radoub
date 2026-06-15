using System;
using System.Collections.Generic;
using System.IO;
using Radoub.Formats.Itp;
using Radoub.Formats.Logging;

namespace Radoub.UI.Services.Palette;

/// <summary>
/// Disk bridge for the palette editor (#2477, M3): loads a module folder + resource type into a
/// ready <see cref="PaletteContext"/>. Reads the loose <c>*palcus.itp</c> (a missing/invalid file
/// becomes an empty new tree), pools the loose blueprints of that type, and seeds
/// <see cref="ItpFile.NextUseableId"/> from the paired <c>*palstd</c> skeleton when the custom file
/// omits it (the reorg mutator's id allocator cannot reach the skeleton). The skeleton is read
/// read-only and never written back.
/// </summary>
public sealed class PaletteEditorLoader
{
    private readonly Func<PaletteResourceType, IBlueprintFileGateway> _gatewayFactory;

    public PaletteEditorLoader(Func<PaletteResourceType, IBlueprintFileGateway>? gatewayFactory = null)
        => _gatewayFactory = gatewayFactory ?? (t => new BlueprintFileGateway(t));

    public PaletteContext Load(string moduleFolder, PaletteResourceType type)
    {
        if (string.IsNullOrEmpty(moduleFolder)) throw new ArgumentNullException(nameof(moduleFolder));
        var d = PaletteResourceTypeInfo.For(type);

        string customPath = Path.Combine(moduleFolder, d.CustomPaletteFile);
        ItpFile itp = ReadItpOrEmpty(customPath);

        // Seed NextUseableId from the skeleton when the custom palette omits it.
        if (itp.NextUseableId is null)
        {
            string skeletonPath = Path.Combine(moduleFolder, d.SkeletonPaletteFile);
            var skeleton = File.Exists(skeletonPath) ? ItpReader.Read(skeletonPath) : null;
            if (skeleton?.NextUseableId is byte seed) itp.NextUseableId = seed;
        }

        // Pool loose blueprints of this type (ResRef = filename without extension).
        var gateway = _gatewayFactory(type);
        var pool = new List<(string ResRef, string Path)>();
        if (Directory.Exists(moduleFolder))
        {
            foreach (var path in Directory.EnumerateFiles(moduleFolder, "*." + d.BlueprintExtension))
            {
                // Normalize to lowercase: Aurora ResRefs are case-insensitive, and this keeps the
                // pool key aligned with the lowercase ResRef text stored in the .itp tree.
                string resRef = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                pool.Add((resRef, path));
            }
        }

        var store = new LooseFileBlueprintStore(gateway, pool);
        var context = new PaletteContext(type, itp, store, customPath);
        LogClassificationSummary(context, d);
        return context;
    }

    // Diagnostic for "more uncategorized than expected": report how many pooled blueprints are
    // uncategorized (not listed in the .itp tree) and, of those, how many still carry a non-zero
    // PaletteID. A high non-zero count means a source tool set the blueprint's PaletteID byte but
    // never wrote the matching .itp tree entry — i.e. the palette tree is the incomplete half.
    private static void LogClassificationSummary(PaletteContext ctx, PaletteResourceDescriptor d)
    {
        int total = ctx.Store.ResRefs.Count;
        int uncategorized = 0, uncategorizedWithPaletteId = 0, drifted = 0;
        foreach (var resRef in ctx.Store.ResRefs)
        {
            var kind = ctx.ViewModel.Classify(resRef).Kind;
            if (kind == PalettePlacementKind.Uncategorized)
            {
                uncategorized++;
                if (ctx.Store.GetPaletteId(resRef) is byte id && id != 0) uncategorizedWithPaletteId++;
            }
            else if (kind == PalettePlacementKind.Drifted) drifted++;
        }
        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"[PaletteEditor] {d.CustomPaletteFile}: {total} blueprints, {uncategorized} uncategorized " +
            $"({uncategorizedWithPaletteId} of those have a non-zero PaletteID but no tree entry), {drifted} drifted.");
    }

    private static ItpFile ReadItpOrEmpty(string path)
    {
        if (!File.Exists(path)) return new ItpFile();
        try
        {
            return ItpReader.Read(path) ?? new ItpFile();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Palette editor: could not read '{Path.GetFileName(path)}' ({ex.Message}); starting from an empty tree.");
            return new ItpFile();
        }
    }
}
