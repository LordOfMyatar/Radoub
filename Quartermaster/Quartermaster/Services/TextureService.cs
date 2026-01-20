using System;
using System.Collections.Generic;
using Radoub.Formats.Common;
using Radoub.Formats.Plt;
using Radoub.Formats.Services;
using Radoub.Formats.Tga;

namespace Quartermaster.Services;

/// <summary>
/// Service for loading and rendering textures for model preview.
/// Handles PLT (palette layered texture) rendering with character colors.
/// </summary>
public class TextureService
{
    private readonly IGameDataService _gameDataService;
    private readonly Dictionary<string, PaletteData> _paletteCache = new();
    private readonly Dictionary<string, byte[]?> _renderedTextureCache = new();

    public TextureService(IGameDataService gameDataService)
    {
        _gameDataService = gameDataService;
    }

    /// <summary>
    /// Load and render a PLT texture with the specified colors.
    /// </summary>
    /// <param name="pltResRef">PLT texture resource reference (without extension)</param>
    /// <param name="colorIndices">Color indices for all PLT layers (optional, uses defaults if null)</param>
    /// <returns>RGBA texture data (width, height, pixels), or null if not found</returns>
    public (int width, int height, byte[] pixels)? RenderPltTexture(
        string pltResRef,
        PltColorIndices? colorIndices = null)
    {
        if (string.IsNullOrEmpty(pltResRef))
            return null;

        colorIndices ??= new PltColorIndices();

        // Try to load PLT file
        var pltData = _gameDataService.FindResource(pltResRef.ToLowerInvariant(), ResourceTypes.Plt);
        if (pltData == null || pltData.Length == 0)
        {
            Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                Radoub.Formats.Logging.LogLevel.DEBUG,
                $"TextureService.RenderPltTexture: PLT '{pltResRef}' not found");
            return null;
        }

