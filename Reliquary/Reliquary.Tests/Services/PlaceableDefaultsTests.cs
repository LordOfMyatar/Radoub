using Radoub.Formats.Utp;
using PlaceableEditor.Services;

namespace PlaceableEditor.Tests.Services;

/// <summary>
/// Tests for game-safe default seeding and save-time backfill (#2417).
/// A bare File → New placeable must round-trip into Aurora without a divide-by-zero:
/// the textbook cause is HP = 0 on a damageable (non-plot, non-static) placeable, where
/// the engine divides by max-HP when computing damage/destruction ratios.
/// </summary>
public class PlaceableDefaultsTests
{
    // --- Seed (NewPlaceable) ---

    [Fact]
    public void Seed_SetsToolsetMatchingDefaults()
    {
        var utp = new UtpFile();

        PlaceableDefaults.Seed(utp);

        // Values verified against real toolset-created UTPs (chest1/appletree/crateofapples).
        Assert.Equal((short)15, utp.HP);
        Assert.Equal((short)15, utp.CurrentHP);
        Assert.Equal((byte)5, utp.Hardness);
        Assert.Equal((byte)16, utp.Fort);
    }

    // --- Backfill (save guard) ---

    [Fact]
    public void EnsureGameSafe_DamageablePlaceableWithZeroHp_GetsMinimumHp()
    {
        var utp = new UtpFile { HP = 0, CurrentHP = 0, Plot = false, Static = false };

        var changed = PlaceableDefaults.EnsureGameSafe(utp);

        Assert.True(changed);
        Assert.True(utp.HP > 0);
        Assert.True(utp.CurrentHP > 0);
    }

    [Fact]
    public void EnsureGameSafe_PlotPlaceableWithZeroHp_LeftAlone()
    {
        // Plot placeables cannot be damaged/destroyed, so HP is never the divisor — don't touch it.
        var utp = new UtpFile { HP = 0, CurrentHP = 0, Plot = true, Static = false };

        var changed = PlaceableDefaults.EnsureGameSafe(utp);

        Assert.False(changed);
        Assert.Equal((short)0, utp.HP);
    }

    [Fact]
    public void EnsureGameSafe_StaticPlaceableWithZeroHp_LeftAlone()
    {
        // Static placeables are baked into geometry; no combat math runs on them.
        var utp = new UtpFile { HP = 0, CurrentHP = 0, Plot = false, Static = true };

        var changed = PlaceableDefaults.EnsureGameSafe(utp);

        Assert.False(changed);
        Assert.Equal((short)0, utp.HP);
    }

    [Fact]
    public void EnsureGameSafe_DamageableWithValidHp_Unchanged()
    {
        var utp = new UtpFile { HP = 15, CurrentHP = 15, Plot = false, Static = false };

        var changed = PlaceableDefaults.EnsureGameSafe(utp);

        Assert.False(changed);
        Assert.Equal((short)15, utp.HP);
    }
}
