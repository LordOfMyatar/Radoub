using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace FieldOrderAnalyzer;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: FieldOrderAnalyzer <file.dlg>");
            return;
        }

        string path = args[0];
        byte[] bytes = File.ReadAllBytes(path);

        // Parse GFF header
        int structOffset = BitConverter.ToInt32(bytes, 8);
        int structCount = BitConverter.ToInt32(bytes, 12);
        int fieldOffset = BitConverter.ToInt32(bytes, 16);
        int fieldCount = BitConverter.ToInt32(bytes, 20);
        int labelOffset = BitConverter.ToInt32(bytes, 24);
        int labelCount = BitConverter.ToInt32(bytes, 28);
        int fieldDataOffset = BitConverter.ToInt32(bytes, 32);

        Console.WriteLine($"=== {Path.GetFileName(path)} ===");
        Console.WriteLine($"Structs: {structCount}, Fields: {fieldCount}, Labels: {labelCount}\n");

        // Parse labels
        var labels = new List<string>();
        for (int i = 0; i < labelCount; i++)
        {
            int labelPos = labelOffset + (i * 16);
            string label = Encoding.ASCII.GetString(bytes, labelPos, 16).TrimEnd('\0');
            labels.Add(label);
        }

        // Parse structs to map field ranges
        var structFieldRanges = new List<(int type, int dataOrOffset, int fieldCount)>();
        for (int i = 0; i < structCount; i++)
        {
            int structPos = structOffset + (i * 12);
            int type = BitConverter.ToInt32(bytes, structPos);
            int dataOrOffset = BitConverter.ToInt32(bytes, structPos + 4);
            int fCount = BitConverter.ToInt32(bytes, structPos + 8);
            structFieldRanges.Add((type, dataOrOffset, fCount));
        }

        // Show Root struct fields (Struct[0])
        Console.WriteLine("=== ROOT (Struct[0]) FIELD SEQUENCE ===\n");
        var rootStruct = structFieldRanges[0];
        int rootFieldStartIndex = rootStruct.dataOrOffset / 4;

        for (int i = 0; i < rootStruct.fieldCount; i++)
        {
            int fieldIdx = rootFieldStartIndex + i;
            int fieldPos = fieldOffset + (fieldIdx * 12);

            int fieldType = BitConverter.ToInt32(bytes, fieldPos);
            int labelIdx = BitConverter.ToInt32(bytes, fieldPos + 4);
            int dataOrOffset = BitConverter.ToInt32(bytes, fieldPos + 8);

            string labelName = labelIdx < labels.Count ? labels[labelIdx] : "???";
            string typeName = GetTypeName(fieldType);

            Console.WriteLine($"Field[{fieldIdx}]: {labelName,-20} Type={fieldType,2} ({typeName,-15}) Data={dataOrOffset}");
        }

        // Show Entry[0] fields (Struct[1])
        if (structCount > 1)
        {
            Console.WriteLine("\n=== ENTRY[0] (Struct[1]) FIELD SEQUENCE ===\n");
            var entryStruct = structFieldRanges[1];
            int entryFieldStartIndex = entryStruct.dataOrOffset / 4;

            for (int i = 0; i < entryStruct.fieldCount; i++)
            {
                int fieldIdx = entryFieldStartIndex + i;
                int fieldPos = fieldOffset + (fieldIdx * 12);

                int fieldType = BitConverter.ToInt32(bytes, fieldPos);
                int labelIdx = BitConverter.ToInt32(bytes, fieldPos + 4);
                int dataOrOffset = BitConverter.ToInt32(bytes, fieldPos + 8);

                string labelName = labelIdx < labels.Count ? labels[labelIdx] : "???";
                string typeName = GetTypeName(fieldType);

                Console.WriteLine($"Field[{fieldIdx}]: {labelName,-20} Type={fieldType,2} ({typeName,-15}) Data={dataOrOffset}");
            }
        }

        // Show Entry[0].Pointer[0] fields (Struct[2])
        if (structCount > 2)
        {
            Console.WriteLine("\n=== ENTRY[0].POINTER[0] (Struct[2]) FIELD SEQUENCE ===\n");
            var ptrStruct = structFieldRanges[2];
            int ptrFieldStartIndex = ptrStruct.dataOrOffset / 4;

            for (int i = 0; i < ptrStruct.fieldCount; i++)
            {
                int fieldIdx = ptrFieldStartIndex + i;
                int fieldPos = fieldOffset + (fieldIdx * 12);

                int fieldType = BitConverter.ToInt32(bytes, fieldPos);
                int labelIdx = BitConverter.ToInt32(bytes, fieldPos + 4);
                int dataOrOffset = BitConverter.ToInt32(bytes, fieldPos + 8);

                string labelName = labelIdx < labels.Count ? labels[labelIdx] : "???";
                string typeName = GetTypeName(fieldType);

                Console.WriteLine($"Field[{fieldIdx}]: {labelName,-20} Type={fieldType,2} ({typeName,-15}) Data={dataOrOffset}");
            }
        }
    }

    static string GetTypeName(int type)
    {
        return type switch
        {
            0 => "BYTE",
            1 => "CHAR",
            2 => "WORD",
            3 => "SHORT",
            4 => "DWORD",
            5 => "INT",
            6 => "DWORD64",
            7 => "INT64",
            8 => "FLOAT",
            9 => "DOUBLE",
            10 => "CExoString",
            11 => "CResRef",
            12 => "CExoLocString",
            13 => "VOID",
            14 => "Struct",
            15 => "List",
            _ => "Unknown"
        };
    }
}
