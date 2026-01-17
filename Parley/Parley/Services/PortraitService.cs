using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Tga;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service for loading NWN portrait images for display in Parley.
    /// Supports loading from loose files and base game data.
    /// #915: NPC portrait display in Node Properties.
    /// </summary>
    public class PortraitService
    {
        private static PortraitService? _instance;
        public static PortraitService Instance => _instance ??= new PortraitService();

        private readonly Dictionary<string, Bitmap?> _portraitCache = new();
        private string? _gameDataPath;
        private string? _moduleOverridePath;

        /// <summary>
        /// Sets the game data path for loading portraits from base game.
        /// </summary>
        public void SetGameDataPath(string? path)
        {
            if (_gameDataPath != path)
            {
                _gameDataPath = path;
                // Don't clear cache - portraits shouldn't change during session
            }
        }

        /// <summary>
        /// Sets the module override path for loading custom portraits.
        /// </summary>
        public void SetModuleOverridePath(string? path)
        {
            if (_moduleOverridePath != path)
            {
                _moduleOverridePath = path;
                // Clear cache when module changes as portraits might differ
                ClearCache();
            }
        }

        /// <summary>
        /// Loads a portrait image by its base ResRef.
        /// NWN portraits use naming: [baseResRef]s.tga (small), [baseResRef]m.tga (medium), [baseResRef]l.tga (large)
        /// </summary>
        /// <param name="baseResRef">Base portrait ResRef from portraits.2da (e.g., "po_elara")</param>
        /// <param name="size">Size suffix: 's' (small/thumbnail), 'm' (medium), 'l' (large). Default 's'.</param>
        /// <returns>Bitmap if found, null otherwise.</returns>
        public Bitmap? LoadPortrait(string? baseResRef, char size = 's')
        {
            if (string.IsNullOrEmpty(baseResRef))
                return null;

            // Build cache key
            var cacheKey = $"{baseResRef}{size}".ToLowerInvariant();

            if (_portraitCache.TryGetValue(cacheKey, out var cached))
                return cached;

            // Build filename: baseResRef + size suffix + .tga
            var fileName = $"{baseResRef}{size}.tga".ToLowerInvariant();

            // Search order: module override, game data portraits folder, game data root
            var searchPaths = new List<string>();

            if (!string.IsNullOrEmpty(_moduleOverridePath) && Directory.Exists(_moduleOverridePath))
            {
                searchPaths.Add(Path.Combine(_moduleOverridePath, fileName));
            }

            if (!string.IsNullOrEmpty(_gameDataPath) && Directory.Exists(_gameDataPath))
            {
                // NWN:EE stores portraits in data/portraits/ folder
                var portraitsFolder = Path.Combine(_gameDataPath, "portraits");
                if (Directory.Exists(portraitsFolder))
                    searchPaths.Add(Path.Combine(portraitsFolder, fileName));

                // Also check data/ directly
                searchPaths.Add(Path.Combine(_gameDataPath, fileName));
            }

            // Note: NWN override folder is typically in Documents or game install
            // For now, module override path is set separately via SetModuleOverridePath

            // Try to find and load the portrait
            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var bitmap = LoadTgaAsBitmap(path);
                        if (bitmap != null)
                        {
                            _portraitCache[cacheKey] = bitmap;
                            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Loaded portrait: {fileName} from {Path.GetDirectoryName(path)}");
                            return bitmap;
                        }
                    }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load portrait {fileName}: {ex.Message}");
                    }
                }
            }

            // Not found - cache as null to avoid repeated lookups
            _portraitCache[cacheKey] = null;
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Portrait not found: {fileName}");
            return null;
        }

        /// <summary>
        /// Loads a TGA file and converts it to an Avalonia Bitmap.
        /// </summary>
        private static Bitmap? LoadTgaAsBitmap(string path)
        {
            var tgaData = File.ReadAllBytes(path);
            if (tgaData.Length == 0)
                return null;

            var tgaImage = TgaReader.Read(tgaData);
            if (tgaImage.Width == 0 || tgaImage.Height == 0)
                return null;

            // Convert RGBA pixels to Avalonia Bitmap
            // TgaReader returns RGBA format
            using var stream = new MemoryStream();

            // Write BMP header + pixel data
            // Using simple 32-bit BMP format
            WriteBmpHeader(stream, tgaImage.Width, tgaImage.Height);
            WriteBmpPixels(stream, tgaImage.Width, tgaImage.Height, tgaImage.Pixels);

            stream.Position = 0;
            return new Bitmap(stream);
        }

        /// <summary>
        /// Writes BMP file header for 32-bit BGRA format.
        /// </summary>
        private static void WriteBmpHeader(Stream stream, int width, int height)
        {
            var writer = new BinaryWriter(stream);

            // BMP File Header (14 bytes)
            var pixelDataSize = width * height * 4;
            var fileSize = 14 + 108 + pixelDataSize; // BITMAPV4HEADER is 108 bytes

            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(fileSize);
            writer.Write((short)0); // Reserved
            writer.Write((short)0); // Reserved
            writer.Write(14 + 108); // Pixel data offset (BITMAPV4HEADER)

            // BITMAPV4HEADER (108 bytes)
            writer.Write(108); // Header size
            writer.Write(width);
            writer.Write(-height); // Negative for top-down DIB
            writer.Write((short)1); // Planes
            writer.Write((short)32); // Bits per pixel
            writer.Write(3); // Compression: BI_BITFIELDS
            writer.Write(pixelDataSize);
            writer.Write(2835); // X pixels per meter (72 DPI)
            writer.Write(2835); // Y pixels per meter (72 DPI)
            writer.Write(0); // Colors used
            writer.Write(0); // Important colors

            // Color masks for BGRA
            writer.Write(0x00FF0000); // Red mask
            writer.Write(0x0000FF00); // Green mask
            writer.Write(0x000000FF); // Blue mask
            writer.Write(0xFF000000); // Alpha mask

            // Color space type: LCS_sRGB
            writer.Write(0x73524742);

            // CIEXYZTRIPLE endpoints (36 bytes) - not used for sRGB
            for (int i = 0; i < 9; i++)
                writer.Write(0);

            // Gamma values - not used for sRGB
            writer.Write(0); // Red gamma
            writer.Write(0); // Green gamma
            writer.Write(0); // Blue gamma
        }

        /// <summary>
        /// Writes pixel data in BGRA format.
        /// </summary>
        private static void WriteBmpPixels(Stream stream, int width, int height, byte[] rgbaPixels)
        {
            // Convert RGBA to BGRA and write
            for (int i = 0; i < rgbaPixels.Length; i += 4)
            {
                var r = rgbaPixels[i];
                var g = rgbaPixels[i + 1];
                var b = rgbaPixels[i + 2];
                var a = rgbaPixels[i + 3];

                stream.WriteByte(b); // Blue
                stream.WriteByte(g); // Green
                stream.WriteByte(r); // Red
                stream.WriteByte(a); // Alpha
            }
        }

        /// <summary>
        /// Clears the portrait cache.
        /// </summary>
        public void ClearCache()
        {
            foreach (var bitmap in _portraitCache.Values)
            {
                bitmap?.Dispose();
            }
            _portraitCache.Clear();
        }
    }
}
