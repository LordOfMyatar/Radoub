namespace Radoub.Formats.Gff;

/// <summary>
/// Represents a GFF (Generic File Format) file used by Aurora Engine games.
/// GFF is the base format for DLG, UTC, UTI, JRL, and many other game files.
/// Reference: BioWare Aurora GFF format spec, neverwinter.nim gff.nim
/// </summary>
public class GffFile
{
    /// <summary>
    /// File type signature (4 bytes, e.g., "DLG ", "UTC ", "JRL ").
    /// </summary>
    public string FileType { get; set; } = string.Empty;

    /// <summary>
    /// File version (4 bytes, typically "V3.2").
    /// </summary>
    public string FileVersion { get; set; } = string.Empty;

    /// <summary>
    /// The root struct containing all data.
    /// </summary>
    public GffStruct RootStruct { get; set; } = new();

    /// <summary>
    /// All structs in the file (indexed by struct array position).
    /// </summary>
    public List<GffStruct> Structs { get; set; } = new();

    /// <summary>
    /// All fields in the file (indexed by field array position).
    /// </summary>
    public List<GffField> Fields { get; set; } = new();

    /// <summary>
    /// All labels in the file (indexed by label array position).
    /// </summary>
    public List<GffLabel> Labels { get; set; } = new();
}

/// <summary>
/// GFF file header containing offsets and counts for all sections.
/// </summary>
public class GffHeader
{
    public string FileType { get; set; } = string.Empty;
    public string FileVersion { get; set; } = string.Empty;
    public uint StructOffset { get; set; }
    public uint StructCount { get; set; }
    public uint FieldOffset { get; set; }
    public uint FieldCount { get; set; }
    public uint LabelOffset { get; set; }
    public uint LabelCount { get; set; }
    public uint FieldDataOffset { get; set; }
    public uint FieldDataCount { get; set; }
    public uint FieldIndicesOffset { get; set; }
    public uint FieldIndicesCount { get; set; }
    public uint ListIndicesOffset { get; set; }
    public uint ListIndicesCount { get; set; }
}

/// <summary>
/// A struct in GFF format, containing zero or more fields.
/// </summary>
public class GffStruct
{
    /// <summary>
    /// Struct type ID (application-specific meaning).
    /// </summary>
    public uint Type { get; set; }

    /// <summary>
    /// For FieldCount=1: direct field index.
    /// For FieldCount>1: offset into FieldIndices array.
    /// </summary>
    public uint DataOrDataOffset { get; set; }

    /// <summary>
    /// Number of fields in this struct.
    /// </summary>
    public uint FieldCount { get; set; }

    /// <summary>
    /// The fields belonging to this struct (populated during parsing).
    /// </summary>
    public List<GffField> Fields { get; set; } = new();

