namespace Radoub.Formats.Erf;

/// <summary>
/// Represents an ERF (Encapsulated Resource File) which is used for HAK, MOD, ERF, and SAV files.
/// Reference: BioWare Aurora ERF format spec, neverwinter.nim erf.nim
/// </summary>
public class ErfFile
{
    /// <summary>
    /// File signature - "ERF ", "HAK ", "MOD ", "SAV ", or "NWM "
    /// </summary>
    public string FileType { get; set; } = "ERF ";

    /// <summary>
    /// File version - "V1.0"
    /// </summary>
    public string FileVersion { get; set; } = "V1.0";

    /// <summary>
    /// Build year (since 1900)
    /// </summary>
    public uint BuildYear { get; set; }

    /// <summary>
    /// Build day (day of year, 1-366)
    /// </summary>
    public uint BuildDay { get; set; }

    /// <summary>
    /// StrRef for file description (from TLK)
    /// </summary>
    public uint DescriptionStrRef { get; set; }

    /// <summary>
    /// Localized description strings (by language ID)
    /// </summary>
    public List<ErfLocalizedString> LocalizedStrings { get; set; } = new();

    /// <summary>
    /// Resource entries (files packed into the ERF)
    /// </summary>
    public List<ErfResourceEntry> Resources { get; set; } = new();

    /// <summary>
    /// Find a resource by ResRef and type.
    /// </summary>
    public ErfResourceEntry? FindResource(string resRef, ushort resourceType)
    {
        return Resources.FirstOrDefault(r =>
            r.ResRef.Equals(resRef, StringComparison.OrdinalIgnoreCase) &&
            r.ResourceType == resourceType);
    }

    /// <summary>
    /// Get all resources of a specific type.
    /// </summary>
    public IEnumerable<ErfResourceEntry> GetResourcesByType(ushort resourceType)
    {
        return Resources.Where(r => r.ResourceType == resourceType);
    }

    /// <summary>
    /// Check if this is a HAK file based on FileType.
    /// </summary>
    public bool IsHak => FileType == "HAK ";

    /// <summary>
    /// Check if this is a MOD file based on FileType.
    /// </summary>
    public bool IsMod => FileType == "MOD ";
}

/// <summary>
/// Localized string entry in an ERF file.
/// </summary>
public class ErfLocalizedString
{
    /// <summary>
    /// Language ID (encoded as LanguageID * 2 + Gender)
    /// </summary>
    public uint LanguageId { get; set; }

    /// <summary>
    /// The localized string content.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Get the base language (without gender encoding).
    /// </summary>
    public int Language => (int)(LanguageId / 2);

    /// <summary>
    /// Get the gender (0 = neutral/masculine, 1 = feminine).
    /// </summary>
    public int Gender => (int)(LanguageId % 2);
}

/// <summary>
/// Resource entry in an ERF file.
/// </summary>
public class ErfResourceEntry
{
    /// <summary>
    /// Resource reference name (max 16 characters, case-insensitive).
    /// </summary>
    public string ResRef { get; set; } = string.Empty;

    /// <summary>
    /// Resource ID (sequential, starts at 0).
    /// </summary>
    public uint ResId { get; set; }

    /// <summary>
    /// Resource type identifier (see ResourceTypes).
    /// </summary>
    public ushort ResourceType { get; set; }

    /// <summary>
    /// Offset to resource data from beginning of ERF file.
    /// </summary>
    public uint Offset { get; set; }

    /// <summary>
    /// Size of resource data in bytes.
    /// </summary>
    public uint Size { get; set; }
}
