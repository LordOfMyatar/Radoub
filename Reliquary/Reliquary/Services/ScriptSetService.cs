using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PlaceableEditor.ViewModels;

namespace PlaceableEditor.Services;

/// <summary>
/// Reads/writes a placeable script set (#2369): the 13 event-handler ResRefs serialized to JSON,
/// keyed by the slot's stable <see cref="ScriptSlotViewModel.EventName"/>. Lets a builder save a
/// reusable set (e.g. a standard chest's open/close/disturbed scripts) and apply it to other
/// placeables. Pure — no Avalonia, no disk — so the host owns the file picker and undo wrapping.
/// </summary>
public static class ScriptSetService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Serialize the assigned (non-empty) script slots to JSON bytes.</summary>
    public static byte[] Serialize(IEnumerable<ScriptSlotViewModel> slots)
    {
        var map = slots
            .Where(s => !string.IsNullOrWhiteSpace(s.ResRef))
            .ToDictionary(s => s.EventName, s => s.ResRef);
        return JsonSerializer.SerializeToUtf8Bytes(map, Options);
    }

    /// <summary>Parse a saved script set back into an event-name → ResRef map. Empty on malformed input.</summary>
    public static IReadOnlyDictionary<string, string> Parse(byte[] bytes)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(bytes)
                   ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Apply a parsed script set to the given slots, matching by EventName. A preset is the full
    /// picture: every slot is set — to the preset's value if present, otherwise cleared — so loading
    /// a set never leaves stale scripts from the previous placeable. Returns the number of slots that
    /// received a non-empty value (used for the status message).
    /// </summary>
    public static int Apply(IEnumerable<ScriptSlotViewModel> slots, IReadOnlyDictionary<string, string> set)
    {
        int assigned = 0;
        foreach (var slot in slots)
        {
            var value = set.TryGetValue(slot.EventName, out var resRef) ? resRef : string.Empty;
            slot.ResRef = value;
            if (!string.IsNullOrWhiteSpace(value)) assigned++;
        }
        return assigned;
    }
}
