using System.Collections.Generic;
using System.IO;
using System.Linq;
using Radoub.Formats.Search.Rename;

namespace RadoubLauncher.Services;

/// <summary>
/// Pure: buckets rename plans + validator-rejected reasons into the three
/// categories the RenameConflictDialog renders (#2179, #2182). No I/O, no UI.
///
/// Replaces the old per-collision AutoSuffixCollisionDialog sequence: all
/// conflicts are surfaced once, in one dialog, with the "why" inline.
/// </summary>
public sealed class RenameConflictSummary
{
    /// <summary>Clean plans: "louis.utc → lewie.utc".</summary>
    public IReadOnlyList<string> WillRename { get; init; } = new List<string>();

    /// <summary>Auto-suffixed plans with the collision reason inline (#2182).</summary>
    public IReadOnlyList<string> AutoSuffixed { get; init; } = new List<string>();

    /// <summary>Validator-rejected entries (full reason list, not just the first).</summary>
    public IReadOnlyList<string> Skipped { get; init; } = new List<string>();

    /// <summary>True when a heavier conflict dialog is warranted.</summary>
    public bool HasConflicts => AutoSuffixed.Count > 0 || Skipped.Count > 0;

    public static RenameConflictSummary Build(
        IReadOnlyList<ResRefRenamePlan> plans, IReadOnlyList<string> rejectedReasons)
    {
        var clean = new List<string>();
        var suffixed = new List<string>();

        foreach (var p in plans)
        {
            var ext = Path.GetExtension(p.SourceFilePath);
            if (p.Validation.AutoSuffixApplied)
                suffixed.Add(
                    $"{p.OldName}{ext} → {p.NewName}{ext} — " +
                    $"\"{p.OldName}{ext}\" already exists in this module, so a numbered suffix keeps both files.");
            else
                clean.Add($"{p.OldName}{ext} → {p.NewName}{ext}");
        }

        return new RenameConflictSummary
        {
            WillRename = clean,
            AutoSuffixed = suffixed,
            Skipped = rejectedReasons?.ToList() ?? new List<string>()
        };
    }
}
