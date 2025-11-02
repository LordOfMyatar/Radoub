using System;
using System.IO;

class GffValidator
{
    static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("Usage: ValidateGff <file.dlg>");
            return;
        }

        var file = args[0];
        if (!File.Exists(file))
        {
            Console.WriteLine($"File not found: {file}");
            return;
        }

        Console.WriteLine($"=== Validating GFF: {Path.GetFileName(file)} ===\n");

        var bytes = File.ReadAllBytes(file);
        bool isValid = true;

        // Read header
        var signature = System.Text.Encoding.ASCII.GetString(bytes, 0, 4);
        var version = System.Text.Encoding.ASCII.GetString(bytes, 4, 4);

        Console.WriteLine($"Signature: '{signature}' (should be 'DLG ')");
        Console.WriteLine($"Version:   '{version}' (should be 'V3.2')");

        if (signature != "DLG ")
        {
            Console.WriteLine("❌ INVALID: Wrong signature!");
            isValid = false;
        }

        int structOffset = BitConverter.ToInt32(bytes, 0x08);
        int structCount = BitConverter.ToInt32(bytes, 0x0C);
        int fieldOffset = BitConverter.ToInt32(bytes, 0x10);
        int fieldCount = BitConverter.ToInt32(bytes, 0x14);
        int labelOffset = BitConverter.ToInt32(bytes, 0x18);
        int labelCount = BitConverter.ToInt32(bytes, 0x1C);
        int fieldDataOffset = BitConverter.ToInt32(bytes, 0x20);
        int fieldDataCount = BitConverter.ToInt32(bytes, 0x24);
        int fieldIndicesOffset = BitConverter.ToInt32(bytes, 0x28);
        int fieldIndicesCount = BitConverter.ToInt32(bytes, 0x2C);
        int listIndicesOffset = BitConverter.ToInt32(bytes, 0x30);
        int listIndicesCount = BitConverter.ToInt32(bytes, 0x34);

        Console.WriteLine($"\n=== Header Offsets ===");
        Console.WriteLine($"StructOffset:       {structOffset,6} (count: {structCount})");
        Console.WriteLine($"FieldOffset:        {fieldOffset,6} (count: {fieldCount})");
        Console.WriteLine($"LabelOffset:        {labelOffset,6} (count: {labelCount})");
        Console.WriteLine($"FieldDataOffset:    {fieldDataOffset,6} (size: {fieldDataCount} bytes)");
        Console.WriteLine($"FieldIndicesOffset: {fieldIndicesOffset,6} (size: {fieldIndicesCount} bytes)");
        Console.WriteLine($"ListIndicesOffset:  {listIndicesOffset,6} (size: {listIndicesCount} bytes)");

        // Validate header offset ordering
        Console.WriteLine($"\n=== Offset Validation ===");
        if (structOffset != 56)
        {
            Console.WriteLine($"❌ StructOffset should be 56, got {structOffset}");
            isValid = false;
        }

        int expectedFieldOffset = structOffset + (structCount * 12);
        if (fieldOffset != expectedFieldOffset)
        {
            Console.WriteLine($"❌ FieldOffset mismatch: expected {expectedFieldOffset}, got {fieldOffset}");
            isValid = false;
        }

        int expectedLabelOffset = fieldOffset + (fieldCount * 12);
        if (labelOffset != expectedLabelOffset)
        {
            Console.WriteLine($"❌ LabelOffset mismatch: expected {expectedLabelOffset}, got {labelOffset}");
            isValid = false;
        }

        int expectedFieldDataOffset = labelOffset + (labelCount * 16);
        if (fieldDataOffset != expectedFieldDataOffset)
        {
            Console.WriteLine($"❌ FieldDataOffset mismatch: expected {expectedFieldDataOffset}, got {fieldDataOffset}");
            isValid = false;
        }

        int expectedFieldIndicesOffset = fieldDataOffset + fieldDataCount;
        if (fieldIndicesOffset != expectedFieldIndicesOffset)
        {
            Console.WriteLine($"❌ FieldIndicesOffset mismatch: expected {expectedFieldIndicesOffset}, got {fieldIndicesOffset}");
            isValid = false;
        }

        int expectedListIndicesOffset = fieldIndicesOffset + fieldIndicesCount;
        if (listIndicesOffset != expectedListIndicesOffset)
        {
            Console.WriteLine($"❌ ListIndicesOffset mismatch: expected {expectedListIndicesOffset}, got {listIndicesOffset}");
            isValid = false;
        }

        int expectedFileSize = listIndicesOffset + listIndicesCount;
        if (bytes.Length != expectedFileSize)
        {
            Console.WriteLine($"❌ File size mismatch: expected {expectedFileSize} bytes, got {bytes.Length} bytes");
            Console.WriteLine($"   Extra data at EOF: {bytes.Length - expectedFileSize} bytes");
            isValid = false;
        }

        // Check FieldIndices count
        int expectedFieldIndicesBytes = fieldCount * 4;
        if (fieldIndicesCount != expectedFieldIndicesBytes)
        {
            Console.WriteLine($"❌ FieldIndicesCount mismatch: expected {expectedFieldIndicesBytes} bytes ({fieldCount} fields * 4), got {fieldIndicesCount}");
            isValid = false;
        }

        if (isValid)
        {
            Console.WriteLine("✅ All header validations PASSED");
        }
        else
        {
            Console.WriteLine("\n❌ VALIDATION FAILED - File has structural errors!");
        }

        Console.WriteLine($"\n=== Summary ===");
        Console.WriteLine($"File size: {bytes.Length} bytes");
        Console.WriteLine($"Expected:  {expectedFileSize} bytes");
        Console.WriteLine($"Result:    {(isValid ? "✅ VALID" : "❌ INVALID")}");
    }
}
