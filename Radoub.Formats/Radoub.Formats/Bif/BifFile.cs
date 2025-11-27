namespace Radoub.Formats.Bif;

/// <summary>
/// Represents a BIF archive file containing game resources.
/// Reference: BioWare Aurora BIF format spec, neverwinter.nim bif.nim
/// </summary>
public class BifFile
{
    /// <summary>
    /// File signature - should be "BIFF"
    /// </summary>
    public string FileType { get; set; } = "BIFF";

    /// <summary>
    /// File version - should be "V1  "
    /// </summary>
    public string FileVersion { get; set; } = "V1  ";

    /// <summary>
    /// Variable resource entries (most common).
    /// </summary>
    public List<BifVariableResource> VariableResources { get; set; } = new();

    /// <summary>
    /// Fixed resource entries (less common, used for tiles).
    /// </summary>
    public List<BifFixedResource> FixedResources { get; set; } = new();

    /// <summary>
    /// The raw file buffer (kept for resource extraction).
    /// </summary>
    internal byte[]? RawBuffer { get; set; }

    /// <summary>
    /// Get a variable resource by its index.
    /// </summary>
    public BifVariableResource? GetVariableResource(int index)
    {
        return index >= 0 && index < VariableResources.Count ? VariableResources[index] : null;
    }

    /// <summary>
    /// Extract a variable resource's raw data.
    /// </summary>
    public byte[]? ExtractVariableResource(int index)
    {
        if (RawBuffer == null)
            return null;

        var resource = GetVariableResource(index);
        if (resource == null)
            return null;

        if (resource.Offset + resource.FileSize > RawBuffer.Length)
            return null;

        var data = new byte[resource.FileSize];
        Array.Copy(RawBuffer, resource.Offset, data, 0, resource.FileSize);
        return data;
    }

    /// <summary>
    /// Extract a variable resource's raw data.
    /// </summary>
    public byte[]? ExtractVariableResource(BifVariableResource resource)
    {
        if (RawBuffer == null)
            return null;

        if (resource.Offset + resource.FileSize > RawBuffer.Length)
            return null;

        var data = new byte[resource.FileSize];
        Array.Copy(RawBuffer, resource.Offset, data, 0, resource.FileSize);
        return data;
    }
}

/// <summary>
/// Variable-size resource entry in a BIF file.
/// </summary>
public class BifVariableResource
{
    /// <summary>
    /// Resource ID (matches ResId from KEY file).
    /// Contains BIF index in top 12 bits, variable table index in bottom 20 bits.
    /// </summary>
    public uint Id { get; set; }

    /// <summary>
    /// Offset to resource data within the BIF file.
    /// </summary>
    public uint Offset { get; set; }

    /// <summary>
    /// Size of the resource data in bytes.
    /// </summary>
    public uint FileSize { get; set; }

    /// <summary>
    /// Resource type identifier.
    /// </summary>
    public uint ResourceType { get; set; }

    /// <summary>
    /// Extract the variable table index from the ID.
    /// </summary>
    public int VariableTableIndex => (int)(Id & 0xFFFFF);
}

/// <summary>
/// Fixed-size resource entry in a BIF file (typically used for tiles).
/// </summary>
public class BifFixedResource
{
    /// <summary>
    /// Resource ID.
    /// </summary>
    public uint Id { get; set; }

    /// <summary>
    /// Offset to resource data within the BIF file.
    /// </summary>
    public uint Offset { get; set; }

    /// <summary>
    /// Number of parts/elements.
    /// </summary>
    public uint PartCount { get; set; }

    /// <summary>
    /// Size of each part in bytes.
    /// </summary>
    public uint PartSize { get; set; }

    /// <summary>
    /// Resource type identifier.
    /// </summary>
    public uint ResourceType { get; set; }

    /// <summary>
    /// Total size of the resource (PartCount * PartSize).
    /// </summary>
    public uint TotalSize => PartCount * PartSize;
}
