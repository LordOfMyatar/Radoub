using RadoubLauncher.Services;
using Xunit;

namespace Trebuchet.Tests;

/// <summary>
/// #2179: checkbox-per-row rename scope. RenameScopeSelection is the pure model
/// behind the Marlinspike results-tree checkboxes — tri-state group cascade and the
/// checked-file set that feeds the rename selectionFilter. Tested without FlaUI.
/// </summary>
public class RenameScopeSelectionTests
{
    private static RenameScopeSelection TwoGroups()
    {
        var sel = new RenameScopeSelection();
        sel.AddFile("uti", "a.uti");
        sel.AddFile("uti", "b.uti");
        sel.AddFile("utc", "c.utc");
        return sel;
    }

    [Fact]
    public void Files_start_checked_by_default()
    {
        var sel = TwoGroups();
        Assert.True(sel.IsFileChecked("a.uti"));
        Assert.Equal(3, sel.SelectedFilePaths.Count);
        Assert.True(sel.HasSelection);
    }

    [Fact]
    public void Can_add_file_unchecked()
    {
        var sel = new RenameScopeSelection();
        sel.AddFile("uti", "a.uti", isChecked: false);
        Assert.False(sel.IsFileChecked("a.uti"));
        Assert.Empty(sel.SelectedFilePaths);
        Assert.False(sel.HasSelection);
    }

    [Fact]
    public void Unchecking_one_file_drops_it_from_selection()
    {
        var sel = TwoGroups();
        sel.SetFileChecked("b.uti", false);
        Assert.False(sel.SelectedFilePaths.Contains("b.uti"));
        Assert.Contains("a.uti", sel.SelectedFilePaths);
        Assert.Contains("c.utc", sel.SelectedFilePaths);
    }

    [Fact]
    public void Group_all_checked_when_every_child_checked()
    {
        var sel = TwoGroups();
        Assert.Equal(GroupCheckState.All, sel.GetGroupState("uti"));
    }

    [Fact]
    public void Group_partial_when_some_children_checked()
    {
        var sel = TwoGroups();
        sel.SetFileChecked("a.uti", false);
        Assert.Equal(GroupCheckState.Partial, sel.GetGroupState("uti"));
    }

    [Fact]
    public void Group_none_when_no_children_checked()
    {
        var sel = TwoGroups();
        sel.SetFileChecked("a.uti", false);
        sel.SetFileChecked("b.uti", false);
        Assert.Equal(GroupCheckState.None, sel.GetGroupState("uti"));
    }

    [Fact]
    public void Group_cascade_unchecks_all_children()
    {
        var sel = TwoGroups();
        sel.SetGroupChecked("uti", false);
        Assert.False(sel.IsFileChecked("a.uti"));
        Assert.False(sel.IsFileChecked("b.uti"));
        Assert.True(sel.IsFileChecked("c.utc")); // other group untouched
        Assert.Equal(GroupCheckState.None, sel.GetGroupState("uti"));
    }

    [Fact]
    public void Group_cascade_rechecks_all_children()
    {
        var sel = TwoGroups();
        sel.SetGroupChecked("uti", false);
        sel.SetGroupChecked("uti", true);
        Assert.Equal(GroupCheckState.All, sel.GetGroupState("uti"));
    }

    [Fact]
    public void SetAll_toggles_every_group()
    {
        var sel = TwoGroups();
        sel.SetAllChecked(false);
        Assert.Empty(sel.SelectedFilePaths);
        Assert.False(sel.HasSelection);
        sel.SetAllChecked(true);
        Assert.Equal(3, sel.SelectedFilePaths.Count);
    }

    [Fact]
    public void Selection_is_case_insensitive_for_file_paths()
    {
        var sel = new RenameScopeSelection();
        sel.AddFile("uti", "Item.uti");
        sel.SetFileChecked("ITEM.UTI", false);
        Assert.False(sel.IsFileChecked("item.uti"));
        Assert.Empty(sel.SelectedFilePaths);
    }

    [Fact]
    public void Unknown_group_state_is_none()
    {
        var sel = TwoGroups();
        Assert.Equal(GroupCheckState.None, sel.GetGroupState("dlg"));
    }
}
