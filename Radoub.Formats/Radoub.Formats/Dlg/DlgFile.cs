using Radoub.Formats.Gff;

namespace Radoub.Formats.Dlg;

/// <summary>
/// Represents a DLG (Dialog) file used by Aurora Engine games.
/// DLG files are GFF-based and store conversation/dialog data.
/// Reference: BioWare Aurora Dialog Format specification, neverwinter.nim
/// </summary>
public class DlgFile
{
    /// <summary>
    /// File type signature - should be "DLG "
    /// </summary>
    public string FileType { get; set; } = "DLG ";

    /// <summary>
    /// File version - typically "V3.2"
    /// </summary>
    public string FileVersion { get; set; } = "V3.2";

    /// <summary>
    /// Delay in milliseconds before displaying next entry (NPC line).
    /// Default: 0 (use game settings)
    /// </summary>
    public uint DelayEntry { get; set; }

    /// <summary>
    /// Delay in milliseconds before displaying next reply (PC line).
    /// Default: 0 (use game settings)
    /// </summary>
    public uint DelayReply { get; set; }

    /// <summary>
    /// Word count for the dialog (used by toolset statistics).
    /// </summary>
    public uint NumWords { get; set; }

    /// <summary>
    /// Script to run when conversation ends normally.
    /// </summary>
    public string EndConversation { get; set; } = string.Empty;

    /// <summary>
    /// Script to run when conversation is aborted.
    /// </summary>
    public string EndConverAbort { get; set; } = string.Empty;

    /// <summary>
    /// If true, prevents camera zoom during conversation.
    /// </summary>
    public bool PreventZoomIn { get; set; }

    /// <summary>
    /// List of NPC dialog entries (spoken by NPCs).
    /// </summary>
    public List<DlgEntry> Entries { get; set; } = new();

    /// <summary>
    /// List of PC dialog replies (spoken by the player).
    /// </summary>
    public List<DlgReply> Replies { get; set; } = new();

    /// <summary>
    /// Starting points for the conversation.
    /// Each start points to an Entry with optional conditions.
    /// </summary>
    public List<DlgLink> StartingList { get; set; } = new();
}

/// <summary>
/// An NPC dialog entry (spoken by an NPC or creature).
/// </summary>
public class DlgEntry
{
    /// <summary>
    /// Speaker tag override. If empty, uses conversation owner.
    /// </summary>
    public string Speaker { get; set; } = string.Empty;

    /// <summary>
    /// Animation to play during this line.
    /// </summary>
    public uint Animation { get; set; }

    /// <summary>
    /// If true, animation loops until the line completes.
    /// </summary>
    public bool AnimLoop { get; set; }

    /// <summary>
    /// Localized dialog text.
    /// </summary>
    public CExoLocString Text { get; set; } = new();

    /// <summary>
    /// Script to run when this entry is displayed.
    /// </summary>
    public string Script { get; set; } = string.Empty;

    /// <summary>
    /// Script action parameters (name/value pairs).
    /// </summary>
    public List<DlgParam> ActionParams { get; set; } = new();

    /// <summary>
    /// Delay override for this specific entry (0xFFFFFFFF = use default).
    /// </summary>
    public uint Delay { get; set; } = 0xFFFFFFFF;

    /// <summary>
    /// Toolset comment (not shown in game).
    /// </summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Sound file to play with this line.
    /// </summary>
    public string Sound { get; set; } = string.Empty;

    /// <summary>
    /// Quest tag to update.
    /// </summary>
    public string Quest { get; set; } = string.Empty;

    /// <summary>
    /// Quest entry ID to set (only valid if Quest is set).
    /// 0xFFFFFFFF means no entry update.
    /// </summary>
    public uint QuestEntry { get; set; } = 0xFFFFFFFF;

    /// <summary>
    /// Links to player replies following this entry.
    /// </summary>
    public List<DlgLink> RepliesList { get; set; } = new();
}

/// <summary>
/// A PC dialog reply (spoken by the player).
/// </summary>
public class DlgReply
{
    /// <summary>
    /// Animation to play during this line.
    /// </summary>
    public uint Animation { get; set; }

    /// <summary>
    /// If true, animation loops until the line completes.
    /// </summary>
    public bool AnimLoop { get; set; }

    /// <summary>
    /// Localized dialog text.
    /// </summary>
    public CExoLocString Text { get; set; } = new();

    /// <summary>
    /// Script to run when this reply is selected.
    /// </summary>
    public string Script { get; set; } = string.Empty;

    /// <summary>
    /// Script action parameters (name/value pairs).
    /// </summary>
    public List<DlgParam> ActionParams { get; set; } = new();

    /// <summary>
    /// Delay override for this specific reply (0xFFFFFFFF = use default).
    /// </summary>
    public uint Delay { get; set; } = 0xFFFFFFFF;

    /// <summary>
    /// Toolset comment (not shown in game).
    /// </summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Sound file to play with this line.
    /// </summary>
    public string Sound { get; set; } = string.Empty;

    /// <summary>
    /// Quest tag to update.
    /// </summary>
    public string Quest { get; set; } = string.Empty;

    /// <summary>
    /// Quest entry ID to set (only valid if Quest is set).
    /// 0xFFFFFFFF means no entry update.
    /// </summary>
    public uint QuestEntry { get; set; } = 0xFFFFFFFF;

    /// <summary>
    /// Links to NPC entries following this reply.
    /// </summary>
    public List<DlgLink> EntriesList { get; set; } = new();
}

/// <summary>
/// A link/pointer to another dialog node.
/// Used in StartingList, RepliesList, and EntriesList.
/// </summary>
public class DlgLink
{
    /// <summary>
    /// Index into the target list (Entries or Replies).
    /// </summary>
    public uint Index { get; set; }

    /// <summary>
    /// Script that must return TRUE for this link to be active.
    /// Empty string means always active.
    /// </summary>
    public string Active { get; set; } = string.Empty;

    /// <summary>
    /// Condition script parameters (name/value pairs).
    /// </summary>
    public List<DlgParam> ConditionParams { get; set; } = new();

    /// <summary>
    /// If true, this is a "link" to an existing node rather than a child traversal.
    /// Links don't recurse when walking the tree.
    /// </summary>
    public bool IsChild { get; set; }

    /// <summary>
    /// Comment for this link (only present when IsChild=true).
    /// </summary>
    public string LinkComment { get; set; } = string.Empty;
}

/// <summary>
/// A script parameter (key/value pair).
/// Used for ActionParams and ConditionParams.
/// </summary>
public class DlgParam
{
    /// <summary>
    /// Parameter key/name.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Parameter value.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
