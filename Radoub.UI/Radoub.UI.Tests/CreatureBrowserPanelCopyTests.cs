using Radoub.Formats.Utc;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for CreatureBrowserPanel.ApplyUtcCopyCustomizations —
/// UTC-specific byte mutation for Copy-to-Module (#1479).
/// The dialog "Name" field maps to FirstName; LastName is preserved.
/// </summary>
public class CreatureBrowserPanelCopyTests
{
    private static byte[] BuildSourceCreature()
    {
        var source = new UtcFile
        {
            TemplateResRef = "nw_blinkdog",
            Tag = "NW_BLINKDOG"
        };
        source.FirstName.SetString(0, "Blink");
        source.LastName.SetString(0, "Dog");
        return UtcWriter.Write(source);
    }

    [Fact]
    public void ApplyUtcCopyCustomizations_RewritesTemplateResRefTagFirstName()
    {
        var sourceBytes = BuildSourceCreature();
        var result = new CopyToModuleResult(
            NewResRef: "my_blink_dog",
            NewTag: "MyBlinkTag",
            NewName: "Bruno");

        var modified = CreatureBrowserPanel.ApplyUtcCopyCustomizations(sourceBytes, result);
        var parsed = UtcReader.Read(modified);

        Assert.Equal("my_blink_dog", parsed.TemplateResRef);
        Assert.Equal("MyBlinkTag", parsed.Tag);
        Assert.Equal("Bruno", parsed.FirstName.GetDefault());
    }

    [Fact]
    public void ApplyUtcCopyCustomizations_LastNameNotOverwrittenByDialogName()
    {
        var sourceBytes = BuildSourceCreature();
        var result = new CopyToModuleResult("my_dog", "MyTag", "Bruno");

        var modified = CreatureBrowserPanel.ApplyUtcCopyCustomizations(sourceBytes, result);
        var parsed = UtcReader.Read(modified);

        // Dialog "Name" only maps to FirstName — LastName is preserved.
        Assert.Equal("Dog", parsed.LastName.GetDefault());
    }

    [Fact]
    public void ApplyUtcCopyCustomizations_NullTagLeavesTagUnchanged()
    {
        var sourceBytes = BuildSourceCreature();
        var result = new CopyToModuleResult("my_dog", NewTag: null, NewName: "Bruno");

        var modified = CreatureBrowserPanel.ApplyUtcCopyCustomizations(sourceBytes, result);
        var parsed = UtcReader.Read(modified);

        Assert.Equal("NW_BLINKDOG", parsed.Tag);
    }

    [Fact]
    public void ApplyUtcCopyCustomizations_NullNameLeavesFirstNameUnchanged()
    {
        var sourceBytes = BuildSourceCreature();
        var result = new CopyToModuleResult("my_dog", NewTag: "MyTag", NewName: null);

        var modified = CreatureBrowserPanel.ApplyUtcCopyCustomizations(sourceBytes, result);
        var parsed = UtcReader.Read(modified);

        Assert.Equal("Blink", parsed.FirstName.GetDefault());
    }
}
