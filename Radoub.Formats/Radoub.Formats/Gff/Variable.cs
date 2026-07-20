namespace Radoub.Formats.Gff;

/// <summary>
/// Variable types supported by the Aurora Engine VarTable.
/// These map to NWScript variable types.
/// </summary>
public enum VariableType : uint
{
    /// <summary>Integer variable (NWScript int).</summary>
    Int = 1,

    /// <summary>Float variable (NWScript float).</summary>
    Float = 2,

    /// <summary>String variable (NWScript string).</summary>
    String = 3,

    /// <summary>Object reference variable (NWScript object).</summary>
    Object = 4,

    /// <summary>Location variable (NWScript location).</summary>
    Location = 5
}

/// <summary>
/// Represents a local variable stored in a GFF VarTable.
/// Used by SetLocalInt/GetLocalInt, SetLocalFloat/GetLocalFloat, etc.
/// Reference: BioWare Aurora CommonGFFStructs - Variable Struct (StructID 0)
/// </summary>
public class Variable
{
    /// <summary>The variable name as set by SetLocal*() script functions.</summary>
    public string Name { get; set; } = string.Empty;

    public VariableType Type { get; set; }

    /// <summary>
    /// The variable's value. Type depends on <see cref="Type"/>:
    /// - Int (1): int
    /// - Float (2): float
    /// - String (3): string
    /// - Object (4): uint (ObjectId)
    /// - Location (5): VariableLocation
    /// </summary>
    public object? Value { get; set; }

    /// <summary>Value as an integer; 0 on type mismatch.</summary>
    public int GetInt() => Value is int i ? i : 0;

    /// <summary>Value as a float; 0.0f on type mismatch.</summary>
    public float GetFloat() => Value is float f ? f : 0.0f;

    /// <summary>Value as a string; empty string on type mismatch.</summary>
    public string GetString() => Value is string s ? s : string.Empty;

    /// <summary>Value as an object ID; 0x7F000000 (OBJECT_INVALID) on type mismatch.</summary>
    public uint GetObjectId() => Value is uint u ? u : 0x7F000000;

    /// <summary>Value as a location; null on type mismatch.</summary>
    public VariableLocation? GetLocation() => Value as VariableLocation;

    public static Variable CreateInt(string name, int value) => new()
    {
        Name = name,
        Type = VariableType.Int,
        Value = value
    };

    public static Variable CreateFloat(string name, float value) => new()
    {
        Name = name,
        Type = VariableType.Float,
        Value = value
    };

    public static Variable CreateString(string name, string value) => new()
    {
        Name = name,
        Type = VariableType.String,
        Value = value
    };

    public static Variable CreateObject(string name, uint objectId) => new()
    {
        Name = name,
        Type = VariableType.Object,
        Value = objectId
    };

    public static Variable CreateLocation(string name, VariableLocation location) => new()
    {
        Name = name,
        Type = VariableType.Location,
        Value = location
    };
}

/// <summary>
/// Represents a location value stored in a Variable.
/// Reference: BioWare Aurora CommonGFFStructs - Location Struct (StructID 1)
/// </summary>
public class VariableLocation
{
    /// <summary>ObjectId of the area containing the location.</summary>
    public uint Area { get; set; }

    public float PositionX { get; set; }

    public float PositionY { get; set; }

    public float PositionZ { get; set; }

    /// <summary>Orientation is the facing direction vector.</summary>
    public float OrientationX { get; set; }

    public float OrientationY { get; set; }

    public float OrientationZ { get; set; }
}
