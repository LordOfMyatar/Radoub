using Radoub.Formats.Utp;

namespace PlaceableEditor.Services;

/// <summary>
/// Game-safe default seeding and save-time backfill for placeables (#2417).
///
/// A bare <c>File → New</c> placeable previously kept every C# default (HP 0, Hardness 0,
/// saves 0). A non-plot, non-static placeable with max HP 0 makes the Aurora engine divide
/// by max-HP when computing damage/destruction ratios, crashing on load. These helpers seed
/// toolset-matching defaults on creation and clamp HP at save as a backstop.
///
/// Default values verified against real toolset-created UTPs (chest1, appletree, crateofapples):
/// HP/CurrentHP 15, Hardness 5, Fort 16. Pure (no UI) so it is unit-testable without FlaUI.
/// </summary>
public static class PlaceableDefaults
{
    public const short DefaultHp = 15;
    public const byte DefaultHardness = 5;
    public const byte DefaultFort = 16;

    /// <summary>
    /// Seed a freshly created placeable with toolset-matching combat/physical defaults.
    /// Leaves Appearance at 0 (placeables.2da row 0 is the valid "invisible object" model);
    /// the user picks a visible model in the editor. HP is the divide-by-zero culprit, not Appearance.
    /// </summary>
    public static void Seed(UtpFile utp)
    {
        utp.HP = DefaultHp;
        utp.CurrentHP = DefaultHp;
        utp.Hardness = DefaultHardness;
        utp.Fort = DefaultFort;
    }

    /// <summary>
    /// Backstop applied before save: a damageable (non-plot, non-static) placeable must never
    /// be written with HP 0. Plot and Static placeables run no combat math, so their HP is left
    /// untouched. Returns true if any field was changed.
    /// </summary>
    public static bool EnsureGameSafe(UtpFile utp)
    {
        bool damageable = !utp.Plot && !utp.Static;
        if (!damageable) return false;

        if (utp.HP <= 0)
        {
            utp.HP = DefaultHp;
            if (utp.CurrentHP <= 0) utp.CurrentHP = DefaultHp;
            return true;
        }

        return false;
    }
}
