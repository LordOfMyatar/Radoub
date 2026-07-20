using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

public class DocumentStateTests
{
    [Fact]
    public void NewDocumentState_IsNotDirty()
    {
        var state = new DocumentState("TestTool");
        Assert.False(state.IsDirty);
    }

    [Fact]
    public void MarkDirty_WithFilePath_SetsDirty()
    {
        var state = new DocumentState("TestTool");
        state.CurrentFilePath = "/test/file.dlg";
        state.MarkDirty();
        Assert.True(state.IsDirty);
    }

    [Fact]
    public void MarkDirty_WithoutFilePath_DoesNotSetDirty()
    {
        var state = new DocumentState("TestTool");
        state.MarkDirty();
        Assert.False(state.IsDirty);
    }

    [Fact]
    public void MarkDirty_WhileLoading_DoesNotSetDirty()
    {
        var state = new DocumentState("TestTool");
        state.CurrentFilePath = "/test/file.dlg";
        state.IsLoading = true;
        state.MarkDirty();
        Assert.False(state.IsDirty);
    }

    [Fact]
    public void ClearDirty_ResetsDirtyFlag()
    {
        var state = new DocumentState("TestTool");
        state.CurrentFilePath = "/test/file.dlg";
        state.MarkDirty();
        Assert.True(state.IsDirty);

        state.ClearDirty();
        Assert.False(state.IsDirty);
    }

    [Fact]
    public void DirtyStateChanged_FiredOnMarkDirty()
    {
        var state = new DocumentState("TestTool");
        state.CurrentFilePath = "/test/file.dlg";

        var fired = false;
        state.DirtyStateChanged += () => fired = true;

        state.MarkDirty();
        Assert.True(fired);
    }

    [Fact]
    public void DirtyStateChanged_FiredOnClearDirty()
    {
        var state = new DocumentState("TestTool");
        state.CurrentFilePath = "/test/file.dlg";
        state.MarkDirty();

        var fired = false;
        state.DirtyStateChanged += () => fired = true;

        state.ClearDirty();
        Assert.True(fired);
    }

    [Fact]
    public void DirtyStateChanged_NotFiredIfAlreadyDirty()
    {
        var state = new DocumentState("TestTool");
        state.CurrentFilePath = "/test/file.dlg";
        state.MarkDirty();

        var firedCount = 0;
        state.DirtyStateChanged += () => firedCount++;

        state.MarkDirty(); // Already dirty
        Assert.Equal(0, firedCount);
    }

    [Fact]
    public void GetTitle_NoFile_ReturnsToolName()
    {
        var state = new DocumentState("TestTool");
        Assert.Equal("TestTool", state.GetTitle());
    }

    [Fact]
    public void GetTitle_WithFile_ReturnsToolAndPath()
    {
        var state = new DocumentState("TestTool");
        state.CurrentFilePath = "/test/file.dlg";

        var title = state.GetTitle();
        Assert.Contains("TestTool - ", title);
        Assert.DoesNotContain("*", title);
    }

    // #1572: title bar shows the filename only, not the full path, so the Windows
    // taskbar can identify the window when truncated.

    [Fact]
    public void GetTitle_WithFile_ShowsFilenameOnly_NotFullPath()
    {
        var state = new DocumentState("Parley");
        state.CurrentFilePath = Path.Combine("C:", "modules", "LNS_DLG", "__hench.dlg");

        Assert.Equal("Parley - __hench.dlg", state.GetTitle());
    }

    [Fact]
    public void GetTitle_WithFile_KeepsExtension()
    {
        var state = new DocumentState("Quartermaster");
        state.CurrentFilePath = Path.Combine("D:", "work", "aaatest.utc");

        Assert.Equal("Quartermaster - aaatest.utc", state.GetTitle());
    }

    [Fact]
    public void GetTitle_DirtyWithFile_AppendsAsteriskAfterFilename()
    {
        var state = new DocumentState("Parley");
        state.CurrentFilePath = Path.Combine("C:", "modules", "__hench.dlg");
        state.MarkDirty();

        Assert.Equal("Parley - __hench.dlg*", state.GetTitle());
    }

