using Radoub.Formats.Gff;
using Radoub.Formats.Search.Rename;

namespace Radoub.UI.Services.Search;

/// <summary>
/// Applies a value update to a specific GFF reference site, given a Location
/// string produced by ResRefReferenceScanner. Each scanner location format
/// (e.g., "Creature List > Item N > TemplateResRef") has a corresponding
/// applier branch here.
///
/// Extracted from ResRefRenameOrchestrator so that adding a new location format
/// (e.g., UTM store panels, DLG ConditionParams) doesn't bloat the orchestrator.
/// </summary>
public class GffReferenceLocationApplier
{
    /// <summary>
    /// Apply <paramref name="newValue"/> to the field identified by <c>refRow.Location</c>
    /// in the parsed GFF tree. Returns true when the update was applied; false when
    /// the location string could not be parsed or the target field was missing.
    /// </summary>
    public bool Apply(GffFile gff, ResRefReference refRow, string newValue)
    {
        var loc = refRow.Location ?? string.Empty;

        // Top-level scalar field: location is just the field name (e.g., "Conversation").
        // Also handle the case where a registered FieldDefinition supplies the GffPath.
        if (!loc.Contains('>'))
            return ApplyTopLevel(gff, refRow, loc, newValue);

        var segments = loc.Split(" > ", StringSplitOptions.TrimEntries);

        // DLG node: "Entry N > Sound" or "Reply N > Sound" or
        //          "Entry N > ActionParams[P] (key)" or
        //          "Entry N > RepliesList[L] > ConditionParams[P] (key)" (nested)
        if ((segments[0].StartsWith("Entry ", StringComparison.Ordinal)
             || segments[0].StartsWith("Reply ", StringComparison.Ordinal))
            && segments.Length >= 2)
        {
            return ApplyDlgNode(gff, segments, refRow, newValue);
        }

        // Generic nested-list pattern: "ListName > Item N > FieldName" or
        // "ListName > Slot N > FieldName" (UTC Equip_ItemList uses "Slot" wording)
        if (segments.Length == 3
            && (segments[1].StartsWith("Item ", StringComparison.Ordinal)
                || segments[1].StartsWith("Slot ", StringComparison.Ordinal)))
        {
            return ApplyNestedListItem(gff, segments[0], segments[1], segments[2], newValue);
        }

        return false;
    }

    private static bool ApplyTopLevel(GffFile gff, ResRefReference refRow, string loc, string newValue)
    {
        // Prefer the registered GffPath when present; otherwise the Location string IS the field name.
        var fieldName = refRow.Field?.GffPath ?? loc;
        if (string.IsNullOrEmpty(fieldName)) return false;

        var f = gff.RootStruct.GetField(fieldName);
        if (f == null) return false;
        f.Value = newValue;
        return true;
    }

    private static bool ApplyNestedListItem(
        GffFile gff, string listName, string itemSegment, string fieldName, string newValue)
    {
        var idxText = itemSegment.StartsWith("Item ", StringComparison.Ordinal)
            ? itemSegment["Item ".Length..]
            : itemSegment["Slot ".Length..];
        if (!int.TryParse(idxText, out var idx)) return false;

        var list = gff.RootStruct.GetField(listName)?.Value as GffList;
        if (list == null || idx >= list.Elements.Count) return false;

        var f = list.Elements[idx].GetField(fieldName);
        if (f == null) return false;
        f.Value = newValue;
        return true;
    }

    private static bool ApplyDlgNode(
        GffFile gff, string[] segments, ResRefReference refRow, string newValue)
    {
        // segments[0] is always "Entry N" or "Reply N".
        var nodeSegment = segments[0];
        var (listName, idxText) = nodeSegment.StartsWith("Entry ", StringComparison.Ordinal)
            ? ("EntryList", nodeSegment["Entry ".Length..])
            : ("ReplyList", nodeSegment["Reply ".Length..]);

        if (!int.TryParse(idxText, out var idx)) return false;

        var nodes = gff.RootStruct.GetField(listName)?.Value as GffList;
        if (nodes == null || idx >= nodes.Elements.Count) return false;

        var node = nodes.Elements[idx];

        // Simple node field (e.g., "Entry N > Sound")
        if (segments.Length == 2)
        {
            var fieldOrParam = segments[1];
            if (fieldOrParam.StartsWith("ActionParams[", StringComparison.Ordinal)
                || fieldOrParam.StartsWith("ConditionParams[", StringComparison.Ordinal))
            {
                return ApplyDlgParam(node, fieldOrParam, refRow, newValue);
            }
            var f = node.GetField(fieldOrParam);
            if (f == null) return false;
            f.Value = newValue;
            return true;
        }

        // Nested link form: "Entry N > RepliesList[L] > ConditionParams[P] (key)"
        // (per scanner's ConditionParams location format)
        if (segments.Length == 3)
        {
            var linkSegment = segments[1];  // "RepliesList[L]" or "EntriesList[L]"
            var open = linkSegment.IndexOf('[');
            var close = linkSegment.IndexOf(']');
            if (open < 0 || close < 0) return false;

            var linkListName = linkSegment[..open];
            if (!int.TryParse(linkSegment[(open + 1)..close], out var linkIdx)) return false;

            var linkList = node.GetField(linkListName)?.Value as GffList;
            if (linkList == null || linkIdx >= linkList.Elements.Count) return false;

            var link = linkList.Elements[linkIdx];
            return ApplyDlgParam(link, segments[2], refRow, newValue);
        }

        return false;
    }

    private static bool ApplyDlgParam(
        GffStruct container, string segment, ResRefReference refRow, string newValue)
    {
        // Parse "ActionParams[P] (key)" or "ConditionParams[P] (key)"
        var openBracket = segment.IndexOf('[');
        var closeBracket = segment.IndexOf(']');
        if (openBracket < 0 || closeBracket < 0) return false;

        var paramListName = segment[..openBracket];
        if (!int.TryParse(segment[(openBracket + 1)..closeBracket], out var paramIdx)) return false;

        var paramList = container.GetField(paramListName)?.Value as GffList;
        if (paramList == null || paramIdx >= paramList.Elements.Count) return false;

        var valueField = paramList.Elements[paramIdx].GetField("Value");
        if (valueField?.Value is not string oldFullValue) return false;

        // Substring replace at MatchOffset (per spec: substring match, not whole-equality, for params)
        if (refRow.MatchOffset < 0 || refRow.MatchOffset + refRow.MatchLength > oldFullValue.Length)
            return false;

        var actual = oldFullValue.Substring(refRow.MatchOffset, refRow.MatchLength);
        if (!string.Equals(actual, refRow.OldValue, StringComparison.OrdinalIgnoreCase))
            return false;

        valueField.Value = string.Concat(
            oldFullValue.AsSpan(0, refRow.MatchOffset),
            newValue,
            oldFullValue.AsSpan(refRow.MatchOffset + refRow.MatchLength));
        return true;
    }

}
