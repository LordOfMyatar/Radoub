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
    /// <param name="skinColor">Skin color index (0-175)</param>
    /// <param name="hairColor">Hair color index (0-175)</param>
    /// <param name="tattoo1Color">Tattoo 1 color index (0-175)</param>
    /// <param name="tattoo2Color">Tattoo 2 color index (0-175)</param>
    /// <returns>RGBA texture data (width, height, pixels), or null if not found</returns>
    public (int width, int height, byte[] pixels)? RenderPltTexture(
        string pltResRef,
        int skinColor = 0,
        int hairColor = 0,
        int tattoo1Color = 0,
        int tattoo2Color = 0)
    {
        if (string.IsNullOrEmpty(pltResRef))
            return null;

        // Try to load PLT file
        var pltData = _gameDataService.FindResource(pltResRef.ToLowerInvariant(), ResourceTypes.Plt);
        if (pltData == null || pltData.Length == 0)
            return null;

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
            var colorIndices = new Dictionary<int, int>
            {
                [PltLayers.Skin] = skinColor,
                [PltLayers.Hair] = hairColor,
                [PltLayers.Metal1] = 0, // Default metal colors
                [PltLayers.Metal2] = 0,
                [PltLayers.Cloth1] = 0, // Default cloth colors
                [PltLayers.Cloth2] = 0,
                [PltLayers.Leather1] = 0, // Default leather colors
                [PltLayers.Leather2] = 0,
                [PltLayers.Tattoo1] = tattoo1Color,
                [PltLayers.Tattoo2] = tattoo2Color
            };

            // Render the PLT
            var pixels = PltReader.Render(pltFile, palettes, colorIndices);
            return (pltFile.Width, pltFile.Height, pixels);
        }
        catch (Exception)
        {
            return null;
        }
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
            return null;

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
    /// Load a texture (tries PLT first, then TGA).
    /// </summary>
    public (int width, int height, byte[] pixels)? LoadTexture(
        string resRef,
        int skinColor = 0,
        int hairColor = 0,
        int tattoo1Color = 0,
        int tattoo2Color = 0)
    {
        if (string.IsNullOrEmpty(resRef))
            return null;

        // Try PLT first
        var pltResult = RenderPltTexture(resRef, skinColor, hairColor, tattoo1Color, tattoo2Color);
        if (pltResult.HasValue)
            return pltResult;

        // Fall back to TGA
        return LoadTgaTexture(resRef);
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
