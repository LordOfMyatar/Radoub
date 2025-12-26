namespace Radoub.Formats.Gff;

/// <summary>
/// Provides helper methods for building GFF fields.
/// Extracted from format-specific writers to reduce duplication.
/// Reference: neverwinter.nim gff.nim
/// </summary>
public static class GffFieldBuilder
{
    /// <summary>
    /// Add a BYTE field to a struct.
    /// </summary>
    public static void AddByteField(GffStruct parent, string label, byte value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.BYTE,
            Label = label,
            Value = value
        });
    }

    /// <summary>
    /// Add a CHAR field to a struct.
    /// </summary>
    public static void AddCharField(GffStruct parent, string label, sbyte value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.CHAR,
            Label = label,
            Value = value
        });
    }

    /// <summary>
    /// Add a WORD field to a struct.
    /// </summary>
    public static void AddWordField(GffStruct parent, string label, ushort value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.WORD,
            Label = label,
            Value = value
        });
    }

    /// <summary>
    /// Add a SHORT field to a struct.
    /// </summary>
    public static void AddShortField(GffStruct parent, string label, short value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.SHORT,
            Label = label,
            Value = value
        });
    }

    /// <summary>
    /// Add a DWORD field to a struct.
    /// </summary>
    public static void AddDwordField(GffStruct parent, string label, uint value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.DWORD,
            Label = label,
            Value = value
        });
    }

    /// <summary>
    /// Add an INT field to a struct.
    /// </summary>
    public static void AddIntField(GffStruct parent, string label, int value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.INT,
            Label = label,
            Value = value
        });
    }

    /// <summary>
    /// Add a DWORD64 field to a struct.
    /// </summary>
    public static void AddDword64Field(GffStruct parent, string label, ulong value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.DWORD64,
            Label = label,
            Value = value
        });
    }

    /// <summary>
    /// Add an INT64 field to a struct.
    /// </summary>
    public static void AddInt64Field(GffStruct parent, string label, long value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.INT64,
            Label = label,
            Value = value
        });
    }

    /// <summary>
    /// Add a FLOAT field to a struct.
    /// </summary>
    public static void AddFloatField(GffStruct parent, string label, float value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.FLOAT,
            Label = label,
            Value = value
        });
    }

    /// <summary>
    /// Add a DOUBLE field to a struct.
    /// </summary>
    public static void AddDoubleField(GffStruct parent, string label, double value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.DOUBLE,
            Label = label,
            Value = value
        });
    }

    /// <summary>
    /// Add a CExoString field to a struct.
    /// </summary>
    public static void AddCExoStringField(GffStruct parent, string label, string value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.CExoString,
            Label = label,
            Value = value
        });
    }

    /// <summary>
    /// Add a CResRef field to a struct.
    /// </summary>
    public static void AddCResRefField(GffStruct parent, string label, string value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.CResRef,
            Label = label,
            Value = value
        });
    }

    /// <summary>
    /// Add a CExoLocString field to a struct.
    /// </summary>
    public static void AddLocStringField(GffStruct parent, string label, CExoLocString locString)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.CExoLocString,
            Label = label,
            Value = locString
        });
    }

    /// <summary>
    /// Add a VOID (raw bytes) field to a struct.
    /// </summary>
    public static void AddVoidField(GffStruct parent, string label, byte[] data)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.VOID,
            Label = label,
            Value = data
        });
    }

    /// <summary>
    /// Add a Struct field to a struct.
    /// </summary>
    public static void AddStructField(GffStruct parent, string label, GffStruct value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.Struct,
            Label = label,
            Value = value
        });
    }

    /// <summary>
    /// Add a List field to a struct. Automatically sets the list count.
    /// </summary>
    public static void AddListField(GffStruct parent, string label, GffList list)
    {
        list.Count = (uint)list.Elements.Count;
        parent.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = label,
            Value = list
        });
    }

    /// <summary>
    /// Add a List field to a struct from a collection of structs.
    /// </summary>
    public static void AddListField(GffStruct parent, string label, IEnumerable<GffStruct> elements)
    {
        var list = new GffList();
        list.Elements.AddRange(elements);
        AddListField(parent, label, list);
    }
}
