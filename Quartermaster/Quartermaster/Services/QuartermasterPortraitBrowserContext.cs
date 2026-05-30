using System.Collections.Generic;
using Avalonia.Media.Imaging;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace Quartermaster.Services;

/// <summary>
/// Quartermaster's implementation of <see cref="IPortraitBrowserContext"/>.
/// Sources portraits from portraits.2da and images from the shared
/// <see cref="ItemIconService"/> for the shared PortraitBrowserWindow (#2291).
/// </summary>
public class QuartermasterPortraitBrowserContext : IPortraitBrowserContext
{
    private readonly IGameDataService _gameDataService;
    private readonly ItemIconService _itemIconService;

    public QuartermasterPortraitBrowserContext(IGameDataService gameDataService, ItemIconService itemIconService)
    {
        _gameDataService = gameDataService;
        _itemIconService = itemIconService;
    }

    public string? CurrentFileDirectory => null;

    public string? NeverwinterNightsPath => RadoubSettings.Instance.NeverwinterNightsPath;

    public bool GameResourcesAvailable => _gameDataService?.IsConfigured ?? false;

    public IEnumerable<PortraitEntry> ListPortraits()
    {
        var portraits = new List<PortraitEntry>();

        int rowCount = _gameDataService.Get2DA("portraits")?.RowCount ?? 500;
        for (int i = 0; i < rowCount; i++)
        {
            var baseResRef = _gameDataService.Get2DAValue("portraits", i, "BaseResRef");
            if (string.IsNullOrEmpty(baseResRef) || baseResRef == "****")
                continue;

            var raceStr = _gameDataService.Get2DAValue("portraits", i, "Race");
            var sexStr = _gameDataService.Get2DAValue("portraits", i, "Sex");

            int race = -1;
            int sex = -1;

            if (!string.IsNullOrEmpty(raceStr) && raceStr != "****")
                int.TryParse(raceStr, out race);

            if (!string.IsNullOrEmpty(sexStr) && sexStr != "****")
                int.TryParse(sexStr, out sex);

            portraits.Add(new PortraitEntry
            {
                Id = (ushort)i,
                ResRef = baseResRef,
                Race = race,
                Sex = sex
            });
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"PortraitBrowserContext: Loaded {portraits.Count} portraits from portraits.2da");
        return portraits;
    }

    public Bitmap? GetPortraitBitmap(string resRef) => _itemIconService.GetPortrait(resRef);

    public string GetRaceName(int raceId)
    {
        if (raceId < 0)
            return "Unknown";

        var strRef = _gameDataService.Get2DAValue("racialtypes", raceId, "Name");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****" && uint.TryParse(strRef, out var tlkRef))
        {
            var name = _gameDataService.GetString(tlkRef);
            if (!string.IsNullOrEmpty(name))
                return name;
        }

        // Fallback names for common races when TLK lookup fails.
        return raceId switch
        {
            0 => "Dwarf",
            1 => "Elf",
            2 => "Gnome",
            3 => "Halfling",
            4 => "Half-Elf",
            5 => "Half-Orc",
            6 => "Human",
            _ => $"Race {raceId}"
        };
    }
}
