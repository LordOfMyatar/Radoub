using System;
using System.Collections.Generic;
using System.IO;
using Radoub.Formats.Fac;
using Radoub.Formats.Logging;

namespace PlaceableEditor.Services;

/// <summary>
/// Loads the module's faction list (repute.fac) for the Behavior panel's Faction combo (#2354).
/// Mirrors Quartermaster's AppearanceService.GetAllFactions: real factions from the module when
/// available, the five standard NWN factions as a graceful fallback. No hardcoded faction data
/// when a repute.fac is present — the faction id is the list index, the engine convention.
/// </summary>
public static class FactionService
{
    /// <summary>
    /// Load (Id, Name) pairs from <paramref name="moduleDirectory"/>/repute.fac. Returns the
    /// standard NWN factions when the directory is null/missing, repute.fac is absent or unreadable,
    /// or the file lists no factions.
    /// </summary>
    public static List<(ushort Id, string Name)> Load(string? moduleDirectory)
    {
        if (!string.IsNullOrEmpty(moduleDirectory))
        {
            try
            {
                var facPath = Path.Combine(moduleDirectory, "repute.fac");
                if (File.Exists(facPath))
                {
                    var fac = FacReader.Read(facPath);
                    if (fac.FactionList.Count > 0)
                    {
                        var factions = new List<(ushort Id, string Name)>(fac.FactionList.Count);
                        for (int i = 0; i < fac.FactionList.Count; i++)
                        {
                            var name = fac.FactionList[i].FactionName;
                            factions.Add(((ushort)i,
                                string.IsNullOrEmpty(name) ? $"Faction {i}" : name));
                        }
                        return factions;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or FormatException)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Reliquary: could not load repute.fac: {ex.Message}. Using default factions.");
            }
        }

        return StandardFactions();
    }

    /// <summary>The five standard NWN factions (id = index), used when no module repute.fac applies.</summary>
    private static List<(ushort Id, string Name)> StandardFactions() => new()
    {
        (0, "PC"),
        (1, "Hostile"),
        (2, "Commoner"),
        (3, "Merchant"),
        (4, "Defender"),
    };
}
