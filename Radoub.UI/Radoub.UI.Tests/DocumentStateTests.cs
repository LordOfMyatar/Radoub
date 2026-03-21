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
}
