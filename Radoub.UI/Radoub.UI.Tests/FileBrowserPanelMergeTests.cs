using Radoub.UI.Controls;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for FileBrowserPanelBase.MergeEntries — the dedup logic used
/// when lazy-loaded entries (HAK/BIF checkbox toggle) need to be added
/// to the master entry list after initial load (#1133).
/// </summary>
public class FileBrowserPanelMergeTests
{
    [Fact]
    public void MergeEntries_EmptyTarget_AddsAll()
    {
        var target = new List<FileBrowserEntry>();
        var source = new List<FileBrowserEntry>
        {
            new() { Name = "creature_a", Source = "BIF" },
            new() { Name = "creature_b", Source = "BIF" }
        };

        FileBrowserPanelBase.MergeEntries(target, source);

        Assert.Equal(2, target.Count);
        Assert.Contains(target, e => e.Name == "creature_a");
        Assert.Contains(target, e => e.Name == "creature_b");
    }

    [Fact]
    public void MergeEntries_DuplicateName_SkipsExisting()
    {
        var target = new List<FileBrowserEntry>
        {
            new() { Name = "shared_creature", Source = "Module" }
        };
        var source = new List<FileBrowserEntry>
        {
            new() { Name = "shared_creature", Source = "BIF" },
            new() { Name = "unique_bif", Source = "BIF" }
        };

        FileBrowserPanelBase.MergeEntries(target, source);

        Assert.Equal(2, target.Count);
        // Original module entry preserved (not replaced by BIF)
        Assert.Equal("Module", target.First(e => e.Name == "shared_creature").Source);
        Assert.Contains(target, e => e.Name == "unique_bif");
    }

    [Fact]
    public void MergeEntries_CaseInsensitiveDedup()
    {
        var target = new List<FileBrowserEntry>
        {
            new() { Name = "NW_DrowRogue005", Source = "Module" }
        };
        var source = new List<FileBrowserEntry>
        {
            new() { Name = "nw_drowrogue005", Source = "BIF" }
        };

        FileBrowserPanelBase.MergeEntries(target, source);

        Assert.Single(target);
        Assert.Equal("Module", target[0].Source); // Module version kept
    }

    [Fact]
    public void MergeEntries_EmptySource_NoChanges()
    {
        var target = new List<FileBrowserEntry>
        {
            new() { Name = "existing", Source = "Module" }
        };

        FileBrowserPanelBase.MergeEntries(target, new List<FileBrowserEntry>());

        Assert.Single(target);
    }

    [Fact]
    public void MergeEntries_MultipleCalls_AccumulateEntries()
    {
        var target = new List<FileBrowserEntry>
        {
            new() { Name = "module_creature", Source = "Module" }
        };

        // First merge: HAK entries
        var hakEntries = new List<FileBrowserEntry>
        {
            new() { Name = "hak_creature", Source = "HAK", IsFromHak = true }
        };
        FileBrowserPanelBase.MergeEntries(target, hakEntries);

        // Second merge: BIF entries
        var bifEntries = new List<FileBrowserEntry>
        {
            new() { Name = "bif_creature", Source = "BIF" }
        };
        FileBrowserPanelBase.MergeEntries(target, bifEntries);

        Assert.Equal(3, target.Count);
    }

    [Fact]
    public void MergeEntries_DuplicatesWithinSource_OnlyFirstAdded()
    {
        var target = new List<FileBrowserEntry>();
        var source = new List<FileBrowserEntry>
        {
            new() { Name = "duplicate", Source = "HAK: cep.hak" },
            new() { Name = "duplicate", Source = "HAK: prc.hak" }
        };

        FileBrowserPanelBase.MergeEntries(target, source);

        Assert.Single(target);
        Assert.Equal("HAK: cep.hak", target[0].Source); // First wins
    }
}
