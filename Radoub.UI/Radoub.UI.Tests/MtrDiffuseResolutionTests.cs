using System.Collections.Generic;
using Radoub.Formats.Mtr;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// #2497: resolve a mesh's diffuse texture from its MTR <c>texture0</c> instead of
/// guessing the <c>_d</c> suffix — the remaining half of the #1755 white-model fix.
/// The decision logic (which diffuse resrefs to try, in order) is pure and unit-tested
/// here; the IO that loads the bytes lives in <see cref="TextureService"/>.
/// </summary>
public class MtrDiffuseResolutionTests
{
    [Fact]
    public void DivergentMtr_DiffuseFromTexture0_LeadsCandidates()
    {
        // The white-model trigger: a CEP3-shaped MTR whose texture0 differs from the
        // mesh bitmap. Bare-name resolution would never find the real diffuse.
        var mtr = MtrReader.Parse("texture0 c_real_diffuse\n");

        var candidates = TextureService.ResolveDiffuseCandidates("c_mesh_bitmap", mtr);

        Assert.Equal("c_real_diffuse", candidates[0]);
    }

    [Fact]
    public void DivergentMtr_StillFallsBackToBareNameAndDSuffix()
    {
        // MTR texture0 leads, but if it misses on disk we must still try the bare name
        // and the #1755 _d variant — defense in depth, not a hard override.
        var mtr = MtrReader.Parse("texture0 c_real_diffuse\n");

        var candidates = TextureService.ResolveDiffuseCandidates("c_mesh_bitmap", mtr);

        Assert.Contains("c_mesh_bitmap", candidates);
        Assert.Contains("c_mesh_bitmap_d", candidates);
    }

    [Fact]
    public void NoMtr_PreservesBareNameThenDSuffix_1755Behavior()
    {
        var candidates = TextureService.ResolveDiffuseCandidates("cre_017_t_b01", mtr: null);

        Assert.Equal(new[] { "cre_017_t_b01", "cre_017_t_b01_d" }, candidates);
    }

    [Fact]
    public void MtrWithNullTexture0_FallsBackToBareNameChain()
    {
        // An MTR that declares no usable texture0 must not blank out the bare-name chain.
        var mtr = MtrReader.Parse("texture0 null\ntexture1 c_normalmap\n");

        var candidates = TextureService.ResolveDiffuseCandidates("cre_017_t_b01", mtr);

        Assert.Equal(new[] { "cre_017_t_b01", "cre_017_t_b01_d" }, candidates);
    }

    [Fact]
    public void Texture0EqualsBitmap_NoDuplicateCandidate()
    {
        // The Zodiac case: texture0 == bitmap. The bare name already resolves; we must
        // not emit it twice.
        var mtr = MtrReader.Parse("texture0 c_zod_boar\n");

        var candidates = TextureService.ResolveDiffuseCandidates("c_zod_boar", mtr);

        Assert.Equal(new[] { "c_zod_boar", "c_zod_boar_d" }, candidates);
    }
}
