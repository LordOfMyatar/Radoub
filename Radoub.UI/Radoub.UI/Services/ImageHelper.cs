using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using SkiaSharp;
using System;
using System.IO;

namespace Radoub.UI.Services;

/// <summary>
/// Centralized helper for image sizing and portrait handling across Radoub tools.
/// Provides standard NWN portrait sizes and consistent resize/scaling algorithms.
/// Issue #972 - Part of Epic #959 (UI Uniformity).
/// </summary>
public static class ImageHelper
{
    #region Standard Portrait Sizes

    /// <summary>
    /// Tiny portrait size (32x40) - Used for inventory icons.
    /// </summary>
    public static readonly Size PortraitTiny = new(32, 40);

    /// <summary>
    /// Small portrait size (64x100) - Used for party bar, dialog speaker.
    /// </summary>
    public static readonly Size PortraitSmall = new(64, 100);

    /// <summary>
    /// Medium portrait size (128x200) - Used for character sheet.
    /// </summary>
    public static readonly Size PortraitMedium = new(128, 200);

    /// <summary>
    /// Large portrait size (256x400) - Used for full portrait view.
    /// </summary>
    public static readonly Size PortraitLarge = new(256, 400);

    /// <summary>
    /// Huge portrait size (512x800) - Used for HD portrait mods.
    /// </summary>
    public static readonly Size PortraitHuge = new(512, 800);

    /// <summary>
    /// Standard NWN portrait aspect ratio (width:height = 0.64 or 16:25).
    /// Note: Tiny size (32x40) has a different ratio (0.8) for inventory icons.
    /// </summary>
    public const double PortraitAspectRatio = 0.64;

    #endregion

    #region Size Calculation

    /// <summary>
    /// Calculates the target size that fits within bounds while preserving aspect ratio.
    /// </summary>
    /// <param name="sourceSize">Original image size</param>
    /// <param name="targetBounds">Maximum bounds to fit within</param>
    /// <param name="preserveAspect">If true, preserve aspect ratio (default). If false, stretch to fill.</param>
    /// <returns>Calculated size that fits within bounds</returns>
    public static Size CalculateFitSize(Size sourceSize, Size targetBounds, bool preserveAspect = true)
    {
        if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
            return targetBounds;

        if (!preserveAspect)
            return targetBounds;

        double sourceAspect = sourceSize.Width / sourceSize.Height;
        double targetAspect = targetBounds.Width / targetBounds.Height;

        double newWidth, newHeight;

        if (sourceAspect > targetAspect)
        {
            // Source is wider - constrain by width
            newWidth = targetBounds.Width;
            newHeight = newWidth / sourceAspect;
        }
        else
        {
            // Source is taller - constrain by height
            newHeight = targetBounds.Height;
            newWidth = newHeight * sourceAspect;
        }

        return new Size(Math.Round(newWidth), Math.Round(newHeight));
    }

    /// <summary>
    /// Calculates portrait size for a given panel width, maintaining standard aspect ratio.
    /// </summary>
    /// <param name="availableWidth">Available width in pixels</param>
    /// <returns>Size with correct portrait aspect ratio</returns>
    public static Size CalculatePortraitSize(double availableWidth)
    {
        double height = availableWidth / PortraitAspectRatio;
        return new Size(Math.Round(availableWidth), Math.Round(height));
    }

    /// <summary>
    /// Gets the nearest standard portrait size for a given dimension.
    /// </summary>
    /// <param name="width">Target width</param>
    /// <returns>Nearest standard portrait size</returns>
    public static Size GetNearestPortraitSize(double width)
    {
        if (width <= 48) return PortraitTiny;
        if (width <= 96) return PortraitSmall;
        if (width <= 192) return PortraitMedium;
        if (width <= 384) return PortraitLarge;
        return PortraitHuge;
    }

    #endregion

    #region Bitmap Resizing

