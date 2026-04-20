using System;
using System.Collections.Generic;
using System.IO;
using Pfim;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Plt;
using Radoub.Formats.Services;
using Radoub.Formats.Tga;
using Radoub.UI.Services;

namespace Quartermaster.Services;

/// <summary>
/// Service for loading and rendering textures for model preview.
/// Handles PLT (palette layered texture) rendering with character colors.
/// </summary>
public class TextureService
{
    private readonly IGameDataService _gameDataService;
    private readonly Dictionary<string, PaletteData?> _paletteCache = new();
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

            var layerColors = BuildLayerColors(colorIndices);

            // Render the PLT and flip to OpenGL orientation (bottom-up)
            var pixels = PltReader.Render(pltFile, palettes, layerColors);
            FlipVertically(pixels, pltFile.Width, pltFile.Height);
            return (pltFile.Width, pltFile.Height, pixels);
        }
        catch (Exception ex)
        {
            Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                Radoub.Formats.Logging.LogLevel.WARN,
                $"TextureService.RenderPltTexture: PLT '{pltResRef}' render failed: {ex.GetType().Name}: {ex.Message}");
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
            // Flip to OpenGL orientation (bottom-up) — TGA output is top-down
            FlipVertically(tgaImage.Pixels, tgaImage.Width, tgaImage.Height);
            return (tgaImage.Width, tgaImage.Height, tgaImage.Pixels);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"TextureService.LoadTgaTexture: TGA '{tgaResRef}' decode failed ({tgaData.Length} bytes): {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Load a DDS texture using Pfim decoder.
    /// Handles both standard Microsoft DDS and BioWare's proprietary DDS format.
    /// </summary>
    public (int width, int height, byte[] pixels)? LoadDdsTexture(string ddsResRef)
    {
        if (string.IsNullOrEmpty(ddsResRef))
            return null;

        var ddsData = _gameDataService.FindResource(ddsResRef.ToLowerInvariant(), ResourceTypes.Dds);
        if (ddsData == null || ddsData.Length == 0)
            return null;

        // Detect BioWare DDS format (lacks "DDS " magic header)
        // Standard DDS starts with 0x44445320 ("DDS "), BioWare starts with width/height
        bool isBiowareDds = ddsData.Length >= 20 &&
            !(ddsData[0] == 0x44 && ddsData[1] == 0x44 && ddsData[2] == 0x53 && ddsData[3] == 0x20);

        byte[]? decodableData = isBiowareDds ? ConvertBiowareDdsToStandard(ddsData) : ddsData;
        if (decodableData == null)
            return null;

        try
        {
            using var stream = new MemoryStream(decodableData);
            using var image = Pfimage.FromStream(stream);

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"TextureService.LoadDdsTexture: DDS '{ddsResRef}' {(isBiowareDds ? "BioWare" : "standard")} format, " +
                $"Pfim decoded as {image.Format}, {image.Width}x{image.Height}");
            byte[] pixels = ConvertPfimToRgba(image);

            // BioWare DDS stores DXT color endpoints as BGR 5:6:5, but Pfim
            // decodes assuming standard RGB 5:6:5, producing swapped R↔B.
            // Swap channels to correct this (#1867).
            if (isBiowareDds)
                SwapRedBlue(pixels);

            return (image.Width, image.Height, pixels);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"TextureService.LoadDdsTexture: DDS '{ddsResRef}' decode failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Convert BioWare's proprietary DDS format to standard Microsoft DDS.
    /// BioWare header (20 bytes): width(4), height(4), channels(4), pitch(4), alpha(4)
    /// Channels: 3 = DXT1 (RGB), 4 = DXT5 (RGBA)
    /// </summary>
    internal static byte[]? ConvertBiowareDdsToStandard(byte[] biowareData)
    {
        if (biowareData.Length < 20) return null;

        uint width = BitConverter.ToUInt32(biowareData, 0);
        uint height = BitConverter.ToUInt32(biowareData, 4);
        uint channels = BitConverter.ToUInt32(biowareData, 8);
        // pitch at offset 12, alpha at offset 16 - not needed for conversion

        if (width == 0 || height == 0 || width > 4096 || height > 4096)
            return null;

        // Determine DXT format from channel count
        // 3 channels = DXT1 (BC1), 4 channels = DXT5 (BC3)
        bool isDxt1 = channels == 3;
        string fourCC = isDxt1 ? "DXT1" : "DXT5";
        uint blockSize = isDxt1 ? 8u : 16u;
        uint mainImageSize = (width / 4) * (height / 4) * blockSize;

        // Build standard DDS header (128 bytes)
        // DDS header: 4 magic + 124 header bytes
        var header = new byte[128];

        // Magic "DDS "
        header[0] = 0x44; header[1] = 0x44; header[2] = 0x53; header[3] = 0x20;

        // dwSize = 124
        BitConverter.GetBytes(124u).CopyTo(header, 4);

        // dwFlags: DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT | DDSD_LINEARSIZE | DDSD_MIPMAPCOUNT
        BitConverter.GetBytes(0x000A1007u).CopyTo(header, 8);

        // dwHeight, dwWidth
        BitConverter.GetBytes(height).CopyTo(header, 12);
        BitConverter.GetBytes(width).CopyTo(header, 16);

        // dwPitchOrLinearSize
        BitConverter.GetBytes(mainImageSize).CopyTo(header, 20);

        // dwDepth = 0 (offset 24)
        // dwMipMapCount - calculate from dimensions
        uint mipCount = 1;
        uint mw = width, mh = height;
        while (mw > 1 || mh > 1) { mw = Math.Max(1, mw / 2); mh = Math.Max(1, mh / 2); mipCount++; }
        BitConverter.GetBytes(mipCount).CopyTo(header, 28);

        // dwReserved1[11] = 0 (offsets 32-75)

        // Pixel format (at offset 76, 32 bytes)
        // ddpf.dwSize = 32
        BitConverter.GetBytes(32u).CopyTo(header, 76);
        // ddpf.dwFlags = DDPF_FOURCC (0x4)
        BitConverter.GetBytes(4u).CopyTo(header, 80);
        // ddpf.dwFourCC
        header[84] = (byte)fourCC[0];
        header[85] = (byte)fourCC[1];
        header[86] = (byte)fourCC[2];
        header[87] = (byte)fourCC[3];
        // ddpf remaining fields = 0 (offsets 88-107)

        // dwCaps = DDSCAPS_TEXTURE | DDSCAPS_MIPMAP | DDSCAPS_COMPLEX (0x401008)
        BitConverter.GetBytes(0x00401008u).CopyTo(header, 108);
        // dwCaps2-4 = 0, dwReserved2 = 0

        // Combine header + pixel data (skip 20-byte BioWare header)
        int pixelDataLen = biowareData.Length - 20;
        var result = new byte[128 + pixelDataLen];
        header.CopyTo(result, 0);
        Array.Copy(biowareData, 20, result, 128, pixelDataLen);

        return result;
    }

    private static byte[] ConvertPfimToRgba(IImage image)
    {
        int width = image.Width;
        int height = image.Height;
        byte[] output = new byte[width * height * 4];
        byte[] src = image.Data;

        switch (image.Format)
        {
            case Pfim.ImageFormat.Rgba32:
                // Pfim DXT decode outputs RGBA byte order — direct copy
                Array.Copy(src, output, Math.Min(src.Length, output.Length));
                break;

            case Pfim.ImageFormat.Rgb24:
                // Pfim outputs RGB byte order — add alpha channel
                for (int i = 0, j = 0; i < output.Length && j < src.Length - 2; i += 4, j += 3)
                {
                    output[i] = src[j];
                    output[i + 1] = src[j + 1];
                    output[i + 2] = src[j + 2];
                    output[i + 3] = 255;
                }
                break;

            case Pfim.ImageFormat.Rgb8:
                for (int i = 0, j = 0; i < output.Length && j < src.Length; i += 4, j++)
                {
                    output[i] = src[j];
                    output[i + 1] = src[j];
                    output[i + 2] = src[j];
                    output[i + 3] = 255;
                }
                break;

            default:
                for (int i = 0; i < output.Length; i += 4)
                {
                    output[i] = 128;
                    output[i + 1] = 128;
                    output[i + 2] = 128;
                    output[i + 3] = 255;
                }
                break;
        }

        return output;
    }

    /// <summary>
    /// Load a texture preferring BIF over HAK (Override → BIF, skip HAK).
    /// For base game creatures, the BIF version is used to avoid CEP texture incompatibilities.
    /// For CEP-only textures (not in BIF), falls back to full resolution.
    /// Mirrors the LoadModelPreferBIF pattern in ModelService (#1867).
    /// </summary>
    public (int width, int height, byte[] pixels)? LoadTexturePreferBIF(
        string resRef,
        PltColorIndices? colorIndices = null)
    {
        var result = LoadTexturePreferBIFWithKind(resRef, colorIndices);
        return result.HasValue ? (result.Value.width, result.Value.height, result.Value.pixels) : null;
    }

    /// <summary>
    /// Same as <see cref="LoadTexturePreferBIF"/> but also reports whether the texture
    /// came from a PLT (color-index-dependent) source. Callers can use this to cache
    /// non-PLT textures across color changes.
    /// </summary>
    public (int width, int height, byte[] pixels, bool isPlt)? LoadTexturePreferBIFWithKind(
        string resRef,
        PltColorIndices? colorIndices = null)
    {
        if (string.IsNullOrEmpty(resRef))
            return null;

        colorIndices ??= new PltColorIndices();
        resRef = resRef.ToLowerInvariant();

        // Try BIF first (Override → BIF, skip HAK)
        var bifResult = LoadTextureFromBaseWithKind(resRef, colorIndices);
        if (bifResult.HasValue)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"TextureService.LoadTexturePreferBIF: '{resRef}' from BIF (isPlt={bifResult.Value.isPlt})");
            return bifResult;
        }

        // Not in BIF — fall back to full resolution (CEP-only texture)
        var fullResult = LoadTextureWithKind(resRef, colorIndices);
        if (fullResult.HasValue)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"TextureService.LoadTexturePreferBIF: '{resRef}' from HAK (CEP-only, isPlt={fullResult.Value.isPlt})");
        }
        return fullResult;
    }

    /// <summary>
    /// Load a texture from base game resources only (Override → BIF, skip HAK).
    /// Tries PLT, TGA, DDS in order using FindBaseResource.
    /// </summary>
    private (int width, int height, byte[] pixels)? LoadTextureFromBase(
        string resRef,
        PltColorIndices colorIndices)
    {
        var result = LoadTextureFromBaseWithKind(resRef, colorIndices);
        return result.HasValue ? (result.Value.width, result.Value.height, result.Value.pixels) : null;
    }

    private (int width, int height, byte[] pixels, bool isPlt)? LoadTextureFromBaseWithKind(
        string resRef,
        PltColorIndices colorIndices)
    {
        // Try PLT
        var pltData = _gameDataService.FindBaseResource(resRef, ResourceTypes.Plt);
        if (pltData != null && pltData.Length > 0)
        {
            try
            {
                var pltFile = PltReader.Read(pltData);
                var palettes = new Dictionary<int, PaletteData>();
                for (int layerId = 0; layerId <= 9; layerId++)
                {
                    var palette = LoadPalette(PltLayers.GetPaletteResRef(layerId));
                    if (palette != null)
                        palettes[layerId] = palette;
                }
                var layerColors = BuildLayerColors(colorIndices);
                var pixels = PltReader.Render(pltFile, palettes, layerColors);
                FlipVertically(pixels, pltFile.Width, pltFile.Height);
                return (pltFile.Width, pltFile.Height, pixels, true);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"TextureService.LoadTextureFromBase: PLT '{resRef}' render failed: {ex.Message}");
            }
        }

        // Try TGA
        var tgaData = _gameDataService.FindBaseResource(resRef, ResourceTypes.Tga);
        if (tgaData != null && tgaData.Length > 0)
        {
            try
            {
                var tgaImage = TgaReader.Read(tgaData);
                FlipVertically(tgaImage.Pixels, tgaImage.Width, tgaImage.Height);
                return (tgaImage.Width, tgaImage.Height, tgaImage.Pixels, false);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"TextureService.LoadTextureFromBase: TGA '{resRef}' decode failed: {ex.Message}");
            }
        }

        // Try DDS
        var ddsData = _gameDataService.FindBaseResource(resRef, ResourceTypes.Dds);
        if (ddsData != null && ddsData.Length > 0)
        {
            bool isBiowareDds = ddsData.Length >= 20 &&
                !(ddsData[0] == 0x44 && ddsData[1] == 0x44 && ddsData[2] == 0x53 && ddsData[3] == 0x20);
            byte[]? decodableData = isBiowareDds ? ConvertBiowareDdsToStandard(ddsData) : ddsData;
            if (decodableData != null)
            {
                try
                {
                    using var stream = new MemoryStream(decodableData);
                    using var image = Pfimage.FromStream(stream);
                    byte[] rgbaPixels = ConvertPfimToRgba(image);
                    if (isBiowareDds)
                        SwapRedBlue(rgbaPixels);
                    return (image.Width, image.Height, rgbaPixels, false);
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"TextureService.LoadTextureFromBase: DDS '{resRef}' decode failed: {ex.Message}");
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Build layer color mapping from PltColorIndices.
    /// </summary>
    internal static Dictionary<int, int> BuildLayerColors(PltColorIndices colorIndices)
    {
        return new Dictionary<int, int>
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
    }

    /// <summary>
    /// Load a texture (tries PLT first, then TGA, then DDS, with human fallback).
    /// </summary>
    public (int width, int height, byte[] pixels)? LoadTexture(
        string resRef,
        PltColorIndices? colorIndices = null)
    {
        var result = LoadTextureWithKind(resRef, colorIndices);
        return result.HasValue ? (result.Value.width, result.Value.height, result.Value.pixels) : null;
    }

    /// <summary>
    /// Same as <see cref="LoadTexture"/> but also reports whether the texture came from
    /// a PLT (color-index-dependent) source. Callers can use this to cache non-PLT
    /// textures across color changes.
    /// </summary>
    public (int width, int height, byte[] pixels, bool isPlt)? LoadTextureWithKind(
        string resRef,
        PltColorIndices? colorIndices = null)
    {
        if (string.IsNullOrEmpty(resRef))
            return null;

        colorIndices ??= new PltColorIndices();

        // Try PLT first, then TGA, then DDS (matches Aurora Engine resolution order)
        var pltResult = RenderPltTexture(resRef, colorIndices);
        if (pltResult.HasValue)
            return (pltResult.Value.width, pltResult.Value.height, pltResult.Value.pixels, true);

        var tgaResult = LoadTgaTexture(resRef);
        if (tgaResult.HasValue)
            return (tgaResult.Value.width, tgaResult.Value.height, tgaResult.Value.pixels, false);

        var ddsResult = LoadDdsTexture(resRef);
        if (ddsResult.HasValue)
            return (ddsResult.Value.width, ddsResult.Value.height, ddsResult.Value.pixels, false);

        // If race-specific texture not found, try human fallback
        // e.g., pme0_head001 -> pmh0_head001
        if (resRef.Length > 4 && (resRef.StartsWith("pm") || resRef.StartsWith("pf")))
        {
            var genderChar = resRef[1]; // 'm' or 'f'
            var humanResRef = $"p{genderChar}h0{resRef.Substring(4)}"; // Replace race code with 'h0'

            if (humanResRef != resRef)
            {
                pltResult = RenderPltTexture(humanResRef, colorIndices);
                if (pltResult.HasValue)
                    return (pltResult.Value.width, pltResult.Value.height, pltResult.Value.pixels, true);

                tgaResult = LoadTgaTexture(humanResRef);
                if (tgaResult.HasValue)
                    return (tgaResult.Value.width, tgaResult.Value.height, tgaResult.Value.pixels, false);

                ddsResult = LoadDdsTexture(humanResRef);
                if (ddsResult.HasValue)
                    return (ddsResult.Value.width, ddsResult.Value.height, ddsResult.Value.pixels, false);
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
            _paletteCache[paletteResRef] = null;
            return null;
        }

        try
        {
            var tgaImage = TgaReader.Read(tgaData);
            var palette = new PaletteData(tgaImage.Width, tgaImage.Height, tgaImage.Pixels);
            _paletteCache[paletteResRef] = palette;
            return palette;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Failed to load palette '{paletteResRef}': {ex.Message}");
            _paletteCache[paletteResRef] = null;
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

    /// <summary>
    /// Swap red and blue channels in RGBA pixel data in-place.
    /// BioWare's proprietary DDS format stores DXT color endpoints as BGR 5:6:5,
    /// but Pfim's DXT decoder assumes standard RGB 5:6:5 ordering.
    /// This produces R↔B swapped output for BioWare DDS only (#1867).
    /// </summary>
    internal static void SwapRedBlue(byte[] rgba)
    {
        for (int i = 0; i < rgba.Length - 2; i += 4)
        {
            (rgba[i], rgba[i + 2]) = (rgba[i + 2], rgba[i]);
        }
    }

    /// <summary>
    /// Flip RGBA pixel data vertically in-place for OpenGL orientation.
    /// TGA and PLT textures are decoded top-down (row 0 = top), but OpenGL
    /// expects bottom-up (row 0 = bottom). DDS textures via Pfim are already
    /// in OpenGL orientation and should NOT be flipped (#1867).
    /// </summary>
    internal static void FlipVertically(byte[] rgba, int width, int height)
    {
        int rowBytes = width * 4;
        var tempRow = new byte[rowBytes];
        for (int y = 0; y < height / 2; y++)
        {
            int topOffset = y * rowBytes;
            int bottomOffset = (height - 1 - y) * rowBytes;
            Array.Copy(rgba, topOffset, tempRow, 0, rowBytes);
            Array.Copy(rgba, bottomOffset, rgba, topOffset, rowBytes);
            Array.Copy(tempRow, 0, rgba, bottomOffset, rowBytes);
        }
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
