using System.Collections.Generic;
using System.IO;
using Radoub.Formats.Services;

namespace Quartermaster.Services;

/// <summary>
/// Provides appearance, phenotype, portrait, wing, tail, sound set, and faction lookups.
/// Uses 2DA, TLK, and FAC files for game data resolution.
/// </summary>
public class AppearanceService
{
    private readonly IGameDataService _gameDataService;

    public AppearanceService(IGameDataService gameDataService)
    {
        _gameDataService = gameDataService;
    }

    #region Appearance

    /// <summary>
    /// Gets the display name for an appearance type ID.
    /// </summary>
    public string GetAppearanceName(ushort appearanceId)
    {
        // Try 2DA/TLK lookup first
        var strRef = _gameDataService.Get2DAValue("appearance", appearanceId, "STRING_REF");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****")
        {
            var tlkName = _gameDataService.GetString(strRef);
            if (!string.IsNullOrEmpty(tlkName))
                return tlkName;
        }

        // Fallback to LABEL column
        var label = _gameDataService.Get2DAValue("appearance", appearanceId, "LABEL");
        if (!string.IsNullOrEmpty(label) && label != "****")
            return label;

        return $"Appearance {appearanceId}";
    }

    /// <summary>
    /// Checks if an appearance type is part-based (dynamic).
    /// Returns true if MODELTYPE is "P" in appearance.2da.
    /// </summary>
    public bool IsPartBasedAppearance(ushort appearanceId)
    {
        var modelType = _gameDataService.Get2DAValue("appearance", appearanceId, "MODELTYPE");
        return modelType?.ToUpperInvariant() == "P";
    }

    /// <summary>
    /// Gets the size AC modifier for a creature based on its appearance.
    /// D&D 3e size categories: Tiny +2, Small +1, Medium 0, Large -1, Huge -2
    /// </summary>
    public int GetSizeAcModifier(ushort appearanceId)
    {
        var sizeCategoryStr = _gameDataService.Get2DAValue("appearance", appearanceId, "SIZECATEGORY");
        if (string.IsNullOrEmpty(sizeCategoryStr) || sizeCategoryStr == "****")
            return 0;

        if (!int.TryParse(sizeCategoryStr, out int sizeCategory))
            return 0;

        // NWN creaturesize.2da indices
        return sizeCategory switch
        {
            1 => 2,   // Tiny
            2 => 1,   // Small
            3 => 0,   // Medium
            4 => -1,  // Large
            5 => -2,  // Huge
            6 => -4,  // Gargantuan
            7 => -8,  // Colossal
            _ => 0
        };
    }

    /// <summary>
    /// Gets all appearance IDs from appearance.2da.
    /// </summary>
    public List<AppearanceInfo> GetAllAppearances()
    {
        var appearances = new List<AppearanceInfo>();

        for (int i = 0; i < 1000; i++)
        {
            var label = _gameDataService.Get2DAValue("appearance", i, "LABEL");
            if (string.IsNullOrEmpty(label) || label == "****")
            {
                if (appearances.Count > 100)
                    break;
                continue;
            }

            var modelType = _gameDataService.Get2DAValue("appearance", i, "MODELTYPE");
            var isPartBased = modelType?.ToUpperInvariant() == "P";

            appearances.Add(new AppearanceInfo
            {
                AppearanceId = (ushort)i,
                Name = GetAppearanceName((ushort)i),
                Label = label,
                IsPartBased = isPartBased
            });
        }

        return appearances;
    }

    #endregion

    #region Phenotype

    /// <summary>
    /// Gets the display name for a phenotype.
    /// </summary>
    public string GetPhenotypeName(int phenotype)
    {
        var strRef = _gameDataService.Get2DAValue("phenotype", phenotype, "Name");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****")
        {
            var tlkName = _gameDataService.GetString(strRef);
            if (!string.IsNullOrEmpty(tlkName))
                return tlkName;
        }

        return phenotype switch
        {
            0 => "Normal",
            2 => "Large",
            _ => $"Phenotype {phenotype}"
        };
    }

    /// <summary>
    /// Gets all phenotypes from phenotype.2da.
    /// </summary>
    public List<PhenotypeInfo> GetAllPhenotypes()
    {
        var phenotypes = new List<PhenotypeInfo>();

        for (int i = 0; i < 20; i++)
        {
            var label = _gameDataService.Get2DAValue("phenotype", i, "Label");
            if (string.IsNullOrEmpty(label) || label == "****")
            {
                if (phenotypes.Count > 0)
                    break;
                continue;
            }

            phenotypes.Add(new PhenotypeInfo
            {
                PhenotypeId = i,
                Name = GetPhenotypeName(i),
                Label = label
            });
        }

        // If no 2DA data, return hardcoded defaults
        if (phenotypes.Count == 0)
        {
            phenotypes.Add(new PhenotypeInfo { PhenotypeId = 0, Name = "Normal", Label = "Normal" });
            phenotypes.Add(new PhenotypeInfo { PhenotypeId = 2, Name = "Large", Label = "Large" });
        }

        return phenotypes;
    }

