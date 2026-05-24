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

        // ITP palette tree: "MAIN > {indexPath} > RESREF" where indexPath is a
        // slash-separated sequence of zero-based indices walking the MAIN list
        // and any nested LIST lists. See ResRefReferenceScanner.ScanItpPaletteTree (#2178).
        if (segments.Length == 3 && segments[0] == "MAIN" && segments[2] == "RESREF")
        {
            return ApplyItpPaletteNode(gff, segments[1], newValue);
        }

        // DLG node: "Entry N > Sound" or "Reply N > Sound" or
        //          "Entry N > ActionParams[P] (key)" or
        //          "Entry N > RepliesList[L] > ConditionParams[P] (key)" (nested)
        if ((segments[0].StartsWith("Entry ", StringComparison.Ordinal)
             || segments[0].StartsWith("Reply ", StringComparison.Ordinal))
            && segments.Length >= 2)
        {
            return ApplyDlgNode(gff, segments, refRow, newValue);
        }

        // UTM panel pattern: "{PanelName} > Item N > InventoryRes" — panel name is dynamic,
        // resolved by walking StoreList for a panel whose struct.Type maps back to the same name
        // via Utm.StorePanels.GetPanelName. Try this BEFORE the generic nested-list branch since
        // "Weapons", "Armor", etc. are not top-level field names on the root struct.
        if (segments.Length == 3
            && segments[1].StartsWith("Item ", StringComparison.Ordinal)
            && segments[2] == "InventoryRes")
        {
            if (ApplyUtmStorePanel(gff, segments[0], segments[1], newValue))
                return true;
            // Fall through to generic — UTC/UTP also have ItemList > Item N > InventoryRes,
            // where segments[0] is the literal top-level field name "ItemList".
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

    /// <summary>
    /// Apply a RESREF update to a node inside the ITP palette tree (#2178).
    /// <paramref name="indexPath"/> is the slash-separated index sequence
    /// emitted by ResRefReferenceScanner.ScanItpPaletteTree, e.g. "0/2/1".
    /// </summary>
    private static bool ApplyItpPaletteNode(GffFile gff, string indexPath, string newValue)
    {
        if (string.IsNullOrEmpty(indexPath)) return false;

        var indices = indexPath.Split('/');
        if (indices.Length == 0) return false;

        var mainList = gff.RootStruct.GetField("MAIN")?.Value as GffList;
        if (mainList == null) return false;
        if (!int.TryParse(indices[0], out var topIdx)) return false;
        if (topIdx < 0 || topIdx >= mainList.Elements.Count) return false;

        var node = mainList.Elements[topIdx];

        for (int depth = 1; depth < indices.Length; depth++)
        {
            var listField = node.GetField("LIST")?.Value as GffList;
            if (listField == null) return false;
            if (!int.TryParse(indices[depth], out var childIdx)) return false;
            if (childIdx < 0 || childIdx >= listField.Elements.Count) return false;
            node = listField.Elements[childIdx];
        }

        var resRefField = node.GetField("RESREF");
        if (resRefField == null) return false;
        resRefField.Value = newValue;
        return true;
    }

    private static bool ApplyUtmStorePanel(
        GffFile gff, string panelName, string itemSegment, string newValue)
    {
        var storeList = gff.RootStruct.GetField("StoreList")?.Value as GffList;
        if (storeList == null) return false;

        // The scanner derives panelName from struct.Type via Utm.StorePanels.GetPanelName(int).
        // Walk StoreList looking for the panel whose Type maps back to the same name.
        var panel = storeList.Elements.FirstOrDefault(p =>
            string.Equals(
                Radoub.Formats.Utm.StorePanels.GetPanelName((int)p.Type),
                panelName,
                StringComparison.Ordinal));
        if (panel == null) return false;

        if (!int.TryParse(itemSegment["Item ".Length..], out var idx)) return false;

        var items = panel.GetField("ItemList")?.Value as GffList;
        if (items == null || idx >= items.Elements.Count) return false;

        var f = items.Elements[idx].GetField("InventoryRes");
        if (f == null) return false;
        f.Value = newValue;
        return true;
    }
}
