using System.Collections.Concurrent;
using Pfim;
using Radoub.Formats.Common;
using Radoub.Formats.Plt;
using Radoub.Formats.Tga;

namespace Radoub.Formats.Services;

/// <summary>
/// Implementation of IImageService for loading NWN image assets.
/// Supports TGA, DDS (via Pfim), and PLT formats.
/// </summary>
public class ImageService : IImageService
{
    private readonly IGameDataService _gameData;
    private readonly ConcurrentDictionary<string, ImageData?> _imageCache;
    private readonly ConcurrentDictionary<int, PaletteData?> _paletteCache;
    private const int MaxCacheSize = 500;

    public ImageService(IGameDataService gameData)
    {
        _gameData = gameData;
        _imageCache = new ConcurrentDictionary<string, ImageData?>();
        _paletteCache = new ConcurrentDictionary<int, PaletteData?>();
    }

    /// <inheritdoc/>
    public ImageData? DecodeImage(byte[] data, string format)
    {
        if (data == null || data.Length == 0)
            return null;

        try
        {
            return format.ToLowerInvariant() switch
            {
                "tga" => DecodeTga(data),
                "dds" => DecodeDds(data),
                "plt" => DecodePlt(data),
                _ => DecodeTga(data) // Default to TGA
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public ImageData? LoadImage(string resRef, ushort resourceType)
    {
        if (string.IsNullOrWhiteSpace(resRef))
            return null;

        string cacheKey = $"{resRef.ToLowerInvariant()}:{resourceType}";

        if (_imageCache.TryGetValue(cacheKey, out var cached))
            return cached;

        // Limit cache size
        if (_imageCache.Count > MaxCacheSize)
        {
            ClearCache();
        }

        var data = _gameData.FindResource(resRef, resourceType);
        if (data == null)
            return null;

        string format = resourceType switch
        {
            ResourceTypes.Tga => "tga",
            ResourceTypes.Dds => "dds",
            ResourceTypes.Plt => "plt",
            _ => "tga"
        };

        var image = DecodeImage(data, format);
        _imageCache.TryAdd(cacheKey, image);
        return image;
    }

    /// <inheritdoc/>
    public ImageData? GetItemIcon(int baseItemType, int modelNumber = 0)
    {
        // Look up icon info from baseitems.2da
        var iconResRef = GetItemIconResRef(baseItemType, modelNumber);
        if (string.IsNullOrEmpty(iconResRef))
            return null;

        // Try TGA first (most common)
        var image = LoadImage(iconResRef, ResourceTypes.Tga);
        if (image != null)
            return image;

        // Try PLT for layered icons (armor, helmets)
        image = LoadImage(iconResRef, ResourceTypes.Plt);
        if (image != null)
            return image;

        // Try DDS as fallback
        return LoadImage(iconResRef, ResourceTypes.Dds);
    }

    /// <inheritdoc/>
    public ImageData? GetPortrait(string resRef)
    {
        if (string.IsNullOrWhiteSpace(resRef))
            return null;

        // Portraits are typically TGA files
        // Try full-size first (po_*_l), then medium (po_*_m), then small (po_*_s)
        string baseRef = resRef.TrimEnd('l', 'm', 's', 'h', 't', '_');

        var image = LoadImage($"{baseRef}m", ResourceTypes.Tga);
        if (image != null)
            return image;

        image = LoadImage($"{baseRef}l", ResourceTypes.Tga);
        if (image != null)
            return image;

        return LoadImage($"{baseRef}s", ResourceTypes.Tga);
    }

    /// <inheritdoc/>
    public void ClearCache()
    {
        _imageCache.Clear();
        _paletteCache.Clear();
    }

    private ImageData? DecodeTga(byte[] data)
    {
        try
        {
            var tga = TgaReader.Read(data);
            return new ImageData(tga.Width, tga.Height, tga.Pixels);
        }
        catch
        {
            // Try Pfim as fallback for TGA edge cases
            return DecodePfim(data);
        }
    }

    private ImageData? DecodeDds(byte[] data)
    {
        return DecodePfim(data);
    }

    private ImageData? DecodePfim(byte[] data)
    {
        try
        {
            using var stream = new MemoryStream(data);
            using var image = Pfimage.FromStream(stream);

            // Convert to RGBA
            byte[] pixels = ConvertPfimToRgba(image);
            return new ImageData(image.Width, image.Height, pixels);
        }
        catch
        {
            return null;
        }
    }

    private byte[] ConvertPfimToRgba(IImage image)
    {
        int width = image.Width;
        int height = image.Height;
        byte[] output = new byte[width * height * 4];
        byte[] src = image.Data;

        switch (image.Format)
        {
            case ImageFormat.Rgba32:
                // Already RGBA, just copy
                Array.Copy(src, output, Math.Min(src.Length, output.Length));
                break;

            case ImageFormat.Rgb24:
                // RGB -> RGBA
                for (int i = 0, j = 0; i < output.Length && j < src.Length - 2; i += 4, j += 3)
                {
                    output[i] = src[j];         // R
                    output[i + 1] = src[j + 1]; // G
                    output[i + 2] = src[j + 2]; // B
                    output[i + 3] = 255;        // A
                }
                break;

            case ImageFormat.Rgb8:
                // Grayscale -> RGBA
                for (int i = 0, j = 0; i < output.Length && j < src.Length; i += 4, j++)
                {
                    output[i] = src[j];     // R
                    output[i + 1] = src[j]; // G
                    output[i + 2] = src[j]; // B
                    output[i + 3] = 255;    // A
                }
                break;

            default:
                // Unsupported format, fill with gray
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

    private ImageData? DecodePlt(byte[] data)
    {
        try
        {
            var plt = PltReader.Read(data);

            // Load palettes for all layers
            var palettes = new Dictionary<int, PaletteData>();
            for (int layer = 0; layer <= 9; layer++)
            {
                var palette = LoadPalette(layer);
                if (palette != null)
                {
                    palettes[layer] = palette;
                }
            }

            // Use default color indices (0 for all layers)
            // Real usage would pass creature's actual color values
            var colorIndices = new Dictionary<int, int>
            {
                { PltLayers.Skin, 0 },
                { PltLayers.Hair, 0 },
                { PltLayers.Metal1, 0 },
                { PltLayers.Metal2, 0 },
                { PltLayers.Cloth1, 0 },
                { PltLayers.Cloth2, 0 },
                { PltLayers.Leather1, 0 },
                { PltLayers.Leather2, 0 },
                { PltLayers.Tattoo1, 0 },
                { PltLayers.Tattoo2, 0 }
            };

            byte[] pixels = PltReader.Render(plt, palettes, colorIndices);
            return new ImageData(plt.Width, plt.Height, pixels);
        }
        catch
        {
            return null;
        }
    }

    private PaletteData? LoadPalette(int layerId)
    {
        if (_paletteCache.TryGetValue(layerId, out var cached))
            return cached;

        string resRef = PltLayers.GetPaletteResRef(layerId);
        var data = _gameData.FindResource(resRef, ResourceTypes.Tga);
        if (data == null)
        {
            _paletteCache.TryAdd(layerId, null);
            return null;
        }

        try
        {
            var tga = TgaReader.Read(data);
            var palette = new PaletteData(tga.Width, tga.Height, tga.Pixels);
            _paletteCache.TryAdd(layerId, palette);
            return palette;
        }
        catch
        {
            _paletteCache.TryAdd(layerId, null);
            return null;
        }
    }

    private string? GetItemIconResRef(int baseItemType, int modelNumber)
    {
        // Look up from baseitems.2da
        // Relevant columns: ItemClass, DefaultIcon, MinRange, MaxRange

        string? itemClass = _gameData.Get2DAValue("baseitems", baseItemType, "ItemClass");
        string? defaultIcon = _gameData.Get2DAValue("baseitems", baseItemType, "DefaultIcon");

        if (string.IsNullOrEmpty(itemClass) && string.IsNullOrEmpty(defaultIcon))
            return null;

        // If we have a DefaultIcon, use it directly
        if (!string.IsNullOrEmpty(defaultIcon) && defaultIcon != "****")
        {
            return defaultIcon;
        }

        // Otherwise, construct from ItemClass pattern
        // Format: i<ItemClass>_<number>.tga
        if (string.IsNullOrEmpty(itemClass) || itemClass == "****")
            return null;

        // Get model range for this base item
        string? minRangeStr = _gameData.Get2DAValue("baseitems", baseItemType, "MinRange");
        string? maxRangeStr = _gameData.Get2DAValue("baseitems", baseItemType, "MaxRange");

        int minRange = 1;
        int maxRange = 1;
        if (int.TryParse(minRangeStr, out int min)) minRange = min;
        if (int.TryParse(maxRangeStr, out int max)) maxRange = max;

        // Clamp model number to valid range
        int iconNum = Math.Clamp(modelNumber, minRange, maxRange);
        if (iconNum == 0) iconNum = minRange;

        // Build icon ResRef: i<ItemClass>_<number>
        return $"i{itemClass}_{iconNum:D3}";
    }
}
