using Radoub.UI.Controls;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for BrowserSaveNotifier — the host-side helper that host tools
/// (Relique etc.) call after a successful save so the browser row Tag/Name
/// updates without a full reindex (#2199 Sprint 2).
///
/// Acts as a regression guard for the SaveCurrentFileAsync wire-up: if a
/// future refactor drops the post-save notify call, the wire-up test in
/// the consuming tool will catch it via a fake IBrowserRowRefresher.
/// </summary>
public class BrowserSaveNotifierTests
{
    private sealed class RecordingRefresher : IBrowserRowRefresher
    {
        public List<string> Calls { get; } = new();
        public Task RefreshRowAsync(string filePath)
        {
            Calls.Add(filePath);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task NotifyAsync_PassesFilePathToRefresher()
    {
        var refresher = new RecordingRefresher();

        await BrowserSaveNotifier.NotifyAsync(refresher, @"C:\mod\sword.uti");

        Assert.Single(refresher.Calls);
        Assert.Equal(@"C:\mod\sword.uti", refresher.Calls[0]);
    }

    [Fact]
    public async Task NotifyAsync_NullRefresher_NoThrow()
    {
        // Defensive — host may call before InitializeItemBrowserPanel runs.
        await BrowserSaveNotifier.NotifyAsync(null, @"C:\mod\sword.uti");
    }

    [Fact]
    public async Task NotifyAsync_NullPath_DoesNotCallRefresher()
    {
        var refresher = new RecordingRefresher();

        await BrowserSaveNotifier.NotifyAsync(refresher, null);

        Assert.Empty(refresher.Calls);
    }

    [Fact]
    public async Task NotifyAsync_EmptyPath_DoesNotCallRefresher()
    {
        var refresher = new RecordingRefresher();

        await BrowserSaveNotifier.NotifyAsync(refresher, string.Empty);

        Assert.Empty(refresher.Calls);
    }

    // #2413: NotifyOrAddAsync handles a new file (reload+select) vs existing (in-place refresh).
    // The full reload/select path needs a real FileBrowserPanelBase (FlaUI); here we lock the
    // null/empty guards so a refactor can't make the helper throw on the unconfigured-panel path.

    [Fact]
    public async Task NotifyOrAddAsync_NullPanel_NoThrow()
    {
        await BrowserSaveNotifier.NotifyOrAddAsync(null, @"C:\mod\crate.utp");
    }

    [Fact]
    public async Task NotifyOrAddAsync_NullPath_NoThrow()
    {
        await BrowserSaveNotifier.NotifyOrAddAsync(null, null);
    }
}

/// <summary>
/// Tests for ItemBrowserPanel.RefreshRowAsync — the IBrowserRowRefresher
/// implementation that combines FindEntryByFilePath + RefreshEntryFromDiskAsync
/// so host tools depend on the interface, not internal lookups.
/// </summary>
public class ItemBrowserPanelRefreshRowTests
{
    private static byte[] BuildUti(string tag, string name)
    {
        var uti = new Radoub.Formats.Uti.UtiFile { TemplateResRef = "x", Tag = tag };
        uti.LocalizedName.SetString(0, name);
        return Radoub.Formats.Uti.UtiWriter.Write(uti);
    }

    // Direct unit test of ItemBrowserPanel.RefreshRowAsync would need an
    // Avalonia control instance. The behavior is exercised via the
    // ItemBrowserPanelRefreshTests round-trip (RefreshEntryFromDiskAsync)
    // + FileBrowserPanelLookupTests (FindEntryByFilePath). This test
    // confirms BrowserSaveNotifier is the seam that knits them together
    // for host-tool consumption.
    [Fact]
    public async Task NotifyAsync_DrivesRefreshThroughFakeRefresher_FullPath()
    {
        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            System.IO.File.WriteAllBytes(tempFile, BuildUti("OLD", "Old"));
            var entry = new ItemBrowserEntry
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(tempFile),
                FilePath = tempFile,
                Tag = "OLD",
                DisplayLabel = "Old",
                MetadataLoaded = true
            };
            var fakePanel = new FakePanelRefresher(entry);

            System.IO.File.WriteAllBytes(tempFile, BuildUti("NEW_TAG", "New Name"));
            await BrowserSaveNotifier.NotifyAsync(fakePanel, tempFile);

            Assert.Equal("NEW_TAG", entry.Tag);
            Assert.Equal("New Name", entry.DisplayLabel);
        }
        finally
        {
            if (System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile);
        }
    }

    private sealed class FakePanelRefresher : IBrowserRowRefresher
    {
        private readonly FileBrowserEntry _entry;
        public FakePanelRefresher(FileBrowserEntry entry) { _entry = entry; }
        public Task RefreshRowAsync(string filePath)
        {
            return string.Equals(_entry.FilePath, filePath, StringComparison.OrdinalIgnoreCase)
                ? ItemBrowserPanel.RefreshEntryFromDiskAsync(_entry)
                : Task.CompletedTask;
        }
    }
}