        try
        {
            var pltFile = PltReader.Read(pltData);

            // Load all required palettes
            var palettes = new Dictionary<int, PaletteData>();
            for (int layerId = 0; layerId <= 9; layerId++)
            {
                var palette = LoadPalette(PltLayers.GetPaletteResRef(layerId));
                if (palette != null)
                    palettes[layerId] = palette;
            }

            // Set up color indices for each layer
            var layerColors = new Dictionary<int, int>
            {
                [PltLayers.Skin] = colorIndices.Skin,
                [PltLayers.Hair] = colorIndices.Hair,
                [PltLayers.Metal1] = colorIndices.Metal1,
                [PltLayers.Metal2] = colorIndices.Metal2,
                [PltLayers.Cloth1] = colorIndices.Cloth1,
                [PltLayers.Cloth2] = colorIndices.Cloth2,
                [PltLayers.Leather1] = colorIndices.Leather1,
                [PltLayers.Leather2] = colorIndices.Leather2,
                [PltLayers.Tattoo1] = colorIndices.Tattoo1,
                [PltLayers.Tattoo2] = colorIndices.Tattoo2
            };

            // Render the PLT
            var pixels = PltReader.Render(pltFile, palettes, layerColors);
            return (pltFile.Width, pltFile.Height, pixels);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Legacy overload for backward compatibility.
    /// </summary>
    public (int width, int height, byte[] pixels)? RenderPltTexture(
        string pltResRef,
        int skinColor,
        int hairColor,
        int tattoo1Color,
        int tattoo2Color)
    {
        return RenderPltTexture(pltResRef, new PltColorIndices
        {
            Skin = skinColor,
            Hair = hairColor,
            Tattoo1 = tattoo1Color,
            Tattoo2 = tattoo2Color
        });
    }

    /// <summary>
    /// Load a regular TGA texture.
    /// </summary>
    /// <param name="tgaResRef">TGA resource reference (without extension)</param>
    /// <returns>RGBA texture data (width, height, pixels), or null if not found</returns>
    public (int width, int height, byte[] pixels)? LoadTgaTexture(string tgaResRef)
    {
        if (string.IsNullOrEmpty(tgaResRef))
            return null;

        var tgaData = _gameDataService.FindResource(tgaResRef.ToLowerInvariant(), ResourceTypes.Tga);
        if (tgaData == null || tgaData.Length == 0)
        {
            Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                Radoub.Formats.Logging.LogLevel.DEBUG,
                $"TextureService.LoadTgaTexture: TGA '{tgaResRef}' not found");
            return null;
        }

        try
        {
            var tgaImage = TgaReader.Read(tgaData);
            return (tgaImage.Width, tgaImage.Height, tgaImage.Pixels);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Load a texture (tries PLT first, then TGA, with human fallback).
    /// </summary>
    public (int width, int height, byte[] pixels)? LoadTexture(
        string resRef,
        PltColorIndices? colorIndices = null)
    {
        if (string.IsNullOrEmpty(resRef))
            return null;

        colorIndices ??= new PltColorIndices();

        // Try PLT first
        var pltResult = RenderPltTexture(resRef, colorIndices);
        if (pltResult.HasValue)
            return pltResult;

        // Fall back to TGA
        var tgaResult = LoadTgaTexture(resRef);
        if (tgaResult.HasValue)
            return tgaResult;

        // If race-specific texture not found, try human fallback
        // e.g., pme0_head001 -> pmh0_head001
        if (resRef.Length > 4 && (resRef.StartsWith("pm") || resRef.StartsWith("pf")))
        {
            var genderChar = resRef[1]; // 'm' or 'f'
            var humanResRef = $"p{genderChar}h0{resRef.Substring(4)}"; // Replace race code with 'h0'

            if (humanResRef != resRef)
            {
                Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                    Radoub.Formats.Logging.LogLevel.DEBUG,
                    $"TextureService.LoadTexture: Trying human fallback '{humanResRef}' for '{resRef}'");

                pltResult = RenderPltTexture(humanResRef, colorIndices);
                if (pltResult.HasValue)
                    return pltResult;

                return LoadTgaTexture(humanResRef);
            }
        }

        return null;
    }

    /// <summary>
    /// Legacy overload for backward compatibility.
    /// </summary>
    public (int width, int height, byte[] pixels)? LoadTexture(
        string resRef,
        int skinColor,
        int hairColor,
        int tattoo1Color,
        int tattoo2Color)
    {
        return LoadTexture(resRef, new PltColorIndices
        {
            Skin = skinColor,
            Hair = hairColor,
            Tattoo1 = tattoo1Color,
            Tattoo2 = tattoo2Color
        });
    }

    private PaletteData? LoadPalette(string paletteResRef)
    {
        if (_paletteCache.TryGetValue(paletteResRef, out var cached))
            return cached;

        var tgaData = _gameDataService.FindResource(paletteResRef, ResourceTypes.Tga);
        if (tgaData == null || tgaData.Length == 0)
        {
            _paletteCache[paletteResRef] = null!;
            return null;
        }

        try
        {
            var tgaImage = TgaReader.Read(tgaData);
            var palette = new PaletteData(tgaImage.Width, tgaImage.Height, tgaImage.Pixels);
            _paletteCache[paletteResRef] = palette;
            return palette;
        }
        catch (Exception)
        {
            _paletteCache[paletteResRef] = null!;
            return null;
        }
    }

    /// <summary>
    /// Clear all cached textures and palettes.
    /// </summary>
    public void ClearCache()
    {
        _paletteCache.Clear();
        _renderedTextureCache.Clear();
    }
}

/// <summary>
/// Color indices for PLT texture layers.
/// Maps to NWN's palette layer colors.
/// </summary>
public class PltColorIndices
{
    /// <summary>Skin color (0-175)</summary>
    public int Skin { get; set; }

    /// <summary>Hair color (0-175)</summary>
    public int Hair { get; set; }

    /// <summary>Metal1 color for armor (0-175)</summary>
    public int Metal1 { get; set; }

    /// <summary>Metal2 color for armor (0-175)</summary>
    public int Metal2 { get; set; }

    /// <summary>Cloth1 color for armor (0-175)</summary>
    public int Cloth1 { get; set; }

    /// <summary>Cloth2 color for armor (0-175)</summary>
    public int Cloth2 { get; set; }

    /// <summary>Leather1 color for armor (0-175)</summary>
    public int Leather1 { get; set; }

    /// <summary>Leather2 color for armor (0-175)</summary>
    public int Leather2 { get; set; }

    /// <summary>Tattoo1 color (0-175)</summary>
    public int Tattoo1 { get; set; }

    /// <summary>Tattoo2 color (0-175)</summary>
    public int Tattoo2 { get; set; }

    /// <summary>
    /// Create default color indices (all 0).
    /// </summary>
    public PltColorIndices() { }

    /// <summary>
    /// Create color indices from creature body colors and armor colors.
    /// </summary>
    public static PltColorIndices FromCreatureAndArmor(
        byte skinColor, byte hairColor, byte tattoo1, byte tattoo2,
        byte metal1 = 0, byte metal2 = 0,
        byte cloth1 = 0, byte cloth2 = 0,
        byte leather1 = 0, byte leather2 = 0)
    {
        return new PltColorIndices
        {
            Skin = skinColor,
            Hair = hairColor,
            Tattoo1 = tattoo1,
            Tattoo2 = tattoo2,
            Metal1 = metal1,
            Metal2 = metal2,
            Cloth1 = cloth1,
            Cloth2 = cloth2,
            Leather1 = leather1,
            Leather2 = leather2
        };
    }
}
