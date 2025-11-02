using System;
using System.IO;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: HexDumpParams <original.dlg> <exported.dlg>");
            Console.WriteLine("\nCompares parameter list binary format between original and exported DLG files.");
            Console.WriteLine("Shows hex dumps of ActionParams and ConditionParams lists from ListIndices section.");
            return;
        }

        string originalPath = args[0];
        string exportedPath = args[1];

        if (!File.Exists(originalPath))
        {
            Console.WriteLine($"ERROR: Original file not found: {originalPath}");
            return;
        }

        if (!File.Exists(exportedPath))
        {
            Console.WriteLine($"ERROR: Exported file not found: {exportedPath}");
            return;
        }

        Console.WriteLine("=== PARAMETER LIST HEX DUMP COMPARISON ===\n");
        Console.WriteLine($"Original: {originalPath}");
        Console.WriteLine($"Exported: {exportedPath}\n");

        var originalFile = new DlgFile(originalPath);
        var exportedFile = new DlgFile(exportedPath);

        originalFile.Load();
        exportedFile.Load();

        Console.WriteLine($"Original: {originalFile.StructCount} structs, ListIndices size: {originalFile.ListIndicesCount} bytes");
        Console.WriteLine($"Exported: {exportedFile.StructCount} structs, ListIndices size: {exportedFile.ListIndicesCount} bytes\n");

        // Find first few nodes with ActionParams > 0
        Console.WriteLine("=== ACTION PARAMETERS (First few nodes with ActionParams > 0) ===\n");
        CompareActionParams(originalFile, exportedFile);

        // Find first few pointers with ConditionParams > 0
        Console.WriteLine("\n=== CONDITION PARAMETERS (First few pointers with ConditionParams > 0) ===\n");
        CompareConditionParams(originalFile, exportedFile);
    }

    static void CompareActionParams(DlgFile original, DlgFile exported)
    {
        int found = 0;
        int maxToShow = 3;

        // Search for Entry/Reply structs with ActionParams
        for (int i = 0; i < Math.Min(original.StructCount, exported.StructCount) && found < maxToShow; i++)
        {
            var origStruct = original.ReadStruct(i);
            var expStruct = exported.ReadStruct(i);

            var origActionParams = original.FindFieldByLabel(origStruct, "ActionParams");
            var expActionParams = exported.FindFieldByLabel(expStruct, "ActionParams");

            if (origActionParams != null && expActionParams != null &&
                origActionParams.Type == 15 && expActionParams.Type == 15) // List type
            {
                uint origListOffset = origActionParams.DataOrDataOffset;
                uint expListOffset = expActionParams.DataOrDataOffset;

                // Read list count
                uint origCount = original.ReadListCount(origListOffset);
                uint expCount = exported.ReadListCount(expListOffset);

                if (origCount > 0 || expCount > 0)
                {
                    found++;
                    Console.WriteLine($"--- Struct[{i}] ActionParams (Type={origStruct.Type}/{expStruct.Type}) ---");
                    Console.WriteLine($"List count: Original={origCount}, Exported={expCount}");

                    CompareHexDump(original, exported, origListOffset, expListOffset, 32, "ActionParams");
                    Console.WriteLine();
                }
            }
        }

        if (found == 0)
        {
            Console.WriteLine("No ActionParams lists found with count > 0");
        }
    }

    static void CompareConditionParams(DlgFile original, DlgFile exported)
    {
        int found = 0;
        int maxToShow = 3;

        // Search through all structs for ConditionParams
        for (int i = 0; i < Math.Min(original.StructCount, exported.StructCount) && found < maxToShow; i++)
        {
            var origStruct = original.ReadStruct(i);
            var expStruct = exported.ReadStruct(i);

            var origConditionParams = original.FindFieldByLabel(origStruct, "ConditionParams");
            var expConditionParams = exported.FindFieldByLabel(expStruct, "ConditionParams");

            if (origConditionParams != null && expConditionParams != null &&
                origConditionParams.Type == 15 && expConditionParams.Type == 15) // List type
            {
                uint origListOffset = origConditionParams.DataOrDataOffset;
                uint expListOffset = expConditionParams.DataOrDataOffset;

                uint origCount = original.ReadListCount(origListOffset);
                uint expCount = exported.ReadListCount(expListOffset);

                if (origCount > 0 || expCount > 0)
                {
                    found++;
                    Console.WriteLine($"--- Struct[{i}] ConditionParams (Type={origStruct.Type}/{expStruct.Type}, Field={origConditionParams.Label}) ---");
                    Console.WriteLine($"List count: Original={origCount}, Exported={expCount}");

                    CompareHexDump(original, exported, origListOffset, expListOffset, 32, "ConditionParams");
                    Console.WriteLine();
                }
            }
        }

        if (found == 0)
        {
            Console.WriteLine("No ConditionParams lists found with count > 0");
        }
    }

    static void CompareHexDump(DlgFile original, DlgFile exported, uint origOffset, uint expOffset, int byteCount, string label)
    {
        byte[] origBytes = original.ReadBytes(origOffset, byteCount);
        byte[] expBytes = exported.ReadBytes(expOffset, byteCount);

        Console.WriteLine($"\nOriginal offset 0x{origOffset:X8}:");
        PrintHexLine(origBytes);
        InterpretParamList(origBytes);

        Console.WriteLine($"\nExported offset 0x{expOffset:X8}:");
        PrintHexLine(expBytes);
        InterpretParamList(expBytes);

        // Highlight differences
        bool identical = true;
        for (int i = 0; i < Math.Min(origBytes.Length, expBytes.Length); i++)
        {
            if (origBytes[i] != expBytes[i])
            {
                identical = false;
                break;
            }
        }

        if (identical && origBytes.Length == expBytes.Length)
        {
            Console.WriteLine("\n✓ IDENTICAL");
        }
        else
        {
            Console.WriteLine("\n✗ DIFFERENT");
            HighlightDifferences(origBytes, expBytes);
        }
    }

    static void PrintHexLine(byte[] bytes)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < bytes.Length; i++)
        {
            sb.Append($"{bytes[i]:X2} ");
            if ((i + 1) % 16 == 0 && i < bytes.Length - 1)
                sb.Append("\n");
        }
        Console.WriteLine(sb.ToString());
    }

    static void InterpretParamList(byte[] bytes)
    {
        if (bytes.Length < 4)
        {
            Console.WriteLine("(insufficient data for count)");
            return;
        }

        uint count = BitConverter.ToUInt32(bytes, 0);
        Console.WriteLine($"Interpreted: count={count}");

        if (count > 0 && bytes.Length >= 4 + (count * 4))
        {
            Console.Write("Struct indices: [");
            for (int i = 0; i < count; i++)
            {
                uint structIndex = BitConverter.ToUInt32(bytes, 4 + (i * 4));
                Console.Write($"{structIndex}");
                if (i < count - 1) Console.Write(", ");
            }
            Console.WriteLine("]");
        }
    }

    static void HighlightDifferences(byte[] original, byte[] exported)
    {
        int maxLen = Math.Max(original.Length, exported.Length);
        StringBuilder sb = new StringBuilder();
        sb.Append("Diff positions: ");

        bool hasDiff = false;
        for (int i = 0; i < maxLen; i++)
        {
            byte origByte = i < original.Length ? original[i] : (byte)0;
            byte expByte = i < exported.Length ? exported[i] : (byte)0;

            if (origByte != expByte)
            {
                if (hasDiff) sb.Append(", ");
                sb.Append($"[{i}]: {origByte:X2}→{expByte:X2}");
                hasDiff = true;
            }
        }

        if (!hasDiff)
            sb.Append("(none)");

        Console.WriteLine(sb.ToString());
    }
}

