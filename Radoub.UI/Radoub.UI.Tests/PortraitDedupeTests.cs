using System.Collections.Generic;
using System.Linq;
using Radoub.UI.Services;
using Radoub.UI.Views;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Defensive guard for the shared PortraitBrowserWindow (#2329): even if a context
/// fails to dedupe, the window must not list the same portrait ResRef twice.
/// Keeps the first occurrence and preserves order; case-insensitive (Aurora ResRefs).
/// </summary>
public class PortraitDedupeTests
{
    private static PortraitEntry P(ushort id, string resRef, int race = -1, int sex = -1)
        => new PortraitEntry { Id = id, ResRef = resRef, Race = race, Sex = sex };

    [Fact]
    public void DedupeByResRef_RemovesRepeats_KeepsFirstInOrder()
    {
        var input = new List<PortraitEntry>
        {
            P(0, "el_f_02_"),
            P(1, "hu_m_01_"),
            P(2, "el_f_02_"), // dup of 0
            P(3, "hu_m_01_"), // dup of 1
        };

        var result = PortraitBrowserWindow.DedupeByResRef(input).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("el_f_02_", result[0].ResRef);
        Assert.Equal("hu_m_01_", result[1].ResRef);
        Assert.Equal(0, result[0].Id); // first occurrence kept
    }

    [Fact]
    public void DedupeByResRef_IsCaseInsensitive()
    {
        var input = new List<PortraitEntry>
        {
            P(0, "hu_m_01_"),
            P(1, "Hu_M_01_"),
        };

        var result = PortraitBrowserWindow.DedupeByResRef(input).ToList();

        Assert.Single(result);
    }

    [Fact]
    public void DedupeByResRef_EmptyInput_ReturnsEmpty()
    {
        var result = PortraitBrowserWindow.DedupeByResRef(new List<PortraitEntry>()).ToList();
        Assert.Empty(result);
    }
}
