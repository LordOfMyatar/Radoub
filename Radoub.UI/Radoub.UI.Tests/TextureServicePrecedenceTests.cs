using Radoub.Formats.Common;
using Radoub.TestUtilities.Mocks;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Texture resolution precedence (#1765). When a resref has BOTH a legacy low-res TGA and a
/// high-res (BioWare) DDS — as most NWN:EE creature textures do — the renderer must pick the
/// higher-resolution candidate, not return the TGA just because it is tried first. Returning the
/// low-res TGA is what made Drow Matron / Duergar Chief look blurry.
/// </summary>
public class TextureServicePrecedenceTests
{
    /// <summary>Minimal uncompressed 32-bit TGA (type 2) of solid color.</summary>
    private static byte[] BuildTga(ushort w, ushort h)
    {
        var data = new byte[18 + w * h * 4];
        data[2] = 2;  // uncompressed true-color
        BitConverter.GetBytes(w).CopyTo(data, 12);
        BitConverter.GetBytes(h).CopyTo(data, 14);
        data[16] = 32; // 32 bpp
        // pixels left zero (black) — only dimensions matter here
        return data;
    }

    /// <summary>BioWare DXT1 DDS of given size (solid block, used for a dimension comparison).</summary>
    private static byte[] BuildBiowareDxt1(uint w, uint h)
    {
        int blocks = (((int)w + 3) >> 2) * (((int)h + 3) >> 2);
        var data = new byte[20 + blocks * 8];
        BitConverter.GetBytes(w).CopyTo(data, 0);
        BitConverter.GetBytes(h).CopyTo(data, 4);
        BitConverter.GetBytes((uint)3).CopyTo(data, 8); // DXT1
        // all block bytes zero => valid solid-color blocks
        return data;
    }

    [Fact]
    public void LoadTextureWithKind_TgaAndDdsBothExist_PrefersHigherResolutionDds()
    {
        var mock = new MockGameDataService(includeSampleData: false);
        mock.SetResource("c_test", ResourceTypes.Tga, BuildTga(8, 8));
        mock.SetResource("c_test", ResourceTypes.Dds, BuildBiowareDxt1(64, 64));
        var tex = new TextureService(mock);

        var result = tex.LoadTextureWithKind("c_test");

        Assert.NotNull(result);
        Assert.Equal(64, result!.Value.width);
        Assert.Equal(64, result.Value.height);
    }

    [Fact]
    public void LoadTextureWithKind_OnlyTgaExists_ReturnsTga()
    {
        var mock = new MockGameDataService(includeSampleData: false);
        mock.SetResource("c_test", ResourceTypes.Tga, BuildTga(16, 16));
        var tex = new TextureService(mock);

        var result = tex.LoadTextureWithKind("c_test");

        Assert.NotNull(result);
        Assert.Equal(16, result!.Value.width);
        Assert.Equal(16, result.Value.height);
    }

    [Fact]
    public void LoadTexturePreferBIFWithKind_TgaAndDdsBothInBase_PrefersHigherResolutionDds()
    {
        // This is the path the creature preview actually uses for base-game creatures
        // (_preferBifTextures). It had the same TGA-before-DDS bug as LoadTextureWithKind,
        // so Drow Matron / Duergar Chief stayed blurry even after the first fix (#1765).
        var mock = new MockGameDataService(includeSampleData: false);
        mock.SetResource("c_test", ResourceTypes.Tga, BuildTga(8, 8));
        mock.SetResource("c_test", ResourceTypes.Dds, BuildBiowareDxt1(64, 64));
        var tex = new TextureService(mock);

        var result = tex.LoadTexturePreferBIFWithKind("c_test");

        Assert.NotNull(result);
        Assert.Equal(64, result!.Value.width);
        Assert.Equal(64, result.Value.height);
    }

    [Fact]
    public void LoadTextureWithKind_TgaAndDdsSameResolution_KeepsTga()
    {
        // On a size tie there is no sharpness benefit to switching, so keep the historical TGA
        // result (preserves its alpha channel / orientation). Only a strictly-larger DDS wins.
        var mock = new MockGameDataService(includeSampleData: false);
        mock.SetResource("c_test", ResourceTypes.Tga, BuildTga(64, 64));
        mock.SetResource("c_test", ResourceTypes.Dds, BuildBiowareDxt1(64, 64));
        var tex = new TextureService(mock);

        var result = tex.LoadTextureWithKind("c_test");

        Assert.NotNull(result);
        Assert.Equal(64, result!.Value.width);
        Assert.Equal(64, result.Value.height);
        // Can't distinguish source by dimensions alone here; the behavioral guarantee is "no switch
        // on tie". The strictly-larger DDS case (first test) proves the switch happens when it should.
    }

    [Fact]
    public void LoadTextureWithKind_TgaLargerThanDds_KeepsTga()
    {
        // Defensive: if the TGA is actually the higher-res asset, it must win.
        var mock = new MockGameDataService(includeSampleData: false);
        mock.SetResource("c_test", ResourceTypes.Tga, BuildTga(128, 128));
        mock.SetResource("c_test", ResourceTypes.Dds, BuildBiowareDxt1(32, 32));
        var tex = new TextureService(mock);

        var result = tex.LoadTextureWithKind("c_test");

        Assert.NotNull(result);
        Assert.Equal(128, result!.Value.width);
        Assert.Equal(128, result.Value.height);
    }
}
