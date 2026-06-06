using System.Collections.Generic;
using System.Linq;
using System.Text;
using PlaceableEditor.ViewModels;

namespace PlaceableEditor.Services;

/// <summary>
/// Reads/writes a placeable script set (#2369, #2374): the 13 event-handler ResRefs as a plain-text
/// `EventName=ResRef` file (one assigned slot per line), keyed by the slot's stable
/// <see cref="ScriptSlotViewModel.EventName"/>. Lets a builder save a reusable set (e.g. a standard
/// chest's open/close/disturbed scripts) and apply it to other placeables. Pure — no Avalonia, no
/// disk — so the host owns the file picker and undo wrapping.
/// </summary>
public static class ScriptSetService
{
    /// <summary>Serialize the assigned (non-empty) script slots to a plain-text `EventName=ResRef` file.</summary>
    public static byte[] Serialize(IEnumerable<ScriptSlotViewModel> slots)
    {
        var sb = new StringBuilder();
        sb.Append("# Reliquary placeable script set\n");
        foreach (var slot in slots.Where(s => !string.IsNullOrWhiteSpace(s.ResRef)))
            sb.Append(slot.EventName).Append('=').Append(slot.ResRef).Append('\n');
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Parse a saved script set into an event-name → ResRef map. Tolerates blank lines, `#` comments,
    /// CRLF/LF, and whitespace around the key/value. Lines without a single `=` are skipped.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Parse(byte[] bytes)
    {
        var map = new Dictionary<string, string>();
        var text = Encoding.UTF8.GetString(bytes);

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            int eq = line.IndexOf('=');
            if (eq <= 0) continue; // no key, or no separator

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (key.Length > 0)
                map[key] = value;
        }

        return map;
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
