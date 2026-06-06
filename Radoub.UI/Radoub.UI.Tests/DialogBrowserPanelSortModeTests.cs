using System.Collections.Generic;
using System.Reflection;
using Avalonia.Headless.XUnit;
using Radoub.UI.Controls;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Coverage for DialogBrowserPanel's ResRef-only overrides (#2360). The DLG format
/// has no Tag or LocName fields, so the panel must restrict sort modes to ResRef and
/// disable the Tag/Name rename path. A regression here would offer broken sort/rename
/// options that silently produce empty or wrong results.
/// </summary>
public class DialogBrowserPanelSortModeTests
{
    /// <summary>Exposes the protected sort-mode and rename overrides for assertion.</summary>
    private sealed class TestablePanel : DialogBrowserPanel
    {
        public IReadOnlyList<BrowserSortMode> ExposedSortModes
            => (IReadOnlyList<BrowserSortMode>)typeof(DialogBrowserPanel)
                .GetProperty("SupportedSortModes", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(this)!;

        public bool ExposedSupportsTagNameRename
            => (bool)typeof(DialogBrowserPanel)
                .GetMethod("SupportsTagNameRename", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(this, null)!;
    }

    [AvaloniaFact]
    public void SupportedSortModes_IsResRefOnly()
    {
        var panel = new TestablePanel();

        var modes = panel.ExposedSortModes;
        Assert.Single(modes);
        Assert.Equal(BrowserSortMode.ResRef, modes[0]);
    }

    [AvaloniaFact]
    public void SupportsTagNameRename_IsFalse()
    {
        var panel = new TestablePanel();

        // DLG has no Tag/Name fields — only the ResRef (filename) rename path applies.
        Assert.False(panel.ExposedSupportsTagNameRename);
    }
}