    #endregion

    #region Portrait

    /// <summary>
    /// Gets the display name for a portrait ID.
    /// </summary>
    public string GetPortraitName(ushort portraitId)
    {
        var baseResRef = _gameDataService.Get2DAValue("portraits", portraitId, "BaseResRef");
        if (!string.IsNullOrEmpty(baseResRef) && baseResRef != "****")
            return baseResRef;

        return $"Portrait {portraitId}";
    }

    /// <summary>
    /// Gets the portrait resref for loading the image.
    /// </summary>
    public string? GetPortraitResRef(ushort portraitId)
    {
        var baseResRef = _gameDataService.Get2DAValue("portraits", portraitId, "BaseResRef");
        if (!string.IsNullOrEmpty(baseResRef) && baseResRef != "****")
            return baseResRef;
        return null;
    }

    /// <summary>
    /// Gets all portraits from portraits.2da.
    /// </summary>
    public List<(ushort Id, string Name)> GetAllPortraits()
    {
        var portraits = new List<(ushort Id, string Name)>();

        for (int i = 0; i < 500; i++)
        {
            var baseResRef = _gameDataService.Get2DAValue("portraits", i, "BaseResRef");
            if (string.IsNullOrEmpty(baseResRef) || baseResRef == "****")
            {
                if (portraits.Count > 50)
                    break;
                continue;
            }

            portraits.Add(((ushort)i, baseResRef));
        }

        return portraits;
    }

    /// <summary>
    /// Finds a portrait ID by its BaseResRef string.
    /// Used when PortraitId is 0 but Portrait string field is set (common in BIC files).
    /// </summary>
    /// <param name="resRef">The portrait ResRef to find (e.g., "hu_m_99_")</param>
    /// <returns>Portrait ID if found, null otherwise</returns>
    public ushort? FindPortraitIdByResRef(string? resRef)
    {
        if (string.IsNullOrEmpty(resRef))
            return null;

        // Normalize for comparison (portraits.2da uses lowercase)
        var normalizedResRef = resRef.ToLowerInvariant();

        // BIC files store portrait with "po_" prefix (e.g., "po_hu_m_01_")
        // portraits.2da stores BaseResRef WITHOUT the prefix (e.g., "hu_m_01_")
        // Strip the "po_" prefix if present for matching
        if (normalizedResRef.StartsWith("po_"))
            normalizedResRef = normalizedResRef.Substring(3);

        for (int i = 0; i < 500; i++)
        {
            var baseResRef = _gameDataService.Get2DAValue("portraits", i, "BaseResRef");
            if (string.IsNullOrEmpty(baseResRef) || baseResRef == "****")
                continue;

            if (baseResRef.Equals(normalizedResRef, System.StringComparison.OrdinalIgnoreCase))
                return (ushort)i;
        }

        return null;
    }

    #endregion

    #region Wings and Tails

    /// <summary>
    /// Gets the display name for a wing type.
    /// </summary>
    public string GetWingName(byte wingId)
    {
        if (wingId == 0)
            return "None";

        var label = _gameDataService.Get2DAValue("wingmodel", wingId, "LABEL");
        if (!string.IsNullOrEmpty(label) && label != "****")
            return label;

        return $"Wings {wingId}";
    }

    /// <summary>
    /// Gets the display name for a tail type.
    /// </summary>
    public string GetTailName(byte tailId)
    {
        if (tailId == 0)
            return "None";

        var label = _gameDataService.Get2DAValue("tailmodel", tailId, "LABEL");
        if (!string.IsNullOrEmpty(label) && label != "****")
            return label;

        return $"Tail {tailId}";
    }

    /// <summary>
    /// Gets all wing types from wingmodel.2da.
    /// </summary>
    public List<(byte Id, string Name)> GetAllWings()
    {
        var wings = new List<(byte Id, string Name)> { (0, "None") };

        for (int i = 1; i < 50; i++)
        {
            var label = _gameDataService.Get2DAValue("wingmodel", i, "LABEL");
            if (string.IsNullOrEmpty(label) || label == "****")
            {
                if (wings.Count > 5)
                    break;
                continue;
            }

            wings.Add(((byte)i, label));
        }

        return wings;
    }

    /// <summary>
    /// Gets all tail types from tailmodel.2da.
    /// </summary>
    public List<(byte Id, string Name)> GetAllTails()
    {
        var tails = new List<(byte Id, string Name)> { (0, "None") };

        for (int i = 1; i < 50; i++)
        {
            var label = _gameDataService.Get2DAValue("tailmodel", i, "LABEL");
            if (string.IsNullOrEmpty(label) || label == "****")
            {
                if (tails.Count > 5)
                    break;
                continue;
            }

            tails.Add(((byte)i, label));
        }

        return tails;
    }

