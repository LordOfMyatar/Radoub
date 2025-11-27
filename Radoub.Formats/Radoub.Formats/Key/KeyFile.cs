namespace Radoub.Formats.Key;

/// <summary>
/// Represents a KEY file that indexes resources in BIF archives.
/// Reference: BioWare Aurora KEY format spec, neverwinter.nim key.nim
/// </summary>
public class KeyFile
{
    /// <summary>
    /// File signature - should be "KEY "
    /// </summary>
    public string FileType { get; set; } = "KEY ";

    /// <summary>
    /// File version - should be "V1  "
    /// </summary>
    public string FileVersion { get; set; } = "V1  ";

    /// <summary>
    /// List of BIF files referenced by this KEY
    /// </summary>
    public List<KeyBifEntry> BifEntries { get; set; } = new();

    /// <summary>
    /// List of resource entries (keys into BIF files)
    /// </summary>
    public List<KeyResourceEntry> ResourceEntries { get; set; } = new();

    /// <summary>
    /// Find a resource by ResRef and type.
    /// </summary>
    public KeyResourceEntry? FindResource(string resRef, ushort resourceType)
    {
        return ResourceEntries.FirstOrDefault(r =>
            r.ResRef.Equals(resRef, StringComparison.OrdinalIgnoreCase) &&
            r.ResourceType == resourceType);
    }

    /// <summary>
    /// Get all resources of a specific type.
    /// </summary>
    public IEnumerable<KeyResourceEntry> GetResourcesByType(ushort resourceType)
    {
        return ResourceEntries.Where(r => r.ResourceType == resourceType);
    }

    /// <summary>
    /// Get the BIF entry for a resource.
    /// </summary>
    public KeyBifEntry? GetBifForResource(KeyResourceEntry resource)
    {
        var bifIndex = resource.BifIndex;
        return bifIndex < BifEntries.Count ? BifEntries[bifIndex] : null;
    }
}

/// <summary>
/// Entry describing a BIF file in the KEY.
/// </summary>
public class KeyBifEntry
{
    /// <summary>
    /// Size of the BIF file in bytes.
    /// </summary>
    public uint FileSize { get; set; }

    /// <summary>
    /// Offset to filename in the KEY file (internal use).
    /// </summary>
    public uint FilenameOffset { get; set; }

    /// <summary>
    /// Length of the filename string.
    /// </summary>
    public ushort FilenameLength { get; set; }

    /// <summary>
    /// Drive flags (typically 1 for CD1).
    /// </summary>
    public ushort Drives { get; set; }

    /// <summary>
    /// The filename/path of the BIF file (relative to game directory).
    /// </summary>
    public string Filename { get; set; } = string.Empty;
}

/// <summary>
/// Entry describing a resource in the KEY.
/// </summary>
public class KeyResourceEntry
{
    /// <summary>
    /// Resource reference name (max 16 characters, case-insensitive).
    /// </summary>
    public string ResRef { get; set; } = string.Empty;

    /// <summary>
    /// Resource type identifier (see ResourceTypes).
    /// </summary>
    public ushort ResourceType { get; set; }

    /// <summary>
    /// Resource ID encoding BIF index and variable table index.
    /// Format: (bifIndex &lt;&lt; 20) | variableTableIndex
    /// </summary>
    public uint ResId { get; set; }

    /// <summary>
    /// Extract the BIF index from ResId.
    /// Top 12 bits of ResId.
    /// </summary>
    public int BifIndex => (int)(ResId >> 20);

    /// <summary>
    /// Extract the variable table index within the BIF from ResId.
    /// Bottom 20 bits of ResId.
    /// </summary>
    public int VariableTableIndex => (int)(ResId & 0xFFFFF);
}
