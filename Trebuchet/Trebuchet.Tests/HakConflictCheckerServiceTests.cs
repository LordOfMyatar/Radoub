using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using RadoubLauncher.Services;
using Xunit;

namespace Trebuchet.Tests;

/// <summary>
/// Tests for HAK conflict detection (#1162). Exercises the pure detection core
/// (FindConflicts) so no real .hak files on disk are needed — each "HAK" is a
/// named list of ErfResourceEntry rows. Winner = first HAK in priority order
/// (BioWare IFO spec: earlier HAKs override later ones).
/// </summary>
public class HakConflictCheckerServiceTests
{
    private static ErfResourceEntry Res(string resRef, ushort type) =>
        new() { ResRef = resRef, ResourceType = type };

    private static HakContents Hak(string name, params ErfResourceEntry[] entries) =>
        new(name, entries.ToList());

    [Fact]
    public void FindConflicts_NoOverlap_ReturnsEmpty()
    {
        var haks = new[]
        {
            Hak("top", Res("alpha", ResourceTypes.Utc)),
            Hak("bottom", Res("beta", ResourceTypes.Utc)),
        };

        var conflicts = HakConflictCheckerService.FindConflicts(haks);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void FindConflicts_SameResRefSameTypeInTwoHaks_IsConflict()
    {
        var haks = new[]
        {
            Hak("top", Res("dupe", ResourceTypes.Utc)),
            Hak("bottom", Res("dupe", ResourceTypes.Utc)),
        };

        var conflicts = HakConflictCheckerService.FindConflicts(haks);

        var conflict = Assert.Single(conflicts);
        Assert.Equal("dupe", conflict.ResRef);
        Assert.Equal(ResourceTypes.Utc, conflict.ResourceType);
        Assert.Equal(new[] { "top", "bottom" }, conflict.ContainingHaks);
    }

    [Fact]
    public void FindConflicts_WinnerIsFirstHakInPriorityOrder()
    {
        // "top" appears before "bottom" in the list → top wins (first = highest priority).
        var haks = new[]
        {
            Hak("top", Res("dupe", ResourceTypes.Mdl)),
            Hak("bottom", Res("dupe", ResourceTypes.Mdl)),
        };

        var conflict = Assert.Single(HakConflictCheckerService.FindConflicts(haks));

        Assert.Equal("top", conflict.WinnerHak);
    }

    [Fact]
    public void FindConflicts_SameResRefDifferentType_IsNotConflict()
    {
        // foo.nss and foo.utc are different resources despite sharing the ResRef.
        var haks = new[]
        {
            Hak("top", Res("foo", ResourceTypes.Nss)),
            Hak("bottom", Res("foo", ResourceTypes.Utc)),
        };

        Assert.Empty(HakConflictCheckerService.FindConflicts(haks));
    }

    [Fact]
    public void FindConflicts_ThreeWayConflict_ListsAllHaksInPriorityOrder()
    {
        var haks = new[]
        {
            Hak("first", Res("shared", ResourceTypes.TwoDA)),
            Hak("second", Res("shared", ResourceTypes.TwoDA)),
            Hak("third", Res("shared", ResourceTypes.TwoDA)),
        };

        var conflict = Assert.Single(HakConflictCheckerService.FindConflicts(haks));

        Assert.Equal(new[] { "first", "second", "third" }, conflict.ContainingHaks);
        Assert.Equal("first", conflict.WinnerHak);
    }

    [Fact]
    public void FindConflicts_ResRefMatchIsCaseInsensitive()
    {
        // Aurora ResRefs are case-insensitive; "Dupe" and "dupe" are the same resource.
        var haks = new[]
        {
            Hak("top", Res("Dupe", ResourceTypes.Utc)),
            Hak("bottom", Res("dupe", ResourceTypes.Utc)),
        };

        var conflict = Assert.Single(HakConflictCheckerService.FindConflicts(haks));
        Assert.Equal(new[] { "top", "bottom" }, conflict.ContainingHaks);
    }

    [Fact]
    public void FindConflicts_DuplicateResRefWithinSingleHak_IsNotConflict()
    {
        // A resource appearing twice in ONE hak is not a cross-hak conflict.
        var haks = new[]
        {
            Hak("only", Res("dupe", ResourceTypes.Utc), Res("dupe", ResourceTypes.Utc)),
        };

        Assert.Empty(HakConflictCheckerService.FindConflicts(haks));
    }

    [Fact]
    public void FindConflicts_PopulatesExtensionForDisplay()
    {
        var haks = new[]
        {
            Hak("top", Res("dupe", ResourceTypes.Nss)),
            Hak("bottom", Res("dupe", ResourceTypes.Nss)),
        };

        var conflict = Assert.Single(HakConflictCheckerService.FindConflicts(haks));
        Assert.Equal(".nss", conflict.Extension);
    }
}
