namespace Radoub.Formats.Plt;

/// <summary>
/// Reads PLT (Packed Layered Texture) files.
/// NWN-specific format for color-customizable textures.
/// </summary>
public static class PltReader
{
    /// <summary>
    /// Read a PLT file and return the raw layer data.
    /// </summary>
    /// <param name="data">Raw PLT file bytes</param>
    /// <returns>Parsed PLT file with layer information</returns>
    /// <exception cref="ArgumentException">If data is invalid</exception>
    public static PltFile Read(byte[] data)
    {
        if (data == null || data.Length < 24)
            throw new ArgumentException("PLT data too small for header");

        // PLT Header (24 bytes):
        // Bytes 0-3: "PLT " signature
        // Bytes 4-7: "V1  " version
        // Bytes 8-15: Unused
        // Bytes 16-19: Width (little-endian)
        // Bytes 20-23: Height (little-endian)

        string signature = System.Text.Encoding.ASCII.GetString(data, 0, 4);
        if (signature != "PLT ")
            throw new ArgumentException($"Invalid PLT signature: '{signature}'");

        string version = System.Text.Encoding.ASCII.GetString(data, 4, 4);
        if (!version.StartsWith("V1"))
            throw new ArgumentException($"Unsupported PLT version: '{version}'");

        uint width = BitConverter.ToUInt32(data, 16);
        uint height = BitConverter.ToUInt32(data, 20);

        if (width == 0 || height == 0 || width > 4096 || height > 4096)
            throw new ArgumentException($"Invalid PLT dimensions: {width}x{height}");

        int expectedSize = 24 + (int)(width * height * 2);
        if (data.Length < expectedSize)
            throw new ArgumentException($"PLT data too small: expected {expectedSize}, got {data.Length}");

        // Read pixel data (2 bytes per pixel)
        var pixels = new PltPixel[width * height];
        int offset = 24;
        for (int i = 0; i < pixels.Length; i++)
        {
            byte grayscale = data[offset++];
            byte layerId = data[offset++];
            pixels[i] = new PltPixel(grayscale, layerId);
        }

        return new PltFile((int)width, (int)height, pixels);
    }

    /// <summary>
    /// Render a PLT file to RGBA pixels using the specified palette colors.
    /// </summary>
    /// <param name="plt">Parsed PLT file</param>
    /// <param name="palettes">Palette images for each layer (indexed by layer ID)</param>
    /// <param name="colorIndices">Color index for each layer (row in palette, 0-175)</param>
    /// <returns>RGBA pixel data (4 bytes per pixel)</returns>
    public static byte[] Render(PltFile plt, Dictionary<int, PaletteData> palettes, Dictionary<int, int> colorIndices)
    {
        byte[] output = new byte[plt.Width * plt.Height * 4];

        for (int i = 0; i < plt.Pixels.Length; i++)
        {
            var pixel = plt.Pixels[i];
            int destIndex = i * 4;

            // Get the palette for this layer
            if (!palettes.TryGetValue(pixel.LayerId, out var palette))
            {
                // No palette for this layer, use grayscale
                output[destIndex] = pixel.Grayscale;
                output[destIndex + 1] = pixel.Grayscale;
                output[destIndex + 2] = pixel.Grayscale;
                output[destIndex + 3] = 255;
                continue;
            }

            // Get the color index for this layer
            if (!colorIndices.TryGetValue(pixel.LayerId, out int colorIndex))
            {
                colorIndex = 0; // Default to first color
            }

            // Look up color: palette is 256 wide (grayscale values) x N tall (color options)
            // X = grayscale value, Y = color index
            var (r, g, b, a) = palette.GetColor(pixel.Grayscale, colorIndex);
            output[destIndex] = r;
            output[destIndex + 1] = g;
            output[destIndex + 2] = b;
            output[destIndex + 3] = a;
        }

        return output;
    }
}

/// <summary>
/// Represents a parsed PLT file.
/// </summary>
public class PltFile
{
    public int Width { get; }
    public int Height { get; }
    public PltPixel[] Pixels { get; }

    public PltFile(int width, int height, PltPixel[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }
}

/// <summary>
/// A single pixel in a PLT file.
/// </summary>
public readonly struct PltPixel
{
    /// <summary>
    /// Grayscale intensity value (0-255).
    /// Used as X coordinate in palette lookup.
    /// </summary>
    public byte Grayscale { get; }

    /// <summary>
    /// Layer ID (0-9) indicating which palette to use.
    /// </summary>
    public byte LayerId { get; }

    public PltPixel(byte grayscale, byte layerId)
    {
        Grayscale = grayscale;
        LayerId = layerId;
    }
}

/// <summary>
/// PLT layer identifiers.
/// </summary>
public static class PltLayers
{
    public const int Skin = 0;
    public const int Hair = 1;
    public const int Metal1 = 2;
    public const int Metal2 = 3;
    public const int Cloth1 = 4;
    public const int Cloth2 = 5;
    public const int Leather1 = 6;
    public const int Leather2 = 7;
    public const int Tattoo1 = 8;
    public const int Tattoo2 = 9;

    /// <summary>
    /// Get the palette filename for a layer.
    /// </summary>
    public static string GetPaletteResRef(int layerId) => layerId switch
    {
        Skin => "pal_skin01",
        Hair => "pal_hair01",
        Metal1 => "pal_armor01",
        Metal2 => "pal_armor02",
        Cloth1 => "pal_cloth01",
        Cloth2 => "pal_cloth02",
        Leather1 => "pal_leath01",
        Leather2 => "pal_leath02",
        Tattoo1 => "pal_tattoo01",
        Tattoo2 => "pal_tattoo02",
        _ => "pal_skin01"
    };
}

/// <summary>
/// Palette data loaded from pal_*.tga files.
/// </summary>
public class PaletteData
{
    private readonly byte[] _pixels; // RGBA format
    private readonly int _width;
    private readonly int _height;

    public PaletteData(int width, int height, byte[] pixels)
    {
        _width = width;
        _height = height;
        _pixels = pixels;
    }

    /// <summary>
    /// Get a color from the palette.
    /// </summary>
    /// <param name="grayscale">Grayscale value (0-255, X coordinate)</param>
    /// <param name="colorIndex">Color option (0-N, Y coordinate)</param>
    public (byte r, byte g, byte b, byte a) GetColor(int grayscale, int colorIndex)
    {
        // Clamp to valid range
        int x = Math.Clamp(grayscale, 0, _width - 1);
        int y = Math.Clamp(colorIndex, 0, _height - 1);

        int index = (y * _width + x) * 4;
        if (index + 3 >= _pixels.Length)
            return (128, 128, 128, 255);

        return (_pixels[index], _pixels[index + 1], _pixels[index + 2], _pixels[index + 3]);
    }
}
