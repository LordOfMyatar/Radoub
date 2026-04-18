using Radoub.Formats.Uti;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for ItemBrowserPanel.ApplyUtiCopyCustomizations —
/// UTI-specific byte mutation for Copy-to-Module (#1479).
/// </summary>
public class ItemBrowserPanelCopyTests
{
    private static byte[] BuildSourceItem()
    {
        var source = new UtiFile
        {
            TemplateResRef = "nw_wblcl001",
            Tag = "NW_WBLCL001"
        };
        source.LocalizedName.SetString(0, "Club");
        return UtiWriter.Write(source);
    }

    [Fact]
    public void ApplyUtiCopyCustomizations_RewritesAllThreeFields()
    {
        var sourceBytes = BuildSourceItem();
        var result = new CopyToModuleResult(
            NewResRef: "my_club",
            NewTag: "MyClubTag",
            NewName: "Cudgel of Ages");

        var modified = ItemBrowserPanel.ApplyUtiCopyCustomizations(sourceBytes, result);
        var parsed = UtiReader.Read(modified);

        Assert.Equal("my_club", parsed.TemplateResRef);
        Assert.Equal("MyClubTag", parsed.Tag);
        Assert.Equal("Cudgel of Ages", parsed.LocalizedName.GetDefault());
    }

    [Fact]
    public void ApplyUtiCopyCustomizations_NullTagLeavesTagUnchanged()
    {
        var sourceBytes = BuildSourceItem();
        var result = new CopyToModuleResult("my_club", NewTag: null, NewName: "Stick");

        var modified = ItemBrowserPanel.ApplyUtiCopyCustomizations(sourceBytes, result);
        var parsed = UtiReader.Read(modified);

        Assert.Equal("NW_WBLCL001", parsed.Tag);
    }

    [Fact]
    public void ApplyUtiCopyCustomizations_NullNameLeavesLocalizedNameUnchanged()
    {
        var sourceBytes = BuildSourceItem();
        var result = new CopyToModuleResult("my_club", NewTag: "MyTag", NewName: null);

        var modified = ItemBrowserPanel.ApplyUtiCopyCustomizations(sourceBytes, result);
        var parsed = UtiReader.Read(modified);

        Assert.Equal("Club", parsed.LocalizedName.GetDefault());
    }
}
