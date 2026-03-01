using System.Collections.Generic;
using Radoub.Formats.Services;

namespace Quartermaster.Services;

/// <summary>
/// Provides domain information from domains.2da — spell lists, granted feats, and descriptions.
/// Used to display what the NWN engine auto-grants for cleric domain selections.
/// </summary>
public class DomainService
{
    private readonly IGameDataService _gameDataService;
    private readonly SpellService _spellService;
    private readonly Dictionary<int, DomainInfo> _cache = new();

    public DomainService(IGameDataService gameDataService, SpellService spellService)
    {
        _gameDataService = gameDataService;
        _spellService = spellService;
    }

    /// <summary>
    /// Gets full domain info including spells and granted feat.
    /// </summary>
    public DomainInfo? GetDomainInfo(int domainId)
    {
        if (_cache.TryGetValue(domainId, out var cached))
            return cached;

        var label = _gameDataService.Get2DAValue("domains", domainId, "Label");
        if (string.IsNullOrEmpty(label) || label == "****")
            return null;

        var info = new DomainInfo { DomainId = domainId, Label = label };

        // Display name from TLK
        var nameStrRef = _gameDataService.Get2DAValue("domains", domainId, "Name");
        if (!string.IsNullOrEmpty(nameStrRef) && nameStrRef != "****")
        {
            var tlkName = _gameDataService.GetString(nameStrRef);
            info.Name = !string.IsNullOrEmpty(tlkName) ? tlkName : label;
        }
        else
        {
            info.Name = label;
        }

        // Description from TLK
        var descStrRef = _gameDataService.Get2DAValue("domains", domainId, "Description");
        if (!string.IsNullOrEmpty(descStrRef) && descStrRef != "****")
        {
            info.Description = _gameDataService.GetString(descStrRef) ?? "";
        }

        // Domain spells: Level_1 through Level_9 columns contain spell IDs
        for (int level = 1; level <= 9; level++)
        {
            var spellIdStr = _gameDataService.Get2DAValue("domains", domainId, $"Level_{level}");
            if (!string.IsNullOrEmpty(spellIdStr) && spellIdStr != "****" && int.TryParse(spellIdStr, out int spellId))
            {
                var spellName = _spellService.GetSpellName(spellId);
                info.DomainSpells.Add(new DomainSpell { Level = level, SpellId = spellId, Name = spellName });
            }
        }

        // Granted feat (e.g., Sun domain → Exceptional Turning)
        var grantedFeatStr = _gameDataService.Get2DAValue("domains", domainId, "GrantedFeat");
        if (!string.IsNullOrEmpty(grantedFeatStr) && grantedFeatStr != "****" && int.TryParse(grantedFeatStr, out int featId))
        {
            info.GrantedFeatId = featId;
            var featNameStrRef = _gameDataService.Get2DAValue("feat", featId, "FEAT");
            if (!string.IsNullOrEmpty(featNameStrRef) && featNameStrRef != "****")
            {
                info.GrantedFeatName = _gameDataService.GetString(featNameStrRef) ?? $"Feat {featId}";
            }
            else
            {
                info.GrantedFeatName = $"Feat {featId}";
            }
        }

        _cache[domainId] = info;
        return info;
    }

    /// <summary>
    /// Gets all valid domains from domains.2da as (Id, Name) tuples for dropdown population.
    /// </summary>
    public List<(int Id, string Name)> GetAllDomains()
    {
        var domains = new List<(int Id, string Name)>();

        for (int row = 0; row < 50; row++)
        {
            var label = _gameDataService.Get2DAValue("domains", row, "Label");
            if (string.IsNullOrEmpty(label) || label == "****")
                continue;

            var nameStrRef = _gameDataService.Get2DAValue("domains", row, "Name");
            var name = label;
            if (!string.IsNullOrEmpty(nameStrRef) && nameStrRef != "****")
            {
                var tlkName = _gameDataService.GetString(nameStrRef);
                if (!string.IsNullOrEmpty(tlkName))
                    name = tlkName;
            }

            domains.Add((row, name));
        }

        return domains;
    }

    /// <summary>
    /// Formats a domain's granted content as a display string.
    /// </summary>
    public static string FormatDomainSummary(DomainInfo domain)
    {
        var lines = new List<string>();

        if (domain.GrantedFeatId >= 0)
            lines.Add($"Granted Feat: {domain.GrantedFeatName}");

        if (domain.DomainSpells.Count > 0)
        {
            lines.Add("Domain Spells:");
            foreach (var spell in domain.DomainSpells)
                lines.Add($"  Level {spell.Level}: {spell.Name}");
        }

        return string.Join("\n", lines);
    }
}

/// <summary>
/// Complete domain information from domains.2da.
/// </summary>
public class DomainInfo
{
    public int DomainId { get; set; }
    public string Label { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<DomainSpell> DomainSpells { get; set; } = new();
    public int GrantedFeatId { get; set; } = -1;
    public string GrantedFeatName { get; set; } = "";
}

/// <summary>
/// A spell granted by a domain at a specific level.
/// </summary>
public class DomainSpell
{
    public int Level { get; set; }
    public int SpellId { get; set; }
    public string Name { get; set; } = "";
}
