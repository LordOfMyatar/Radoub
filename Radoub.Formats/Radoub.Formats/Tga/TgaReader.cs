namespace Radoub.Formats.Tga;

/// <summary>
/// Reads TGA (Truevision Graphics Adapter) image files.
/// Supports uncompressed 24-bit (BGR) and 32-bit (BGRA) images.
/// Used primarily for NWN palette files (pal_skin01.tga, etc.).
/// </summary>
public static class TgaReader
{
    /// <summary>
    /// Read a TGA file and return the image data.
    /// </summary>
    /// <param name="data">Raw TGA file bytes</param>
    /// <returns>Parsed TGA image with width, height, and RGBA pixel data</returns>
    /// <exception cref="ArgumentException">If data is invalid or unsupported format</exception>
    public static TgaImage Read(byte[] data)
    {
        if (data == null || data.Length < 18)
            throw new ArgumentException("TGA data too small for header");

        // TGA Header (18 bytes)
        // Byte 0: ID length
        // Byte 1: Color map type (0 = no color map)
        // Byte 2: Image type (2 = uncompressed true-color)
        // Bytes 3-7: Color map specification (ignored)
        // Bytes 8-9: X origin
        // Bytes 10-11: Y origin
        // Bytes 12-13: Width
        // Bytes 14-15: Height
        // Byte 16: Pixel depth (24 or 32)
        // Byte 17: Image descriptor (bits 4-5 = origin)

        byte idLength = data[0];
        byte colorMapType = data[1];
        byte imageType = data[2];
        ushort width = BitConverter.ToUInt16(data, 12);
        ushort height = BitConverter.ToUInt16(data, 14);
        byte pixelDepth = data[16];
        byte imageDescriptor = data[17];

        // Validate format
        if (colorMapType != 0)
            throw new ArgumentException($"Color-mapped TGA not supported (type {colorMapType})");

        if (imageType != 2 && imageType != 10)
            throw new ArgumentException($"Unsupported TGA image type: {imageType} (only uncompressed/RLE true-color supported)");

        if (pixelDepth != 24 && pixelDepth != 32)
            throw new ArgumentException($"Unsupported pixel depth: {pixelDepth} (only 24 or 32 supported)");

        int bytesPerPixel = pixelDepth / 8;
        int pixelDataOffset = 18 + idLength;
        int expectedSize = pixelDataOffset + (width * height * bytesPerPixel);

        if (data.Length < expectedSize && imageType == 2)
            throw new ArgumentException($"TGA data too small: expected {expectedSize}, got {data.Length}");

        // Origin: bit 5 of image descriptor indicates top-to-bottom
        bool topToBottom = (imageDescriptor & 0x20) != 0;

        // Allocate RGBA output (always 4 bytes per pixel)
        byte[] pixels = new byte[width * height * 4];

        if (imageType == 2)
        {
            // Uncompressed
            ReadUncompressed(data, pixelDataOffset, width, height, bytesPerPixel, topToBottom, pixels);
        }
        else if (imageType == 10)
        {
            // RLE compressed
            ReadRleCompressed(data, pixelDataOffset, width, height, bytesPerPixel, topToBottom, pixels);
        }

        return new TgaImage(width, height, pixels);
    }

    private static void ReadUncompressed(byte[] data, int offset, int width, int height, int bytesPerPixel, bool topToBottom, byte[] pixels)
    {
        int srcIndex = offset;
        for (int y = 0; y < height; y++)
        {
            int destY = topToBottom ? y : (height - 1 - y);
            for (int x = 0; x < width; x++)
            {
                int destIndex = (destY * width + x) * 4;

                // TGA stores BGR(A), convert to RGBA
                byte b = data[srcIndex++];
                byte g = data[srcIndex++];
                byte r = data[srcIndex++];
                byte a = bytesPerPixel == 4 ? data[srcIndex++] : (byte)255;

                pixels[destIndex] = r;
                pixels[destIndex + 1] = g;
                pixels[destIndex + 2] = b;
                pixels[destIndex + 3] = a;
            }
        }
    }

    private static void ReadRleCompressed(byte[] data, int offset, int width, int height, int bytesPerPixel, bool topToBottom, byte[] pixels)
    {
        int srcIndex = offset;
        int totalPixels = width * height;
        int pixelCount = 0;

        while (pixelCount < totalPixels && srcIndex < data.Length)
        {
            byte header = data[srcIndex++];
            int count = (header & 0x7F) + 1;

            if ((header & 0x80) != 0)
            {
                // RLE packet - one pixel repeated
                byte b = data[srcIndex++];
                byte g = data[srcIndex++];
                byte r = data[srcIndex++];
                byte a = bytesPerPixel == 4 ? data[srcIndex++] : (byte)255;

                for (int i = 0; i < count && pixelCount < totalPixels; i++)
                {
                    int y = pixelCount / width;
                    int x = pixelCount % width;
                    int destY = topToBottom ? y : (height - 1 - y);
                    int destIndex = (destY * width + x) * 4;

                    pixels[destIndex] = r;
                    pixels[destIndex + 1] = g;
                    pixels[destIndex + 2] = b;
                    pixels[destIndex + 3] = a;

                    pixelCount++;
                }
            }
            else
            {
                // Raw packet - count pixels follow
                for (int i = 0; i < count && pixelCount < totalPixels; i++)
                {
                    byte b = data[srcIndex++];
                    byte g = data[srcIndex++];
                    byte r = data[srcIndex++];
                    byte a = bytesPerPixel == 4 ? data[srcIndex++] : (byte)255;

                    int y = pixelCount / width;
                    int x = pixelCount % width;
                    int destY = topToBottom ? y : (height - 1 - y);
                    int destIndex = (destY * width + x) * 4;

                    pixels[destIndex] = r;
                    pixels[destIndex + 1] = g;
                    pixels[destIndex + 2] = b;
                    pixels[destIndex + 3] = a;

                    pixelCount++;
                }
            }
        }
    }

    /// <summary>
    /// Get the color at a specific pixel coordinate.
    /// </summary>
    /// <param name="image">TGA image</param>
    /// <param name="x">X coordinate (0 to width-1)</param>
    /// <param name="y">Y coordinate (0 to height-1)</param>
    /// <returns>RGBA color tuple</returns>
    public static (byte r, byte g, byte b, byte a) GetPixel(TgaImage image, int x, int y)
    {
        if (x < 0 || x >= image.Width || y < 0 || y >= image.Height)
            return (128, 128, 128, 255); // Gray fallback for out of bounds

        int index = (y * image.Width + x) * 4;
        return (image.Pixels[index], image.Pixels[index + 1], image.Pixels[index + 2], image.Pixels[index + 3]);
    }
}

/// <summary>
/// Represents a parsed TGA image.
/// </summary>
public class TgaImage
{
    /// <summary>
    /// Image width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Image height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Pixel data in RGBA format (4 bytes per pixel).
    /// </summary>
    public byte[] Pixels { get; }

    public TgaImage(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }
}
