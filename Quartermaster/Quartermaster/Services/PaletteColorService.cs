using System;
using System.Collections.Generic;
using Avalonia.Media;
using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.Tga;

namespace Quartermaster.Services;

/// <summary>
/// Provides color extraction from NWN palette TGA files.
/// Caches loaded palettes for performance.
/// </summary>
public class PaletteColorService
{
    private readonly IGameDataService _gameDataService;
    private readonly Dictionary<string, TgaImage?> _paletteCache = new();

    /// <summary>
    /// Palette file names for each color type.
    /// </summary>
    public static class Palettes
    {
        public const string Skin = "pal_skin01";
        public const string Hair = "pal_hair01";
        public const string Tattoo1 = "pal_tattoo01";
        public const string Tattoo2 = "pal_tattoo01"; // Same palette as Tattoo1
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

        // Use X=127 (middle of the shading range) for a representative color
        // Y = colorIndex (row in the palette)
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
    /// <param name="paletteName">Palette file name</param>
    /// <param name="colorIndex">Color index (row)</param>
    /// <param name="numStops">Number of gradient stops (default 8)</param>
    /// <returns>List of (offset, color) tuples for LinearGradientBrush</returns>
    public List<(double offset, Color color)> GetPaletteGradient(string paletteName, byte colorIndex, int numStops = 8)
    {
        var stops = new List<(double offset, Color color)>();
        var palette = GetPalette(paletteName);

        if (palette == null)
        {
            // Return gray gradient if palette not found
            stops.Add((0.0, Colors.DarkGray));
            stops.Add((1.0, Colors.LightGray));
            return stops;
        }

        // Sample across the X-axis (0-255 is the shading range)
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

    /// <summary>
    /// Load a palette from game resources, with caching.
    /// </summary>
    private TgaImage? GetPalette(string paletteName)
    {
        if (_paletteCache.TryGetValue(paletteName, out var cached))
            return cached;

        TgaImage? palette = null;
        try
        {
            var data = _gameDataService.FindResource(paletteName, ResourceTypes.Tga);
            if (data != null)
            {
                palette = TgaReader.Read(data);
            }
        }
        catch (Exception)
        {
            // Failed to load palette - will use fallback color
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
