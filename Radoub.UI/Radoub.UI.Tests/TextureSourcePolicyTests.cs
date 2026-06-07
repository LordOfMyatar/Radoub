using Radoub.Formats.Resolver;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for TextureSourcePolicy — chooses whether the model preview should prefer
/// base-game (BIF) textures based on where the model itself was resolved (#1758).
/// Base/Override models keep the #1867 BIF-prefer behavior (avoids reversed CEP
/// textures); HAK/Module models use the normal HAK-first chain so CEP-only creature
/// textures (e.g. c_fairy) resolve correctly instead of a stale BIF stub.
/// </summary>
public class TextureSourcePolicyTests
{
    [Fact]
    public void PreferBif_BifSource_ReturnsTrue()
        => Assert.True(TextureSourcePolicy.PreferBif(ResourceSource.Bif));

    [Fact]
    public void PreferBif_OverrideSource_ReturnsTrue()
        => Assert.True(TextureSourcePolicy.PreferBif(ResourceSource.Override));

    [Fact]
    public void PreferBif_HakSource_ReturnsFalse() // #1758 c_fairy case
        => Assert.False(TextureSourcePolicy.PreferBif(ResourceSource.Hak));

    [Fact]
    public void PreferBif_ModuleSource_ReturnsFalse()
        => Assert.False(TextureSourcePolicy.PreferBif(ResourceSource.Module));
}
