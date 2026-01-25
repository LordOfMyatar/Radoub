namespace Radoub.Formats.Ifo;

/// <summary>
/// Defines minimum game version requirements for IFO fields.
/// Used for backward compatibility validation when editing modules.
/// </summary>
public static class IfoVersionRequirements
{
    /// <summary>
    /// Base NWN 1.69 (Diamond Edition) - universal compatibility.
    /// </summary>
    public const string Version169 = "1.69";

    /// <summary>
    /// NWN:EE initial release.
    /// </summary>
    public const string Version174 = "1.74";

    /// <summary>
    /// NWSync support added.
    /// </summary>
    public const string Version177 = "1.77";

    /// <summary>
    /// Targeting mode scripts added.
    /// </summary>
    public const string Version180 = "1.80";

    /// <summary>
    /// GUI and tile action events added.
    /// </summary>
    public const string Version185 = "1.85";

    /// <summary>
    /// DefaultBic and PartyControl added.
    /// </summary>
    public const string Version187 = "1.87";

    /// <summary>
    /// Maps IFO GFF field names to minimum required game version.
    /// Only includes fields that require versions higher than 1.69.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> FieldMinVersions = new Dictionary<string, string>
    {
        // NWN:EE 1.74 - Initial EE release
        { "Mod_OnModStart", Version174 },
        { "Mod_OnPlrChat", Version174 },

        // NWN:EE 1.77 - NWSync
        { "Mod_UUID", Version177 },

        // NWN:EE 1.80 - Targeting mode
        { "Mod_OnPlrTarget", Version180 },

        // NWN:EE 1.85 - GUI/Tile events and NUI
        { "Mod_OnPlrGuiEvt", Version185 },
        { "Mod_OnPlrTileAct", Version185 },
        { "Mod_OnNuiEvent", Version185 },

        // NWN:EE 1.87 - DefaultBic and PartyControl
        { "Mod_DefaultBic", Version187 },
        { "Mod_PartyControl", Version187 },
    };

    /// <summary>
    /// Maps IfoFile property names to their GFF field names.
    /// Used for user-friendly display in warnings.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> PropertyToFieldName = new Dictionary<string, string>
    {
        { nameof(IfoFile.OnModuleStart), "Mod_OnModStart" },
        { nameof(IfoFile.OnPlayerChat), "Mod_OnPlrChat" },
        { nameof(IfoFile.ModuleUuid), "Mod_UUID" },
        { nameof(IfoFile.OnPlayerTarget), "Mod_OnPlrTarget" },
        { nameof(IfoFile.OnPlayerGuiEvent), "Mod_OnPlrGuiEvt" },
        { nameof(IfoFile.OnPlayerTileAction), "Mod_OnPlrTileAct" },
        { nameof(IfoFile.OnNuiEvent), "Mod_OnNuiEvent" },
        { nameof(IfoFile.DefaultBic), "Mod_DefaultBic" },
        { nameof(IfoFile.PartyControl), "Mod_PartyControl" },
    };

    /// <summary>
    /// User-friendly display names for EE-only fields.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> FieldDisplayNames = new Dictionary<string, string>
    {
        { "Mod_OnModStart", "OnModuleStart script" },
        { "Mod_OnPlrChat", "OnPlayerChat script" },
        { "Mod_UUID", "Module UUID" },
        { "Mod_OnPlrTarget", "OnPlayerTarget script" },
        { "Mod_OnPlrGuiEvt", "OnPlayerGuiEvent script" },
        { "Mod_OnPlrTileAct", "OnPlayerTileAction script" },
        { "Mod_OnNuiEvent", "OnNuiEvent script" },
        { "Mod_DefaultBic", "Default character (BIC)" },
        { "Mod_PartyControl", "Party control mode" },
    };

