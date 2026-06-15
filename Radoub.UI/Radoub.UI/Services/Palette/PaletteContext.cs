using System;
using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Itp;
using Radoub.UI.Undo;
using Radoub.UI.ViewModels;

namespace Radoub.UI.Services.Palette;

/// <summary>
/// Per-resource-type editing state for the palette editor (#2477, M3). Bundles the working
/// <see cref="ItpFile"/>, the blueprint store, the M2 <see cref="PaletteEditorViewModel"/>, an
/// <see cref="UndoRedoManager"/>, and the on-disk custom-palette path. Each resource type gets its
/// own context; switching type disposes the old one and builds a new one (full isolation).
/// Owns save write-set assembly: the <c>.itp</c> write followed by the store's changed-blueprint
/// writes.
/// </summary>
public sealed class PaletteContext
{
    public PaletteResourceType Type { get; }
    public ItpFile Palette { get; }
    public LooseFileBlueprintStore Store { get; }
    public string CustomPalettePath { get; }
    public PaletteEditorViewModel ViewModel { get; }
    public UndoRedoManager UndoManager { get; } = new();

    /// <param name="onTreeChanged">UI refresh callback passed to the M2 view model. Null headless.</param>
    public PaletteContext(
        PaletteResourceType type,
        ItpFile palette,
        LooseFileBlueprintStore store,
        string customPalettePath,
        Action? onTreeChanged = null)
    {
        Type = type;
        Palette = palette ?? throw new ArgumentNullException(nameof(palette));
        Store = store ?? throw new ArgumentNullException(nameof(store));
        CustomPalettePath = customPalettePath ?? throw new ArgumentNullException(nameof(customPalettePath));
        ViewModel = new PaletteEditorViewModel(palette, store, onTreeChanged);
    }

    /// <summary>
    /// The atomic save write-set: the <c>.itp</c> palette first (validated by a structural
    /// round-trip), then one write per changed blueprint (from the store). Materialize once and
    /// hand to <see cref="PaletteSaveTransaction.Commit"/>.
    /// </summary>
    public IReadOnlyList<PaletteFileWrite> BuildWriteSet()
    {
        var writes = new List<PaletteFileWrite>
        {
            new PaletteFileWrite(
                Path: CustomPalettePath,
                ProduceBytes: () => ItpWriter.Write(Palette),
                Validate: bytes => ItpStructurallyEqual(Palette, ItpReader.Read(bytes))),
        };
        writes.AddRange(Store.BuildBlueprintWrites());
        return writes;
    }

    // Round-trip guard: the re-read tree must match the in-memory tree's categories, ids, nesting,
    // and blueprint placements. Aurora tolerates field reordering, so this compares structure, not
    // bytes. (A lost placement is the corruption this guards against.)
    private static bool ItpStructurallyEqual(ItpFile expected, ItpFile? actual)
    {
        if (actual == null) return false;
        return NodesEqual(expected.MainNodes, actual.MainNodes);
    }

    private static bool NodesEqual(List<PaletteNode> a, List<PaletteNode> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            switch (a[i], b[i])
            {
                case (PaletteCategoryNode ca, PaletteCategoryNode cb):
                    if (ca.Id != cb.Id) return false;
                    var aRefs = ca.Blueprints.Select(x => x.ResRef).OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
                    var bRefs = cb.Blueprints.Select(x => x.ResRef).OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
                    if (!aRefs.SequenceEqual(bRefs, StringComparer.OrdinalIgnoreCase)) return false;
                    if (!NodesEqual(ca.Children, cb.Children)) return false;
                    break;
                case (PaletteBranchNode ba, PaletteBranchNode bb):
                    if (!NodesEqual(ba.Children, bb.Children)) return false;
                    break;
                case (PaletteBlueprintNode pa, PaletteBlueprintNode pb):
                    if (!string.Equals(pa.ResRef, pb.ResRef, StringComparison.OrdinalIgnoreCase)) return false;
                    break;
                default:
                    return false; // node-kind mismatch
            }
        }
        return true;
    }
}
