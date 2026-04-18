using Radoub.Formats.Utm;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for StoreBrowserPanel.ApplyUtmCopyCustomizations —
/// UTM-specific byte mutation applied when a user copies a store
/// from BIF/HAK into the module directory (#2017).
/// </summary>
public class StoreBrowserPanelCopyTests
{
    private static byte[] BuildSourceStore()
    {
        var source = new UtmFile
        {
            ResRef = "nw_stk_01",
            Tag = "NW_STK_STANDARD",
            MarkUp = 35,
            MarkDown = 50
        };
        source.LocName.SetString(0, "Standard Shop");
        return UtmWriter.Write(source);
    }

    [Fact]
    public void ApplyUtmCopyCustomizations_RewritesAllThreeFields()
    {
        var sourceBytes = BuildSourceStore();
        var result = new CopyToModuleResult(
            NewResRef: "my_test_store",
            NewTag: "MyTestTag",
            NewName: "Bob's Emporium");

        var modified = StoreBrowserPanel.ApplyUtmCopyCustomizations(sourceBytes, result);
        var parsed = UtmReader.Read(modified);

        Assert.Equal("my_test_store", parsed.ResRef);
        Assert.Equal("MyTestTag", parsed.Tag);
        Assert.Equal("Bob's Emporium", parsed.LocName.GetDefault());
    }

    [Fact]
    public void ApplyUtmCopyCustomizations_PreservesOtherFields()
    {
        var sourceBytes = BuildSourceStore();
        var result = new CopyToModuleResult("my_test_store", "MyTestTag", "My Name");

        var modified = StoreBrowserPanel.ApplyUtmCopyCustomizations(sourceBytes, result);
        var parsed = UtmReader.Read(modified);

        Assert.Equal(35, parsed.MarkUp);
        Assert.Equal(50, parsed.MarkDown);
    }

    [Fact]
    public void ApplyUtmCopyCustomizations_NullTagLeavesTagUnchanged()
    {
        var sourceBytes = BuildSourceStore();
        var result = new CopyToModuleResult("my_test_store", NewTag: null, NewName: "New Name");

        var modified = StoreBrowserPanel.ApplyUtmCopyCustomizations(sourceBytes, result);
        var parsed = UtmReader.Read(modified);

        Assert.Equal("my_test_store", parsed.ResRef);
        Assert.Equal("NW_STK_STANDARD", parsed.Tag);  // unchanged
        Assert.Equal("New Name", parsed.LocName.GetDefault());
    }

    [Fact]
    public void ApplyUtmCopyCustomizations_NullNameLeavesLocNameUnchanged()
    {
        var sourceBytes = BuildSourceStore();
        var result = new CopyToModuleResult("my_test_store", NewTag: "NewTag", NewName: null);

        var modified = StoreBrowserPanel.ApplyUtmCopyCustomizations(sourceBytes, result);
        var parsed = UtmReader.Read(modified);

        Assert.Equal("my_test_store", parsed.ResRef);
        Assert.Equal("NewTag", parsed.Tag);
        Assert.Equal("Standard Shop", parsed.LocName.GetDefault());  // unchanged
    }

    [Fact]
    public void ApplyUtmCopyCustomizations_ResRefAlwaysReplaced()
    {
        // ResRef is required in the dialog, so the result always carries a value —
        // verify the mutation always writes it even when Tag/Name are null.
        var sourceBytes = BuildSourceStore();
        var result = new CopyToModuleResult("renamed_only", null, null);

        var modified = StoreBrowserPanel.ApplyUtmCopyCustomizations(sourceBytes, result);
        var parsed = UtmReader.Read(modified);

        Assert.Equal("renamed_only", parsed.ResRef);
    }
}
