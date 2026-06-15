using System;
using System.Collections.Generic;
using System.Linq;

namespace Radoub.UI.Services.Palette;

/// <summary>
/// <see cref="IBlueprintPaletteStore"/> over the module's loose blueprint files (#2477, M3).
/// Captures each blueprint's ORIGINAL <c>PaletteID</c> at construction, stages
/// <see cref="SetPaletteId"/> changes in memory, and — because it alone knows both the original
/// and staged value — owns blueprint write-set assembly via <see cref="BuildBlueprintWrites"/>.
/// Disk access is confined to the injected <see cref="IBlueprintFileGateway"/>.
/// </summary>
public sealed class LooseFileBlueprintStore : IBlueprintPaletteStore
{
    private sealed class Entry
    {
        public required string ResRef { get; init; }
        public required string Path { get; init; }
        public byte Original { get; init; }
        public byte Staged { get; set; }
    }

    private readonly IBlueprintFileGateway _gateway;
    private readonly Dictionary<string, Entry> _byResRef =
        new(StringComparer.OrdinalIgnoreCase);

    /// <param name="gateway">Reads/rewrites PaletteID for the four blueprint formats.</param>
    /// <param name="pool">(ResRef, file path) for every loose blueprint of the selected type.</param>
    public LooseFileBlueprintStore(IBlueprintFileGateway gateway, IEnumerable<(string ResRef, string Path)> pool)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        foreach (var (resRef, path) in pool ?? throw new ArgumentNullException(nameof(pool)))
        {
            if (string.IsNullOrEmpty(resRef) || _byResRef.ContainsKey(resRef)) continue;
            byte original = gateway.ReadPaletteId(path);
            _byResRef[resRef] = new Entry { ResRef = resRef, Path = path, Original = original, Staged = original };
        }
    }

    public byte? GetPaletteId(string resRef)
        => _byResRef.TryGetValue(resRef, out var e) ? e.Staged : (byte?)null;

    public bool SetPaletteId(string resRef, byte paletteId)
    {
        if (!_byResRef.TryGetValue(resRef, out var e)) return false;
        e.Staged = paletteId;
        return true;
    }

    public bool Contains(string resRef) => _byResRef.ContainsKey(resRef);

    // Snapshot, not the live Keys view, so callers cannot observe internal dictionary state.
    public IReadOnlyCollection<string> ResRefs => _byResRef.Keys.ToList();

    /// <summary>
    /// One <see cref="PaletteFileWrite"/> per blueprint whose staged <c>PaletteID</c> differs from
    /// its original. <c>ProduceBytes</c> applies the staged id via the gateway; <c>Validate</c>
    /// re-reads the produced bytes' PaletteID and requires it to equal the staged id.
    /// Enumerate once at save time: changes staged after enumeration are not captured.
    /// </summary>
    public IEnumerable<PaletteFileWrite> BuildBlueprintWrites()
    {
        foreach (var e in _byResRef.Values.Where(e => e.Staged != e.Original))
        {
            var entry = e; // capture
            yield return new PaletteFileWrite(
                Path: entry.Path,
                ProduceBytes: () => _gateway.ProduceBytesWithPaletteId(entry.Path, entry.Staged),
                Validate: bytes => _gateway.ReadPaletteIdFromBytes(bytes) == entry.Staged);
        }
    }
}
