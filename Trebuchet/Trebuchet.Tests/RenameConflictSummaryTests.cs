using System.Collections.Generic;
using Radoub.Formats.Search.Rename;
using RadoubLauncher.Services;

namespace Trebuchet.Tests;

/// <summary>
/// Tests for RenameConflictSummary — the pure helper that buckets rename plans
/// and validator-rejected reasons into the three categories the
/// RenameConflictDialog renders (#2179, #2182).
/// </summary>
public class RenameConflictSummaryTests
{
    private static ResRefRenamePlan Plan(string oldName, string newName, string ext, bool autoSuffix)
        => new()
        {
            OldName = oldName,
            NewName = newName,
            ResourceType = 2027,
            SourceFilePath = $@"C:\m\{oldName}{ext}",
            TargetFilePath = $@"C:\m\{newName}{ext}",
            Validation = autoSuffix
                ? ResRefValidationResult.Ok(newName, null, autoSuffix: true)
                : ResRefValidationResult.Ok(newName, null)
        };

    [Fact]
    public void Build_SeparatesCleanSuffixedAndSkipped()
    {
        var plans = new List<ResRefRenamePlan>
        {
            Plan("louis", "lewie", ".utc", autoSuffix: false),
            Plan("bob", "bob_2", ".utc", autoSuffix: true),
        };
        var rejected = new List<string> { "\"toolongname12345678\" — 17 characters (16 max). Try '...'." };

        var summary = RenameConflictSummary.Build(plans, rejected);

        Assert.Single(summary.WillRename);
        Assert.Single(summary.AutoSuffixed);
        Assert.Single(summary.Skipped);
        Assert.True(summary.HasConflicts);
    }

    [Fact]
    public void Build_NoConflicts_WhenAllCleanAndNothingRejected()
    {
        var plans = new List<ResRefRenamePlan> { Plan("louis", "lewie", ".utc", autoSuffix: false) };

        var summary = RenameConflictSummary.Build(plans, new List<string>());

        Assert.False(summary.HasConflicts);
        Assert.Single(summary.WillRename);
        Assert.Empty(summary.AutoSuffixed);
        Assert.Empty(summary.Skipped);
    }

    [Fact]
    public void AutoSuffixedRow_ExplainsWhy()
    {
        var plans = new List<ResRefRenamePlan> { Plan("bob", "bob_2", ".utc", autoSuffix: true) };

        var summary = RenameConflictSummary.Build(plans, new List<string>());

        var row = Assert.Single(summary.AutoSuffixed);
        Assert.Contains("bob.utc", row);        // original name + ext
        Assert.Contains("bob_2.utc", row);      // suffixed name + ext
        Assert.Contains("already exists", row); // the "why" (#2182)
    }

    [Fact]
    public void Build_NullRejected_TreatedAsEmpty()
    {
        var plans = new List<ResRefRenamePlan> { Plan("louis", "lewie", ".utc", autoSuffix: false) };

        var summary = RenameConflictSummary.Build(plans, null!);

        Assert.Empty(summary.Skipped);
        Assert.False(summary.HasConflicts);
    }
}
