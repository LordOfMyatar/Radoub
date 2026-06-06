using Radoub.Formats.Utp;
using PlaceableEditor.Views.Panels;

namespace PlaceableEditor.Tests.Panels;

/// <summary>
/// PlaceableBrowserPanel reads Tag + Name from UTP bytes for the browser's
/// Name/Tag columns (#2294). Tests the pure metadata-read seam without spinning
/// up Avalonia.
/// </summary>
public class PlaceableBrowserPanelIndexingTests
{
    private static byte[] BuildUtp(string resRef, string tag, string name)
    {
        var utp = new UtpFile
        {
            TemplateResRef = resRef,
            Tag = tag
        };
        utp.LocName.SetString(0, name);
        return UtpWriter.Write(utp);
    }

    [Fact]
    public void ReadUtpMetadata_ReturnsTagAndName()
    {
        var bytes = BuildUtp("boulder001", "BOULDER_TAG", "Granite Boulder");

        var (tag, name) = PlaceableBrowserPanel.ReadUtpMetadata(bytes);

        Assert.Equal("BOULDER_TAG", tag);
        Assert.Equal("Granite Boulder", name);
    }

    [Fact]
    public void ReadUtpMetadata_EmptyName_ReturnsEmptyString()
    {
        var bytes = BuildUtp("plain001", "PLAIN_TAG", "");

        var (tag, name) = PlaceableBrowserPanel.ReadUtpMetadata(bytes);

        Assert.Equal("PLAIN_TAG", tag);
        Assert.Equal("", name);
    }

    [Fact]
    public void ReadUtpMetadata_GarbageBytes_ReturnsEmptyPair()
    {
        var (tag, name) = PlaceableBrowserPanel.ReadUtpMetadata(new byte[] { 0x00, 0x01, 0x02 });

        Assert.Equal("", tag);
        Assert.Equal("", name);
    }
}