class StructInfo
{
    public uint Type { get; set; }
    public uint DataOrDataOffset { get; set; }
    public uint FieldCount { get; set; }
    public uint[] FieldIndices { get; set; } = Array.Empty<uint>();
}

class FieldInfo
{
    public uint Type { get; set; }
    public uint LabelIndex { get; set; }
    public string Label { get; set; } = string.Empty;
    public uint DataOrDataOffset { get; set; }
}

class DlgFile
{
    private byte[] _data;
    private string _path;

    public uint StructOffset { get; private set; }
    public uint StructCount { get; private set; }
    public uint FieldOffset { get; private set; }
    public uint FieldCount { get; private set; }
    public uint LabelOffset { get; private set; }
    public uint LabelCount { get; private set; }
    public uint FieldDataOffset { get; private set; }
    public uint FieldDataCount { get; private set; }
    public uint FieldIndicesOffset { get; private set; }
    public uint FieldIndicesCount { get; private set; }
    public uint ListIndicesOffset { get; private set; }
    public uint ListIndicesCount { get; private set; }

    public DlgFile(string path)
    {
        _path = path;
        _data = Array.Empty<byte>();
    }

    public void Load()
    {
        _data = File.ReadAllBytes(_path);

        // Read GFF header
        StructOffset = BitConverter.ToUInt32(_data, 0x08);
        StructCount = BitConverter.ToUInt32(_data, 0x0C);
        FieldOffset = BitConverter.ToUInt32(_data, 0x10);
        FieldCount = BitConverter.ToUInt32(_data, 0x14);
        LabelOffset = BitConverter.ToUInt32(_data, 0x18);
        LabelCount = BitConverter.ToUInt32(_data, 0x1C);
        FieldDataOffset = BitConverter.ToUInt32(_data, 0x20);
        FieldDataCount = BitConverter.ToUInt32(_data, 0x24);
        FieldIndicesOffset = BitConverter.ToUInt32(_data, 0x28);
        FieldIndicesCount = BitConverter.ToUInt32(_data, 0x2C);
        ListIndicesOffset = BitConverter.ToUInt32(_data, 0x30);
        ListIndicesCount = BitConverter.ToUInt32(_data, 0x34);
    }