    #endregion

    #region Sound Sets

    /// <summary>
    /// Gets the display name for a sound set ID.
    /// </summary>
    public string GetSoundSetName(ushort soundSetId)
    {
        // Try STRREF for localized name first
        var strRef = _gameDataService.Get2DAValue("soundset", soundSetId, "STRREF");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****")
        {
            var tlkName = _gameDataService.GetString(strRef);
            if (!string.IsNullOrEmpty(tlkName))
                return tlkName;
        }

        // Fall back to LABEL column
        var label = _gameDataService.Get2DAValue("soundset", soundSetId, "LABEL");
        if (!string.IsNullOrEmpty(label) && label != "****")
            return label;

        return $"Sound Set {soundSetId}";
    }

    /// <summary>
    /// Gets all sound sets from soundset.2da.
    /// </summary>
    public List<(ushort Id, string Name)> GetAllSoundSets()
    {
        var soundSets = new List<(ushort Id, string Name)>();

        for (int i = 0; i < 500; i++)
        {
            var label = _gameDataService.Get2DAValue("soundset", i, "LABEL");
            if (string.IsNullOrEmpty(label) || label == "****")
            {
                if (soundSets.Count > 50)
                    break;
                continue;
            }

            var displayName = GetSoundSetName((ushort)i);
            soundSets.Add(((ushort)i, displayName));
        }

        return soundSets;
    }

    #endregion

    #region Factions

    /// <summary>
    /// Gets all factions from repute.fac or returns default NWN factions.
    /// </summary>
    /// <param name="moduleDirectory">Optional path to the module directory containing repute.fac</param>
    public List<(ushort Id, string Name)> GetAllFactions(string? moduleDirectory = null)
    {
        // Try to load factions from repute.fac in the module directory
        if (!string.IsNullOrEmpty(moduleDirectory))
        {
            try
            {
                var facPath = Path.Combine(moduleDirectory, "repute.fac");
                if (File.Exists(facPath))
                {
                    var facFile = Radoub.Formats.Fac.FacReader.Read(facPath);
                    if (facFile.FactionList.Count > 0)
                    {
                        var factions = new List<(ushort Id, string Name)>();
                        for (int i = 0; i < facFile.FactionList.Count; i++)
                        {
                            var faction = facFile.FactionList[i];
                            var displayName = string.IsNullOrEmpty(faction.FactionName)
                                ? $"Faction {i}"
                                : faction.FactionName;
                            factions.Add(((ushort)i, displayName));
                        }
                        return factions;
                    }
                }
            }
            catch
            {
                // Fall through to defaults if parsing fails
            }
        }

        // Standard NWN factions (fallback when repute.fac unavailable)
        return new List<(ushort Id, string Name)>
        {
            (0, "PC"),
            (1, "Hostile"),
            (2, "Commoner"),
            (3, "Merchant"),
            (4, "Defender")
        };
    }

    #endregion

    #region Packages

    /// <summary>
    /// Gets the display name for a package (auto-levelup preset) ID.
    /// </summary>
    public string GetPackageName(byte packageId)
    {
        var strRef = _gameDataService.Get2DAValue("packages", packageId, "Name");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****")
        {
            var tlkName = _gameDataService.GetString(strRef);
            if (!string.IsNullOrEmpty(tlkName))
                return tlkName;
        }

        var label = _gameDataService.Get2DAValue("packages", packageId, "Label");
        if (!string.IsNullOrEmpty(label) && label != "****")
            return label;

        return $"Package {packageId}";
    }

    /// <summary>
    /// Gets all packages from packages.2da.
    /// </summary>
    public List<(byte Id, string Name)> GetAllPackages()
    {
        var packages = new List<(byte Id, string Name)>();

        for (int i = 0; i < 256; i++)
        {
            var label = _gameDataService.Get2DAValue("packages", i, "Label");
            if (string.IsNullOrEmpty(label) || label == "****")
            {
                if (packages.Count > 20 && i > 50)
                    break;
                continue;
            }

            var name = GetPackageName((byte)i);
            packages.Add(((byte)i, name));
        }

        packages.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase));
        return packages;
    }

    #endregion
}

/// <summary>
/// Appearance information from appearance.2da.
/// </summary>
public class AppearanceInfo
{
    public ushort AppearanceId { get; set; }
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public bool IsPartBased { get; set; }
}

/// <summary>
/// Phenotype information from phenotype.2da.
/// </summary>
public class PhenotypeInfo
{
    public int PhenotypeId { get; set; }
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
}