    /// <summary>
    /// Get a field by label (case-insensitive).
    /// </summary>
    public GffField? GetField(string label)
    {
        return Fields.FirstOrDefault(f => f.Label.Equals(label, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get a field value with type safety and default fallback.
    /// </summary>
    public T GetFieldValue<T>(string label, T defaultValue = default!)
    {
        var field = GetField(label);
        if (field?.Value is T value)
            return value;
        return defaultValue;
    }
}

/// <summary>
/// A field in GFF format, containing typed data.
/// </summary>
public class GffField
{
    /// <summary>
    /// Field type (see constants below).
    /// </summary>
    public uint Type { get; set; }

    /// <summary>
    /// Index into the labels array.
    /// </summary>
    public uint LabelIndex { get; set; }

    /// <summary>
    /// The field's label/name (populated during parsing).
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// For simple types: the value itself.
    /// For complex types: offset into FieldData section.
    /// </summary>
    public uint DataOrDataOffset { get; set; }

    /// <summary>
    /// The parsed value (type depends on field Type).
    /// </summary>
    public object? Value { get; set; }

    // Field type constants from GFF specification
    public const uint BYTE = 0;
    public const uint CHAR = 1;
    public const uint WORD = 2;
    public const uint SHORT = 3;
    public const uint DWORD = 4;
    public const uint INT = 5;
    public const uint DWORD64 = 6;
    public const uint INT64 = 7;
    public const uint FLOAT = 8;
    public const uint DOUBLE = 9;
    public const uint CExoString = 10;
    public const uint CResRef = 11;
    public const uint CExoLocString = 12;
    public const uint VOID = 13;
    public const uint Struct = 14;
    public const uint List = 15;

    // Type checking helpers
    public bool IsByte => Type == BYTE;
    public bool IsChar => Type == CHAR;
    public bool IsWord => Type == WORD;
    public bool IsShort => Type == SHORT;
    public bool IsDWord => Type == DWORD;
    public bool IsInt => Type == INT;
    public bool IsDWord64 => Type == DWORD64;
    public bool IsInt64 => Type == INT64;
    public bool IsFloat => Type == FLOAT;
    public bool IsDouble => Type == DOUBLE;
    public bool IsCExoString => Type == CExoString;
    public bool IsCResRef => Type == CResRef;
    public bool IsCExoLocString => Type == CExoLocString;
    public bool IsVoid => Type == VOID;
    public bool IsStruct => Type == Struct;
    public bool IsList => Type == List;
}

/// <summary>
/// A label (field name) in GFF format. Max 16 characters.
/// </summary>
public class GffLabel
{
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// A list of structs in GFF format.
/// </summary>
public class GffList
{
    /// <summary>
    /// Number of struct references in this list.
    /// </summary>
    public uint Count { get; set; }

    /// <summary>
    /// The structs in this list (populated during parsing).
    /// </summary>
    public List<GffStruct> Elements { get; set; } = new();
}

/// <summary>
/// A localized string supporting multiple languages.
/// </summary>
public class CExoLocString
{
    /// <summary>
    /// String reference into TLK file (0xFFFFFFFF = no TLK reference).
    /// </summary>
    public uint StrRef { get; set; } = 0xFFFFFFFF;

    /// <summary>
    /// Number of localized substrings.
    /// </summary>
    public uint SubStringCount { get; set; }

    /// <summary>
    /// Localized strings keyed by language ID.
    /// Language ID = (LanguageEnum * 2) + Gender (0=male, 1=female).
    /// </summary>
    public Dictionary<uint, string> LocalizedStrings { get; set; } = new();

    /// <summary>
    /// Get string for a specific language ID.
    /// </summary>
    public string GetString(uint languageId = 0)
    {
        return LocalizedStrings.TryGetValue(languageId, out var text) ? text : string.Empty;
    }

    /// <summary>
    /// Get the default string (English male, or first available).
    /// </summary>
    public string GetDefaultString()
    {
        // Try English male (0) first, then any available language
        if (LocalizedStrings.TryGetValue(0, out var english))
            return english;
        return LocalizedStrings.Values.FirstOrDefault() ?? string.Empty;
    }

    /// <summary>
    /// True if no strings and no TLK reference.
    /// </summary>
    public bool IsEmpty => LocalizedStrings.Count == 0 && StrRef == 0xFFFFFFFF;
}

/// <summary>
/// Extension methods for GFF field types.
/// </summary>
public static class GffFieldTypeExtensions
{
    /// <summary>
    /// Returns true if the field type stores its value directly in DataOrDataOffset.
    /// </summary>
    public static bool IsSimpleType(this uint fieldType)
    {
        return fieldType switch
        {
            GffField.BYTE or GffField.CHAR or GffField.WORD or GffField.SHORT or
            GffField.DWORD or GffField.INT or GffField.DWORD64 or GffField.INT64 or
            GffField.FLOAT or GffField.DOUBLE => true,
            _ => false
        };
    }

    /// <summary>
    /// Returns true if the field type uses DataOrDataOffset as an offset.
    /// </summary>
    public static bool IsComplexType(this uint fieldType)
    {
        return fieldType switch
        {
            GffField.CExoString or GffField.CResRef or GffField.CExoLocString or
            GffField.VOID or GffField.Struct or GffField.List => true,
            _ => false
        };
    }

    /// <summary>
    /// Get the human-readable name for a field type.
    /// </summary>
    public static string GetTypeName(this uint fieldType)
    {
        return fieldType switch
        {
            GffField.BYTE => "BYTE",
            GffField.CHAR => "CHAR",
            GffField.WORD => "WORD",
            GffField.SHORT => "SHORT",
            GffField.DWORD => "DWORD",
            GffField.INT => "INT",
            GffField.DWORD64 => "DWORD64",
            GffField.INT64 => "INT64",
            GffField.FLOAT => "FLOAT",
            GffField.DOUBLE => "DOUBLE",
            GffField.CExoString => "CExoString",
            GffField.CResRef => "CResRef",
            GffField.CExoLocString => "CExoLocString",
            GffField.VOID => "VOID",
            GffField.Struct => "Struct",
            GffField.List => "List",
            _ => $"Unknown({fieldType})"
        };
    }
}