    /// <summary>
    /// Resizes a bitmap to the target size with high quality filtering.
    /// </summary>
    /// <param name="source">Source bitmap</param>
    /// <param name="targetSize">Target size</param>
    /// <param name="preserveAspect">If true, maintain aspect ratio within target bounds</param>
    /// <returns>Resized bitmap, or null if source is null</returns>
    public static Bitmap? ResizeBitmap(Bitmap? source, Size targetSize, bool preserveAspect = true)
    {
        if (source == null)
            return null;

        var sourceSize = new Size(source.PixelSize.Width, source.PixelSize.Height);
        var finalSize = preserveAspect
            ? CalculateFitSize(sourceSize, targetSize, true)
            : targetSize;

        int targetWidth = (int)Math.Max(1, finalSize.Width);
        int targetHeight = (int)Math.Max(1, finalSize.Height);

        // Use SkiaSharp for high-quality resize
        using var ms = new MemoryStream();
        source.Save(ms);
        ms.Position = 0;

        using var skBitmap = SKBitmap.Decode(ms);
        if (skBitmap == null)
            return null;

        using var resized = skBitmap.Resize(
            new SKImageInfo(targetWidth, targetHeight),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

        if (resized == null)
            return null;

        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var resultStream = new MemoryStream(data.ToArray());

        return new Bitmap(resultStream);
    }

    /// <summary>
    /// Resizes a portrait to a standard size with high quality filtering.
    /// </summary>
    /// <param name="source">Source portrait bitmap</param>
    /// <param name="targetSize">Target portrait size (use PortraitSmall, PortraitMedium, etc.)</param>
    /// <returns>Resized portrait bitmap</returns>
    public static Bitmap? ResizePortrait(Bitmap? source, Size targetSize)
    {
        return ResizeBitmap(source, targetSize, preserveAspect: true);
    }

    #endregion

    #region Missing Portrait Placeholder

    private static Bitmap? _missingPortraitPlaceholder;
    private static readonly object _placeholderLock = new();

    /// <summary>
    /// Gets a placeholder image for missing portraits.
    /// Returns a simple gray silhouette at the requested size.
    /// </summary>
    /// <param name="size">Desired size (default: PortraitSmall)</param>
    /// <returns>Placeholder bitmap</returns>
    public static Bitmap GetMissingPortraitPlaceholder(Size? size = null)
    {
        var targetSize = size ?? PortraitSmall;

        // Generate placeholder on demand
        lock (_placeholderLock)
        {
            if (_missingPortraitPlaceholder != null)
            {
                // Resize cached placeholder to requested size
                var resized = ResizeBitmap(_missingPortraitPlaceholder, targetSize, preserveAspect: false);
                if (resized != null)
                    return resized;
            }

            // Create a simple placeholder - gray background with darker silhouette
            _missingPortraitPlaceholder = CreatePlaceholderBitmap();

            var result = ResizeBitmap(_missingPortraitPlaceholder, targetSize, preserveAspect: false);
            return result ?? _missingPortraitPlaceholder;
        }
    }

    /// <summary>
    /// Creates the base placeholder bitmap (at Medium size for quality).
    /// Simple gray silhouette design.
    /// </summary>
    private static Bitmap CreatePlaceholderBitmap()
    {
        int width = (int)PortraitMedium.Width;
        int height = (int)PortraitMedium.Height;

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        // Background - neutral gray
        canvas.Clear(new SKColor(64, 64, 64));

        // Draw simple head/shoulders silhouette
        using var silhouettePaint = new SKPaint
        {
            Color = new SKColor(48, 48, 48),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        // Head (oval at top center)
        float headCenterX = width / 2f;
        float headCenterY = height * 0.28f;
        float headRadiusX = width * 0.22f;
        float headRadiusY = height * 0.14f;
        canvas.DrawOval(headCenterX, headCenterY, headRadiusX, headRadiusY, silhouettePaint);

        // Shoulders (trapezoid shape)
        using var path = new SKPath();
        float shoulderTop = height * 0.45f;
        float shoulderBottom = height;
        float neckWidth = width * 0.25f;
        float shoulderWidth = width * 0.9f;

        path.MoveTo(headCenterX - neckWidth / 2, shoulderTop);
        path.LineTo(headCenterX - shoulderWidth / 2, shoulderBottom);
        path.LineTo(headCenterX + shoulderWidth / 2, shoulderBottom);
        path.LineTo(headCenterX + neckWidth / 2, shoulderTop);
        path.Close();
        canvas.DrawPath(path, silhouettePaint);

        // Draw "?" in center
        using var textPaint = new SKPaint
        {
            Color = new SKColor(96, 96, 96),
            IsAntialias = true
        };
        using var font = new SKFont(SKTypeface.Default, height * 0.15f);
        canvas.DrawText("?", headCenterX, height * 0.72f, SKTextAlign.Center, font, textPaint);

        // Convert to Bitmap
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var ms = new MemoryStream(data.ToArray());

        return new Bitmap(ms);
    }

    /// <summary>
    /// Clears the cached placeholder bitmap.
    /// Call when theme changes or on app shutdown.
    /// </summary>
    public static void ClearPlaceholderCache()
    {
        lock (_placeholderLock)
        {
            _missingPortraitPlaceholder?.Dispose();
            _missingPortraitPlaceholder = null;
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Validates that an image has valid dimensions.
    /// </summary>
    /// <param name="bitmap">Bitmap to validate</param>
    /// <returns>True if bitmap has valid non-zero dimensions</returns>
    public static bool IsValidBitmap(Bitmap? bitmap)
    {
        return bitmap != null &&
               bitmap.PixelSize.Width > 0 &&
               bitmap.PixelSize.Height > 0;
    }

    /// <summary>
    /// Gets the aspect ratio of a bitmap.
    /// </summary>
    /// <param name="bitmap">Bitmap to measure</param>
    /// <returns>Width/Height ratio, or 1.0 if bitmap is null/invalid</returns>
    public static double GetAspectRatio(Bitmap? bitmap)
    {
        if (!IsValidBitmap(bitmap))
            return 1.0;

        return (double)bitmap!.PixelSize.Width / bitmap.PixelSize.Height;
    }

    /// <summary>
    /// Checks if a bitmap appears to be a portrait (roughly 5:8 aspect ratio).
    /// </summary>
    /// <param name="bitmap">Bitmap to check</param>
    /// <param name="tolerance">Aspect ratio tolerance (default 0.1)</param>
    /// <returns>True if bitmap has portrait-like aspect ratio</returns>
    public static bool IsPortraitAspect(Bitmap? bitmap, double tolerance = 0.1)
    {
        var aspect = GetAspectRatio(bitmap);
        return Math.Abs(aspect - PortraitAspectRatio) < tolerance;
    }

    #endregion
}
