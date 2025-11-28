namespace Radoub.Formats.Resolver;

/// <summary>
/// Source of a resolved resource.
/// </summary>
public enum ResourceSource
{
    /// <summary>Resource found in override folder.</summary>
    Override,

    /// <summary>Resource found in a HAK file.</summary>
    Hak,

    /// <summary>Resource found in base game BIF.</summary>
    Bif
}

/// <summary>
/// Result of a resource lookup including source information.
/// </summary>
public class ResourceResult
{
    /// <summary>
    /// The resource data.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Where the resource was found.
    /// </summary>
    public ResourceSource Source { get; }

    /// <summary>
    /// Path to the file containing the resource.
    /// For Override: the file path. For HAK/BIF: the archive path.
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    /// The ResRef of the resource.
    /// </summary>
    public string ResRef { get; }

    /// <summary>
    /// The resource type.
    /// </summary>
    public ushort ResourceType { get; }

    public ResourceResult(byte[] data, ResourceSource source, string sourcePath, string resRef, ushort resourceType)
    {
        Data = data;
        Source = source;
        SourcePath = sourcePath;
        ResRef = resRef;
        ResourceType = resourceType;
    }
}

/// <summary>
/// Information about an available resource (without loading data).
/// </summary>
public class ResourceInfo
{
    /// <summary>
    /// The ResRef of the resource.
    /// </summary>
    public string ResRef { get; set; } = string.Empty;

    /// <summary>
    /// The resource type.
    /// </summary>
    public ushort ResourceType { get; set; }

    /// <summary>
    /// Where the resource is located.
    /// </summary>
    public ResourceSource Source { get; set; }

    /// <summary>
    /// Path to the source file/archive.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;
}
