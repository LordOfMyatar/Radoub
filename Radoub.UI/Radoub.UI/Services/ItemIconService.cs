using System;
using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;
using SkiaSharp;

namespace Radoub.UI.Services;

/// <summary>
/// Provides item icons from NWN game files with fallback to placeholder SVGs.
/// Converts decoded images to Avalonia-compatible bitmaps.
/// Shared across all Radoub tools (Quartermaster, Fence, etc.).
/// </summary>
public class ItemIconService
{
    private readonly ImageService _imageService;
    private readonly IGameDataService _gameDataService;
    private readonly ConcurrentDictionary<string, Bitmap?> _bitmapCache;
    // No cache limit - keep icons for session lifetime (~15-30MB for 1500 icons is acceptable)

    public ItemIconService(IGameDataService gameDataService)
    {
        ArgumentNullException.ThrowIfNull(gameDataService);
        _gameDataService = gameDataService;
        _imageService = new ImageService(gameDataService);
        _bitmapCache = new ConcurrentDictionary<string, Bitmap?>();
    }

    /// <summary>
    /// Get the icon for an item.
    /// </summary>
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
    public Bitmap? GetItemIcon(int baseItemType, int modelNumber = 0)
    {
        string cacheKey = $"item:{baseItemType}:{modelNumber}";

        if (_bitmapCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var imageData = _imageService.GetItemIcon(baseItemType, modelNumber);
        Bitmap? bitmap = null;

        if (imageData != null)
        {
            bitmap = ImageDataToBitmap(imageData);
            if (bitmap == null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"ItemIconService: Failed to convert icon for base type {baseItemType} model {modelNumber} " +
                    $"({imageData.Width}x{imageData.Height}) to bitmap");
            }
        }

        _bitmapCache.TryAdd(cacheKey, bitmap);
        return bitmap;
    }

    /// <summary>
    /// Get a portrait image.
    /// </summary>
    public Bitmap? GetPortrait(string resRef)
    {
        if (string.IsNullOrWhiteSpace(resRef))
            return null;

        string cacheKey = $"portrait:{resRef.ToLowerInvariant()}";

        if (_bitmapCache.TryGetValue(cacheKey, out var cached))
            return cached;

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
    /// Get a spell icon.
    /// </summary>
    public Bitmap? GetSpellIcon(int spellId)
    {
        return GetCachedIcon($"spell:{spellId}", () => _imageService.GetSpellIcon(spellId));
    }

    /// <summary>
    /// Get a feat icon.
    /// </summary>
    public Bitmap? GetFeatIcon(int featId)
    {
        return GetCachedIcon($"feat:{featId}", () => _imageService.GetFeatIcon(featId));
    }

    /// <summary>
    /// Get a skill icon.
    /// </summary>
    public Bitmap? GetSkillIcon(int skillId)
    {
        return GetCachedIcon($"skill:{skillId}", () => _imageService.GetSkillIcon(skillId));
    }

    /// <summary>
    /// Get a class icon.
    /// </summary>
    public Bitmap? GetClassIcon(int classId)
    {
        return GetCachedIcon($"class:{classId}", () => _imageService.GetClassIcon(classId));
    }

    /// <summary>
    /// Helper to get cached icon with lazy loading.
    /// </summary>
    private Bitmap? GetCachedIcon(string cacheKey, Func<ImageData?> loader)
    {
        if (_bitmapCache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            var imageData = loader();
            Bitmap? bitmap = null;

            if (imageData != null)
            {
                bitmap = ImageDataToBitmap(imageData);
            }

            _bitmapCache.TryAdd(cacheKey, bitmap);
            return bitmap;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"ItemIconService: Exception for {cacheKey}: {ex.Message}");
            _bitmapCache.TryAdd(cacheKey, null);
            return null;
        }
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
        // NOTE: Do NOT dispose bitmaps here - they may still be bound to UI Image controls.
        // Disposing while bound causes Avalonia crash in Image.ArrangeOverride.
        // Let GC handle bitmap cleanup when controls are disposed.
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
            if (imageData.Width <= 0 || imageData.Height <= 0)
                return null;

            int expectedSize = imageData.Width * imageData.Height * 4;
            if (imageData.Pixels == null || imageData.Pixels.Length != expectedSize)
                return null;

            var info = new SKImageInfo(imageData.Width, imageData.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var skBitmap = new SKBitmap(info);

            var pixels = skBitmap.GetPixels();
            if (pixels == IntPtr.Zero)
                return null;

            System.Runtime.InteropServices.Marshal.Copy(imageData.Pixels, 0, pixels, imageData.Pixels.Length);

            using var image = SKImage.FromBitmap(skBitmap);
            if (image == null)
                return null;

            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            if (data == null)
                return null;

            using var stream = new System.IO.MemoryStream(data.ToArray());
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"ImageDataToBitmap: Exception: {ex.Message}");
            return null;
        }
    }
}
