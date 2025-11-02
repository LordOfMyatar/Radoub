using System;
using System.IO;
using System.Text;

class Program
{
    static void Main()
    {
        string originalFile = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\chef.dlg";

        using var fs = new FileStream(originalFile, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);

        // Read header
        br.BaseStream.Seek(8, SeekOrigin.Begin);
        br.ReadUInt32(); br.ReadUInt32(); // struct
        br.ReadUInt32(); br.ReadUInt32(); // field
        br.ReadUInt32(); br.ReadUInt32(); // label

        uint fieldDataOffset = br.ReadUInt32();
        uint fieldDataCount = br.ReadUInt32();

        Console.WriteLine($"FieldData starts at offset {fieldDataOffset}, size {fieldDataCount}");

        // Read first 100 bytes of FieldData
        br.BaseStream.Seek(fieldDataOffset, SeekOrigin.Begin);
        byte[] first100 = br.ReadBytes(100);

        Console.WriteLine("\nFirst 100 bytes of FieldData:");
        for (int i = 0; i < 100; i += 16)
        {
            Console.Write($"  {i:X4}: ");
            for (int j = 0; j < 16 && i + j < 100; j++)
            {
                Console.Write($"{first100[i + j]:X2} ");
            }
            Console.WriteLine();
        }

        // Check specifically at offset 0, 22, 26 (from gap analysis)
        Console.WriteLine("\n=== Checking specific offsets ===");
        CheckOffset(br, fieldDataOffset, 0, "offset 0 (22-byte gap)");
        CheckOffset(br, fieldDataOffset, 22, "offset 22");
        CheckOffset(br, fieldDataOffset, 26, "offset 26 (84-byte gap)");
    }

    static void CheckOffset(BinaryReader br, uint fieldDataBase, int offset, string label)
    {
        br.BaseStream.Seek(fieldDataBase + offset, SeekOrigin.Begin);

        // Try reading as CExoString
        uint length = br.ReadUInt32();
        Console.WriteLine($"\n{label}:");
        Console.WriteLine($"  Length prefix: {length}");

        if (length == 0)
        {
            Console.WriteLine($"  Empty string (length=0)");
        }
        else if (length < 1000)
        {
            byte[] data = br.ReadBytes((int)length);
            string text = Encoding.UTF8.GetString(data);
            Console.WriteLine($"  Text: '{text}'");
        }
        else
        {
            Console.WriteLine($"  Invalid length (probably not a string)");
        }
    }
}
