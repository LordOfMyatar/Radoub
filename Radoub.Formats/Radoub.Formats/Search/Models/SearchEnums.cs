namespace Radoub.Formats.Search;

/// <summary>
/// Type of GFF field being searched. Determines matching behavior.
/// </summary>
public enum SearchFieldType
{
    /// <summary>CExoLocString — search all language variants</summary>
    LocString,
    /// <summary>CExoString — plain string</summary>
    Text,
    /// <summary>CResRef — max 16 chars, warn on replace overflow</summary>
    ResRef,
    /// <summary>Tag field — case-insensitive by default</summary>
    Tag,
    /// <summary>Script reference — exact match by default</summary>
    Script,
    /// <summary>Script parameter key/value pair</summary>
    ScriptParam,
    /// <summary>Local variable name or string value</summary>
    Variable
}

/// <summary>
/// Logical category for grouping fields in UI.
/// </summary>
public enum SearchFieldCategory
{
    /// <summary>Player-facing text (dialog, names, descriptions)</summary>
    Content,
    /// <summary>Tags, resrefs, template references</summary>
    Identity,
    /// <summary>Script references and parameters</summary>
    Script,
    /// <summary>Comments, sounds, quest tags, non-game-facing data</summary>
    Metadata,
    /// <summary>Local variables (VarTable)</summary>
    Variable
}