    public StructInfo ReadStruct(int index)
    {
        uint structPos = StructOffset + (uint)(index * 12);

        var info = new StructInfo
        {
            Type = BitConverter.ToUInt32(_data, (int)structPos),
            DataOrDataOffset = BitConverter.ToUInt32(_data, (int)structPos + 4),
            FieldCount = BitConverter.ToUInt32(_data, (int)structPos + 8)
        };

        // Read field indices
        if (info.FieldCount == 1)
        {
            info.FieldIndices = new uint[] { info.DataOrDataOffset };
        }
        else if (info.FieldCount > 1)
        {
            info.FieldIndices = new uint[info.FieldCount];
            uint indicesPos = FieldIndicesOffset + info.DataOrDataOffset;
            for (int i = 0; i < info.FieldCount; i++)
            {
                info.FieldIndices[i] = BitConverter.ToUInt32(_data, (int)indicesPos + (i * 4));
            }
        }

        return info;
    }

    public FieldInfo? FindFieldByLabel(StructInfo structInfo, string targetLabel)
    {
        foreach (uint fieldIndex in structInfo.FieldIndices)
        {
            uint fieldPos = FieldOffset + (fieldIndex * 12);

            var field = new FieldInfo
            {
                Type = BitConverter.ToUInt32(_data, (int)fieldPos),
                LabelIndex = BitConverter.ToUInt32(_data, (int)fieldPos + 4),
                DataOrDataOffset = BitConverter.ToUInt32(_data, (int)fieldPos + 8)
            };

            // Read label
            uint labelPos = LabelOffset + (field.LabelIndex * 16);
            field.Label = ReadNullTerminatedString((int)labelPos, 16);

            if (field.Label.Equals(targetLabel, StringComparison.OrdinalIgnoreCase))
            {
                return field;
            }
        }

        return null;
    }

    public uint ReadListCount(uint listOffset)
    {
        uint absoluteOffset = ListIndicesOffset + listOffset;
        if (absoluteOffset + 4 > _data.Length)
            return 0;

        return BitConverter.ToUInt32(_data, (int)absoluteOffset);
    }

    public byte[] ReadBytes(uint offset, int count)
    {
        uint absoluteOffset = ListIndicesOffset + offset;
        if (absoluteOffset >= _data.Length)
            return Array.Empty<byte>();

        int bytesToRead = Math.Min(count, (int)(_data.Length - absoluteOffset));
        byte[] result = new byte[bytesToRead];
        Array.Copy(_data, absoluteOffset, result, 0, bytesToRead);
        return result;
    }

    private string ReadNullTerminatedString(int offset, int maxLength)
    {
        int end = offset;
        while (end < offset + maxLength && end < _data.Length && _data[end] != 0)
        {
            end++;
        }

        return System.Text.Encoding.ASCII.GetString(_data, offset, end - offset);
    }
}
