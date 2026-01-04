namespace Radoub.Formats.Services;

/// <summary>
/// Service for loading and decoding NWN image assets.
/// Supports TGA, DDS, and PLT (palette texture) formats.
/// </summary>
public interface IImageService
{
    /// <summary>
    /// Decode an image from raw bytes.
    /// Supports TGA, DDS, and PLT formats.
    /// </summary>
    /// <param name="data">Raw image file bytes</param>
    /// <param name="format">Image format hint (tga, dds, plt)</param>
    /// <returns>Decoded image, or null if decoding fails</returns>
    ImageData? DecodeImage(byte[] data, string format);

    /// <summary>
    /// Decode an image from a game resource.
    /// </summary>
    /// <param name="resRef">Resource reference name</param>
    /// <param name="resourceType">Resource type ID (3=TGA, 6=PLT, 2033=DDS)</param>
    /// <returns>Decoded image, or null if not found or decoding fails</returns>
    ImageData? LoadImage(string resRef, ushort resourceType);

    /// <summary>
    /// Get the item inventory icon for a base item type.
    /// Uses baseitems.2da to look up icon ResRef patterns.
    /// </summary>
    /// <param name="baseItemType">Base item type ID from baseitems.2da</param>
    /// <param name="modelNumber">Model variation number (for ranged icons)</param>
    /// <returns>Decoded icon image, or null if not found</returns>
    ImageData? GetItemIcon(int baseItemType, int modelNumber = 0);

    /// <summary>
    /// Get a portrait image by ResRef.
    /// </summary>
    /// <param name="resRef">Portrait ResRef (e.g., "po_elf_m_")</param>
    /// <returns>Decoded portrait image, or null if not found</returns>
    ImageData? GetPortrait(string resRef);

    /// <summary>
    /// Get a spell icon by spell ID.
    /// Uses spells.2da IconResRef column.
    /// </summary>
    /// <param name="spellId">Spell ID from spells.2da</param>
    /// <returns>Decoded icon image, or null if not found</returns>
    ImageData? GetSpellIcon(int spellId);

    /// <summary>
    /// Get a feat icon by feat ID.
    /// Uses feat.2da ICON column.
    /// </summary>
    /// <param name="featId">Feat ID from feat.2da</param>
    /// <returns>Decoded icon image, or null if not found</returns>
    ImageData? GetFeatIcon(int featId);

    /// <summary>
    /// Get a skill icon by skill ID.
    /// Uses skills.2da Icon column.
    /// </summary>
    /// <param name="skillId">Skill ID from skills.2da</param>
    /// <returns>Decoded icon image, or null if not found</returns>
    ImageData? GetSkillIcon(int skillId);

    /// <summary>
    /// Get a class icon by class ID.
    /// Uses classes.2da Icon column.
    /// </summary>
    /// <param name="classId">Class ID from classes.2da</param>
    /// <returns>Decoded icon image, or null if not found</returns>
    ImageData? GetClassIcon(int classId);

    /// <summary>
    /// Clear the image cache.
    /// Call when resource paths change.
    /// </summary>
    void ClearCache();
}

/// <summary>
/// Decoded image data in RGBA format.
/// </summary>
public class ImageData
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
    /// Row order is top-to-bottom.
    /// </summary>
    public byte[] Pixels { get; }

    public ImageData(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    /// <summary>
    /// Get the color at a specific pixel coordinate.
    /// </summary>
    public (byte r, byte g, byte b, byte a) GetPixel(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return (128, 128, 128, 255); // Gray fallback for out of bounds

        int index = (y * Width + x) * 4;
        return (Pixels[index], Pixels[index + 1], Pixels[index + 2], Pixels[index + 3]);
    }
}
