using System;
using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;
using SkiaSharp;

namespace Quartermaster.Services;

/// <summary>
/// Provides item icons from NWN game files with fallback to placeholder SVGs.
/// Converts decoded images to Avalonia-compatible bitmaps.
/// </summary>
public class ItemIconService
{
    private readonly ImageService _imageService;
    private readonly IGameDataService _gameDataService;
    private readonly ConcurrentDictionary<string, Bitmap?> _bitmapCache;
    private const int MaxCacheSize = 500;

    public ItemIconService(IGameDataService gameDataService)
    {
        _gameDataService = gameDataService;
        _imageService = new ImageService(gameDataService);
        _bitmapCache = new ConcurrentDictionary<string, Bitmap?>();
    }

    /// <summary>
    /// Get the icon for an item.
    /// </summary>
    /// <param name="item">The item to get icon for</param>
    /// <returns>Avalonia Bitmap, or null if not found (use placeholder)</returns>
    public Bitmap? GetItemIcon(UtiFile item)
    {
        if (item == null)
            return null;

        return GetItemIcon(item.BaseItem, item.ModelPart1);
    }

    /// <summary>
    /// Get the icon for a base item type.
    /// </summary>
    /// <param name="baseItemType">Base item type ID from baseitems.2da</param>
    /// <param name="modelNumber">Model variation number (default 0 uses minimum from 2DA)</param>
    /// <returns>Avalonia Bitmap, or null if not found</returns>
    public Bitmap? GetItemIcon(int baseItemType, int modelNumber = 0)
    {
        string cacheKey = $"item:{baseItemType}:{modelNumber}";

        if (_bitmapCache.TryGetValue(cacheKey, out var cached))
            return cached;

        // Limit cache size
        if (_bitmapCache.Count > MaxCacheSize)
        {
            ClearCache();
        }

        var imageData = _imageService.GetItemIcon(baseItemType, modelNumber);
        Bitmap? bitmap = null;

        if (imageData != null)
        {
            bitmap = ImageDataToBitmap(imageData);
        }

        _bitmapCache.TryAdd(cacheKey, bitmap);
        return bitmap;
    }

    /// <summary>
    /// Get a portrait image.
    /// </summary>
    /// <param name="resRef">Portrait ResRef (e.g., "po_elf_m_")</param>
    /// <returns>Avalonia Bitmap, or null if not found</returns>
    public Bitmap? GetPortrait(string resRef)
    {
        if (string.IsNullOrWhiteSpace(resRef))
            return null;

        string cacheKey = $"portrait:{resRef.ToLowerInvariant()}";

        if (_bitmapCache.TryGetValue(cacheKey, out var cached))
            return cached;

        // Limit cache size
        if (_bitmapCache.Count > MaxCacheSize)
        {
            ClearCache();
        }

        var imageData = _imageService.GetPortrait(resRef);
        Bitmap? bitmap = null;

        if (imageData != null)
        {
            bitmap = ImageDataToBitmap(imageData);
        }

        _bitmapCache.TryAdd(cacheKey, bitmap);
        return bitmap;
    }

    /// <summary>
    /// Check if game data is available for loading real icons.
    /// </summary>
    public bool IsGameDataAvailable => _gameDataService.IsConfigured;

    /// <summary>
    /// Clear the bitmap cache.
    /// </summary>
    public void ClearCache()
    {
        // Dispose all cached bitmaps
        foreach (var kvp in _bitmapCache)
        {
            kvp.Value?.Dispose();
        }
        _bitmapCache.Clear();
        _imageService.ClearCache();
    }

    /// <summary>
    /// Convert ImageData (RGBA bytes) to Avalonia Bitmap.
    /// </summary>
    private static Bitmap? ImageDataToBitmap(ImageData imageData)
    {
        try
        {
            // Create SkiaSharp bitmap from RGBA data
            var info = new SKImageInfo(imageData.Width, imageData.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var skBitmap = new SKBitmap(info);

            // Copy pixel data
            var pixels = skBitmap.GetPixels();
            System.Runtime.InteropServices.Marshal.Copy(imageData.Pixels, 0, pixels, imageData.Pixels.Length);

            // Encode to PNG in memory
            using var image = SKImage.FromBitmap(skBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new System.IO.MemoryStream(data.ToArray());

            // Create Avalonia bitmap from PNG
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }
}