    /// <summary>
    /// Gets information about EE-only fields that have values in the given IFO file.
    /// </summary>
    /// <param name="ifo">The IFO file to check.</param>
    /// <returns>List of (fieldName, displayName, minVersion, currentValue) for fields with values.</returns>
    public static List<(string FieldName, string DisplayName, string MinVersion, string Value)> GetPopulatedEeFields(IfoFile ifo)
    {
        var result = new List<(string, string, string, string)>();

        // Check each EE-only field
        if (!string.IsNullOrEmpty(ifo.OnModuleStart))
            result.Add(("Mod_OnModStart", "OnModuleStart script", Version174, ifo.OnModuleStart));

        if (!string.IsNullOrEmpty(ifo.OnPlayerChat))
            result.Add(("Mod_OnPlrChat", "OnPlayerChat script", Version174, ifo.OnPlayerChat));

        if (!string.IsNullOrEmpty(ifo.ModuleUuid))
            result.Add(("Mod_UUID", "Module UUID", Version177, ifo.ModuleUuid));

        if (!string.IsNullOrEmpty(ifo.OnPlayerTarget))
            result.Add(("Mod_OnPlrTarget", "OnPlayerTarget script", Version180, ifo.OnPlayerTarget));

        if (!string.IsNullOrEmpty(ifo.OnPlayerGuiEvent))
            result.Add(("Mod_OnPlrGuiEvt", "OnPlayerGuiEvent script", Version185, ifo.OnPlayerGuiEvent));

        if (!string.IsNullOrEmpty(ifo.OnPlayerTileAction))
            result.Add(("Mod_OnPlrTileAct", "OnPlayerTileAction script", Version185, ifo.OnPlayerTileAction));

        if (!string.IsNullOrEmpty(ifo.OnNuiEvent))
            result.Add(("Mod_OnNuiEvent", "OnNuiEvent script", Version185, ifo.OnNuiEvent));

        if (!string.IsNullOrEmpty(ifo.DefaultBic))
            result.Add(("Mod_DefaultBic", "Default character (BIC)", Version187, ifo.DefaultBic));

        // PartyControl: 0 is default, only flag if explicitly set to non-zero
        if (ifo.PartyControl != 0)
            result.Add(("Mod_PartyControl", "Party control mode", Version187, ifo.PartyControl.ToString()));

        return result;
    }

    /// <summary>
    /// Gets fields that would be incompatible with a target version.
    /// </summary>
    /// <param name="ifo">The IFO file to check.</param>
    /// <param name="targetVersion">The target minimum game version.</param>
    /// <returns>Fields that require a higher version than the target.</returns>
    public static List<(string FieldName, string DisplayName, string MinVersion, string Value)> GetIncompatibleFields(
        IfoFile ifo, string targetVersion)
    {
        var populated = GetPopulatedEeFields(ifo);
        return populated
            .Where(f => GameVersionComparer.Instance.Compare(f.MinVersion, targetVersion) > 0)
            .ToList();
    }

    /// <summary>
    /// Gets the highest version required by any populated field in the IFO.
    /// </summary>
    /// <param name="ifo">The IFO file to check.</param>
    /// <returns>The highest required version, or "1.69" if no EE fields are populated.</returns>
    public static string GetRequiredVersion(IfoFile ifo)
    {
        var populated = GetPopulatedEeFields(ifo);
        if (populated.Count == 0)
            return Version169;

        return populated
            .Select(f => f.MinVersion)
            .OrderByDescending(v => v, GameVersionComparer.Instance)
            .First();
    }
}

/// <summary>
/// Compares NWN game version strings (e.g., "1.69", "1.87.8193.35").
/// </summary>
public class GameVersionComparer : IComparer<string>
{
    /// <summary>
    /// Singleton instance for use in LINQ operations.
    /// </summary>
    public static readonly GameVersionComparer Instance = new();

    /// <summary>
    /// Compares two version strings.
    /// </summary>
    /// <returns>
    /// Less than 0 if x &lt; y, 0 if equal, greater than 0 if x &gt; y.
    /// </returns>
    public int Compare(string? x, string? y)
    {
        if (x == y) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        var partsX = x.Split('.');
        var partsY = y.Split('.');

        var maxParts = Math.Max(partsX.Length, partsY.Length);
        for (int i = 0; i < maxParts; i++)
        {
            var partX = i < partsX.Length && int.TryParse(partsX[i], out var px) ? px : 0;
            var partY = i < partsY.Length && int.TryParse(partsY[i], out var py) ? py : 0;

            if (partX != partY)
                return partX.CompareTo(partY);
        }

        return 0;
    }

    /// <summary>
    /// Checks if a module version supports a required version.
    /// </summary>
    /// <param name="moduleVersion">The module's MinGameVersion.</param>
    /// <param name="requiredVersion">The version required by a field.</param>
    /// <returns>True if moduleVersion >= requiredVersion.</returns>
    public static bool Supports(string moduleVersion, string requiredVersion)
    {
        return Instance.Compare(moduleVersion, requiredVersion) >= 0;
    }
}
