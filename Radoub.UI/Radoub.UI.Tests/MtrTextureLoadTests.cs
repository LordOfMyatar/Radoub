using System.Text;
using Radoub.Formats.Common;
using Radoub.TestUtilities.Mocks;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// #2497 IO-level resolution: when a mesh names an MTR material, the diffuse must be
/// loaded from the MTR <c>texture0</c> even though the mesh bitmap itself has no texture
/// on disk (the white-model case). Falls back to the #1755 bare/<c>_d</c> chain otherwise.
/// </summary>
public class MtrTextureLoadTests
{
    // Minimal 1x1 32-bit uncompressed true-color TGA (18-byte header + one BGRA pixel).
    private static byte[] OnePixelTga(byte b, byte g, byte r, byte a)
    {
        var tga = new byte[18 + 4];
        tga[2] = 2;       // image type: uncompressed true-color
        tga[12] = 1;      // width = 1
        tga[14] = 1;      // height = 1
        tga[16] = 32;     // 32 bpp
        tga[18] = b; tga[19] = g; tga[20] = r; tga[21] = a;
        return tga;
    }

    private static byte[] Mtr(string text) => Encoding.ASCII.GetBytes(text);

    [Fact]
    public void DivergentMtr_LoadsDiffuseFromTexture0_WhenBitmapMissing()
    {
        var game = new MockGameDataService(includeSampleData: false);
        // The mesh bitmap "c_mesh_bitmap" has NO texture on disk (white-model trigger).
        // Its material file points at a different diffuse via texture0.
        game.SetResource("c_mesh_bitmap", ResourceTypes.Mtr, Mtr("texture0 c_real_diffuse\n"));
        game.SetResource("c_real_diffuse", ResourceTypes.Tga, OnePixelTga(10, 20, 30, 255));

        var service = new TextureService(game);

        var result = service.LoadTextureWithKind("c_mesh_bitmap", "c_mesh_bitmap");

        Assert.NotNull(result);
        Assert.Equal(1, result!.Value.width);
        Assert.Equal(1, result.Value.height);
    }

    [Fact]
    public void NoMtr_StillResolvesBareName_1755Unbroken()
    {
        var game = new MockGameDataService(includeSampleData: false);
        game.SetResource("c_zod_boar", ResourceTypes.Tga, OnePixelTga(1, 2, 3, 255));

        var service = new TextureService(game);

        // No materialName supplied — must behave exactly like the existing loader.
        var result = service.LoadTextureWithKind("c_zod_boar");

        Assert.NotNull(result);
    }

    [Fact]
    public void MtrPresentButMissingTexture0_FallsBackToDSuffix()
    {
        var game = new MockGameDataService(includeSampleData: false);
        game.SetResource("cre_017_t_b01", ResourceTypes.Mtr, Mtr("texture0 null\ntexture1 cre_017_t_b01_n\n"));
        // Only the #1755 _d diffuse exists on disk.
        game.SetResource("cre_017_t_b01_d", ResourceTypes.Tga, OnePixelTga(9, 9, 9, 255));

        var service = new TextureService(game);

        var result = service.LoadTextureWithKind("cre_017_t_b01", "cre_017_t_b01");

        Assert.NotNull(result);
    }
}
