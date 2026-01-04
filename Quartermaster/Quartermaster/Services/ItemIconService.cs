using System;
using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Radoub.Formats.Logging;
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
    /// Get a spell icon.
    /// </summary>
    /// <param name="spellId">Spell ID from spells.2da</param>
    /// <returns>Avalonia Bitmap, or null if not found</returns>
    public Bitmap? GetSpellIcon(int spellId)
    {
        return GetCachedIcon($"spell:{spellId}", () => _imageService.GetSpellIcon(spellId));
    }

    /// <summary>
    /// Get a feat icon.
    /// </summary>
    /// <param name="featId">Feat ID from feat.2da</param>
    /// <returns>Avalonia Bitmap, or null if not found</returns>
    public Bitmap? GetFeatIcon(int featId)
    {
        return GetCachedIcon($"feat:{featId}", () => _imageService.GetFeatIcon(featId));
    }

    /// <summary>
    /// Get a skill icon.
    /// </summary>
    /// <param name="skillId">Skill ID from skills.2da</param>
    /// <returns>Avalonia Bitmap, or null if not found</returns>
    public Bitmap? GetSkillIcon(int skillId)
    {
        return GetCachedIcon($"skill:{skillId}", () => _imageService.GetSkillIcon(skillId));
    }

    /// <summary>
    /// Get a class icon.
    /// </summary>
    /// <param name="classId">Class ID from classes.2da</param>
    /// <returns>Avalonia Bitmap, or null if not found</returns>
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

        // Limit cache size
        if (_bitmapCache.Count > MaxCacheSize)
        {
            UnifiedLogger.Log(LogLevel.DEBUG, $"ItemIconService: Cache full ({_bitmapCache.Count}), clearing...", "UI", "[UI]");
            ClearCache();
        }

        try
        {
            UnifiedLogger.Log(LogLevel.DEBUG, $"ItemIconService: Loading {cacheKey}", "UI", "[UI]");
            var imageData = loader();
            Bitmap? bitmap = null;

            if (imageData != null)
            {
                UnifiedLogger.Log(LogLevel.DEBUG, $"ItemIconService: Got imageData {imageData.Width}x{imageData.Height} for {cacheKey}", "UI", "[UI]");
                bitmap = ImageDataToBitmap(imageData);
                UnifiedLogger.Log(LogLevel.DEBUG, $"ItemIconService: Converted to bitmap for {cacheKey}: {(bitmap != null ? "success" : "null")}", "UI", "[UI]");
            }
            else
            {
                UnifiedLogger.Log(LogLevel.DEBUG, $"ItemIconService: No imageData for {cacheKey}", "UI", "[UI]");
            }

            _bitmapCache.TryAdd(cacheKey, bitmap);
            return bitmap;
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.ERROR, $"ItemIconService: Exception for {cacheKey}: {ex.Message}", "UI", "[UI]");
            // Silently cache null on failure to prevent repeated attempts
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
            // Validate input
            if (imageData.Width <= 0 || imageData.Height <= 0)
            {
                UnifiedLogger.Log(LogLevel.WARN, $"ImageDataToBitmap: Invalid dimensions {imageData.Width}x{imageData.Height}", "UI", "[UI]");
                return null;
            }

            int expectedSize = imageData.Width * imageData.Height * 4;
            if (imageData.Pixels == null || imageData.Pixels.Length != expectedSize)
            {
                UnifiedLogger.Log(LogLevel.WARN, $"ImageDataToBitmap: Invalid pixel data - expected {expectedSize}, got {imageData.Pixels?.Length ?? 0}", "UI", "[UI]");
                return null;
            }

            // Create SkiaSharp bitmap from RGBA data
            var info = new SKImageInfo(imageData.Width, imageData.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var skBitmap = new SKBitmap(info);

            // Copy pixel data
            var pixels = skBitmap.GetPixels();
            if (pixels == IntPtr.Zero)
            {
                UnifiedLogger.Log(LogLevel.WARN, "ImageDataToBitmap: GetPixels returned null", "UI", "[UI]");
                return null;
            }

            System.Runtime.InteropServices.Marshal.Copy(imageData.Pixels, 0, pixels, imageData.Pixels.Length);

            // Encode to PNG in memory
            using var image = SKImage.FromBitmap(skBitmap);
            if (image == null)
            {
                UnifiedLogger.Log(LogLevel.WARN, "ImageDataToBitmap: SKImage.FromBitmap returned null", "UI", "[UI]");
                return null;
            }

            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            if (data == null)
            {
                UnifiedLogger.Log(LogLevel.WARN, "ImageDataToBitmap: Encode returned null", "UI", "[UI]");
                return null;
            }

            using var stream = new System.IO.MemoryStream(data.ToArray());

            // Create Avalonia bitmap from PNG
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.ERROR, $"ImageDataToBitmap: Exception: {ex.Message}", "UI", "[UI]");
            return null;
        }
    }
}
