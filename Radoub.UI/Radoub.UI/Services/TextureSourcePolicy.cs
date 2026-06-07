using Radoub.Formats.Resolver;

namespace Radoub.UI.Services;

/// <summary>
/// Decides whether the model preview should prefer base-game (BIF) textures, based on
/// where the model itself was resolved from (#1758).
///
/// Base-game and Override models keep the #1867 BIF-prefer behavior so a base creature
/// does not pick up an incompatible CEP texture (e.g. reversed bat wings). HAK and Module
/// models use the normal HAK-first chain so CEP-only creature textures resolve from their
/// own pack instead of a stale base-game stub (the c_fairy 32x32 case).
/// </summary>
public static class TextureSourcePolicy
{
    public static bool PreferBif(ResourceSource modelSource) =>
        modelSource is ResourceSource.Bif or ResourceSource.Override;
}
