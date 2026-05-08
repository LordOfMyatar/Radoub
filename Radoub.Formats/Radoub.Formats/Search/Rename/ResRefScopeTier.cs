namespace Radoub.Formats.Search.Rename;

/// <summary>
/// Classifies a ResRef reference by where it was found and how confident
/// the match is. Used by the rename preview to render appropriate visual
/// treatment (e.g., low-confidence .nss matches get a warning badge).
/// </summary>
public enum ResRefScopeTier
{
    /// <summary>Typed GFF ResRef field (e.g., UTC.Conversation, GIT.TemplateResRef).</summary>
    TypedGffField,

    /// <summary>DLG ActionParams or ConditionParams value substring match.</summary>
    DlgScriptParam,

    /// <summary>.nss source with surrounding quotes — high confidence.</summary>
    NssQuotedString,

    /// <summary>.nss source as bare substring (no quotes) — low confidence; user should verify.</summary>
    NssBareSubstring
}
