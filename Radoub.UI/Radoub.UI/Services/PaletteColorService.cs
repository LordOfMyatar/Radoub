using System;
using System.Collections.Generic;
using Avalonia.Media;
using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.Tga;

namespace Radoub.UI.Services;

/// <summary>
/// Provides color extraction from NWN palette TGA files.
/// Caches loaded palettes for performance.
/// Shared across all Radoub tools (character and item color palettes).
/// </summary>
public class PaletteColorService
{
    private readonly IGameDataService _gameDataService;
    private readonly Dictionary<string, TgaImage?> _paletteCache = new();

    /// <summary>
    /// Palette file names for each color type.
    ///
    /// Per the BioWare Aurora item format spec (Section 2.1.2.4): all six armor color
    /// fields index into the SAME single palette per material — there is no <c>pal_*02</c>
    /// in NWN. The "1" / "2" distinction is which PLT layer pixel they apply to in the
    /// rendered armor texture, not which palette file. Same convention for tattoo1/2.
    /// </summary>
    public static class Palettes
    {
        // Character palettes
        public const string Skin = "pal_skin01";
        public const string Hair = "pal_hair01";
        public const string Tattoo1 = "pal_tattoo01";
        public const string Tattoo2 = "pal_tattoo01"; // Same palette as Tattoo1

        // Item palettes — all "2" slots use the same palette file as the matching "1" slot
        // per the wiki spec (Cloth2 indexes pal_cloth01.tga, Leather2 → pal_leath01.tga, etc.)
        public const string Cloth1 = "pal_cloth01";
        public const string Cloth2 = "pal_cloth01"; // Same palette as Cloth1 (pal_cloth02 does not exist)
        public const string Leather1 = "pal_leath01";
        public const string Leather2 = "pal_leath01"; // Same palette as Leather1
        public const string Metal1 = "pal_armor01";
        public const string Metal2 = "pal_armor01"; // Same palette as Metal1
    }

    public PaletteColorService(IGameDataService gameDataService)
    {
        _gameDataService = gameDataService;
    }

    /// <summary>
    /// Get the color for a palette index.
    /// </summary>
    /// <param name="paletteName">Palette file name (e.g., "pal_skin01")</param>
    /// <param name="colorIndex">Color index (0-175)</param>
    /// <returns>Avalonia Color, or gray if palette not found</returns>
    public Color GetPaletteColor(string paletteName, byte colorIndex)
    {
        var palette = GetPalette(paletteName);
        if (palette == null)
            return Colors.Gray;

        var (r, g, b, a) = TgaReader.GetPixel(palette, 127, colorIndex);
        return Color.FromArgb(a, r, g, b);
    }

    /// <summary>
    /// Get the skin color for a palette index.
    /// </summary>
    public Color GetSkinColor(byte colorIndex) => GetPaletteColor(Palettes.Skin, colorIndex);

    /// <summary>
    /// Get the hair color for a palette index.
    /// </summary>
    public Color GetHairColor(byte colorIndex) => GetPaletteColor(Palettes.Hair, colorIndex);

    /// <summary>
    /// Get the tattoo color for a palette index (both tattoos use same palette).
    /// </summary>
    public Color GetTattooColor(byte colorIndex) => GetPaletteColor(Palettes.Tattoo1, colorIndex);

    /// <summary>
    /// Get gradient stops for a palette row. Returns colors sampled across the X-axis.
    /// </summary>
    public List<(double offset, Color color)> GetPaletteGradient(string paletteName, byte colorIndex, int numStops = 8)
    {
        var stops = new List<(double offset, Color color)>();
        var palette = GetPalette(paletteName);

        if (palette == null)
        {
            stops.Add((0.0, Colors.DarkGray));
            stops.Add((1.0, Colors.LightGray));
            return stops;
        }

        if (numStops < 2) numStops = 2;

        for (int i = 0; i < numStops; i++)
        {
            double offset = (double)i / (numStops - 1);
            int x = (int)(offset * 255);
            var (r, g, b, a) = TgaReader.GetPixel(palette, x, colorIndex);
            stops.Add((offset, Color.FromArgb(a, r, g, b)));
        }

        return stops;
    }

    /// <summary>
    /// Create a LinearGradientBrush for a palette row.
    /// </summary>
    public LinearGradientBrush CreateGradientBrush(string paletteName, byte colorIndex)
    {
        var stops = GetPaletteGradient(paletteName, colorIndex);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Avalonia.RelativePoint(0, 0.5, Avalonia.RelativeUnit.Relative),
            EndPoint = new Avalonia.RelativePoint(1, 0.5, Avalonia.RelativeUnit.Relative)
        };

        foreach (var (offset, color) in stops)
        {
            brush.GradientStops.Add(new GradientStop(color, offset));
        }

        return brush;
    }

    private TgaImage? GetPalette(string paletteName)
    {
        if (_paletteCache.TryGetValue(paletteName, out var cached))
            return cached;

        TgaImage? palette = null;
        try
        {
            var data = _gameDataService.FindResource(paletteName, ResourceTypes.Tga);
            if (data == null)
            {
                Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                    Radoub.Formats.Logging.LogLevel.DEBUG,
                    $"PaletteColorService: palette TGA '{paletteName}' not found in game resources");
            }
            else
            {
                palette = TgaReader.Read(data);
            }
        }
        catch (Exception ex)
        {
            // Log so future "all gray" diagnoses don't require code reading.
            Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                Radoub.Formats.Logging.LogLevel.DEBUG,
                $"PaletteColorService: failed to parse palette '{paletteName}': {ex.GetType().Name}: {ex.Message}");
        }

        _paletteCache[paletteName] = palette;
        return palette;
    }

    /// <summary>
    /// Clear the palette cache. Call when game data paths change.
    /// </summary>
    public void ClearCache()
    {
        _paletteCache.Clear();
    }
}