    [Fact]
    public void GetTitle_ReadOnlyWithFile_ShowsReadOnlyMarkerAfterFilename()
    {
        var state = new DocumentState("Fence");
        state.CurrentFilePath = Path.Combine("C:", "modules", "store01.utm");
        state.IsReadOnly = true;

        Assert.Equal("Fence - store01.utm [Read-Only]", state.GetTitle());
    }

    [Fact]
    public void GetTitle_ExtraInfoWithFile_PlacedAfterFilename()
    {
        var state = new DocumentState("Quartermaster");
        state.CurrentFilePath = Path.Combine("C:", "chars", "hero.bic");

        Assert.Equal("Quartermaster - hero.bic (Player)", state.GetTitle(" (Player)"));
    }

    [Fact]
    public void GetTitle_TitleSuffixIgnoredOnceFileLoaded()
    {
        // The subtitle identifies the tool when idle; with a file open the filename
        // is what matters, and keeping both makes the taskbar text unreadable.
        var state = new DocumentState("Fence", " - Merchant Editor");
        state.CurrentFilePath = Path.Combine("C:", "modules", "store01.utm");

        Assert.Equal("Fence - store01.utm", state.GetTitle());
    }

    [Fact]
    public void GetTitle_FilenameWithNoDirectory_StillWorks()
    {
        var state = new DocumentState("Relique");
        state.CurrentFilePath = "sword.uti";

        Assert.Equal("Relique - sword.uti", state.GetTitle());
    }

    [Fact]
    public void GetTitle_Dirty_IncludesAsterisk()
    {
        var state = new DocumentState("TestTool");
        state.CurrentFilePath = "/test/file.dlg";
        state.MarkDirty();

        var title = state.GetTitle();
        Assert.EndsWith("*", title);
    }

    [Fact]
    public void GetTitle_WithExtraInfo_IncludesExtraInfo()
    {
        var state = new DocumentState("TestTool");
        state.CurrentFilePath = "/test/file.bic";

        var title = state.GetTitle(" (Player)");
        Assert.Contains("(Player)", title);
    }

    [Fact]
    public void GetTitle_WithTitleSuffix_NoFile_IncludesSuffix()
    {
        var state = new DocumentState("Fence", " - Merchant Editor");
        Assert.Equal("Fence - Merchant Editor", state.GetTitle());
    }

    [Fact]
    public void MarkDirty_WhenReadOnly_DoesNotSetDirty()
    {
        var state = new DocumentState("TestTool");
        state.CurrentFilePath = "/test/file.dlg";
        state.IsReadOnly = true;
        state.MarkDirty();
        Assert.False(state.IsDirty);
    }

    [Fact]
    public void GetTitle_WhenReadOnly_ShowsIndicator()
    {
        var state = new DocumentState("TestTool");
        state.CurrentFilePath = "/test/file.dlg";
        state.IsReadOnly = true;
        var title = state.GetTitle();
        Assert.Contains("[Read-Only]", title);
    }

    [Fact]
    public void IsReadOnly_DefaultsFalse()
    {
        var state = new DocumentState("TestTool");
        Assert.False(state.IsReadOnly);
    }

    [Fact]
    public void GetTitle_AfterFilePathChange_ReturnsNewPath()
    {
        // Scenario: Open file A (clean), then open file B from Recent Files
        // GetTitle() should reflect file B even though ClearDirty() didn't fire an event
        var state = new DocumentState("TestTool");
        state.CurrentFilePath = "/test/fileA.uti";
        state.ClearDirty(); // already clean, no event fires

        // Now "open" a different file
        state.CurrentFilePath = "/test/fileB.uti";
        state.ClearDirty(); // still clean, no event fires

        var title = state.GetTitle();
        Assert.Contains("fileB.uti", title);
        Assert.DoesNotContain("fileA.uti", title);
    }

    [Fact]
    public void ClearDirty_WhenAlreadyClean_DoesNotFireEvent()
    {
        var state = new DocumentState("TestTool");
        state.CurrentFilePath = "/test/file.uti";

        bool eventFired = false;
        state.DirtyStateChanged += () => eventFired = true;

        state.ClearDirty(); // already clean
        Assert.False(eventFired);
    }
}
