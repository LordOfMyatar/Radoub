using DialogEditor.Models.Sound;
using Xunit;

namespace DialogEditor.Tests;

/// <summary>
/// Tests for SoundFileInfo model — computed properties.
/// Issue #1241: Sound Browser overhaul.
/// </summary>
public class SoundFileInfoTests
{
    [Fact]
    public void IsCompatible_MonoValidWav_ReturnsTrue()
    {
        var info = new SoundFileInfo { IsMono = true, IsValidWav = true, ChannelUnknown = false };
        Assert.True(info.IsCompatible);
    }

    [Fact]
    public void IsCompatible_StereoValidWav_ReturnsFalse()
    {
        var info = new SoundFileInfo { IsMono = false, IsValidWav = true, ChannelUnknown = false };
        Assert.False(info.IsCompatible);
    }

    [Fact]
    public void IsCompatible_MonoInvalidWav_ReturnsFalse()
    {
        var info = new SoundFileInfo { IsMono = true, IsValidWav = false, ChannelUnknown = false };
        Assert.False(info.IsCompatible);
    }

    [Fact]
    public void IsCompatible_ChannelUnknownValidWav_ReturnsTrue()
    {
        var info = new SoundFileInfo { IsMono = false, ChannelUnknown = true, IsValidWav = true };
        Assert.True(info.IsCompatible);
    }

    [Fact]
    public void IsCompatible_ChannelUnknownInvalidWav_ReturnsFalse()
    {
        var info = new SoundFileInfo { IsMono = false, ChannelUnknown = true, IsValidWav = false };
        Assert.False(info.IsCompatible);
    }

    [Fact]
    public void IsFromHak_WithHakPathAndErfEntry_ReturnsTrue()
    {
        var info = new SoundFileInfo
        {
            HakPath = "/some/path.hak",
            ErfEntry = new Radoub.Formats.Erf.ErfResourceEntry()
        };
        Assert.True(info.IsFromHak);
    }

    [Fact]
    public void IsFromHak_WithoutErfEntry_ReturnsFalse()
    {
        var info = new SoundFileInfo { HakPath = "/some/path.hak" };
        Assert.False(info.IsFromHak);
    }

    [Fact]
    public void IsFromBif_WithBifInfo_ReturnsTrue()
    {
        var info = new SoundFileInfo { BifInfo = new BifSoundInfo() };
        Assert.True(info.IsFromBif);
    }

    [Fact]
    public void IsFromBif_WithoutBifInfo_ReturnsFalse()
    {
        var info = new SoundFileInfo();
        Assert.False(info.IsFromBif);
    }

    [Fact]
    public void IsFromArchive_HakOrBif_ReturnsTrue()
    {
        var hakInfo = new SoundFileInfo
        {
            HakPath = "/path.hak",
            ErfEntry = new Radoub.Formats.Erf.ErfResourceEntry()
        };
        Assert.True(hakInfo.IsFromArchive);

        var bifInfo = new SoundFileInfo { BifInfo = new BifSoundInfo() };
        Assert.True(bifInfo.IsFromArchive);
    }

    [Fact]
    public void IsFromArchive_LooseFile_ReturnsFalse()
    {
        var info = new SoundFileInfo { FullPath = "/some/file.wav" };
        Assert.False(info.IsFromArchive);
    }
}
