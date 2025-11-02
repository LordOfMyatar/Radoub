using System;
using System.IO;
using System.Collections.Generic;

namespace ListAnalyzer;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: ListAnalyzer <file.dlg>");
            return;
        }

        string path = args[0];
        byte[] bytes = File.ReadAllBytes(path);

        // Parse GFF header
        int listIndicesOffset = BitConverter.ToInt32(bytes, 48);
        int listIndicesCount = BitConverter.ToInt32(bytes, 52);

        Console.WriteLine($"=== {Path.GetFileName(path)} ===");
        Console.WriteLine($"ListIndicesOffset: {listIndicesOffset}");
        Console.WriteLine($"ListIndicesCount: {listIndicesCount} bytes = {listIndicesCount / 4} DWORDs\n");

        // Parse list indices
        Console.WriteLine("=== LIST INDICES SECTION ===\n");
        int pos = listIndicesOffset;
        int listNum = 0;

        while (pos < listIndicesOffset + listIndicesCount)
        {
            // Each list starts with count (4 bytes), then indices (count * 4 bytes)
            if (pos + 4 > bytes.Length) break;

            int count = BitConverter.ToInt32(bytes, pos);
            pos += 4;

            Console.WriteLine($"List[{listNum}]: {count} elements");

            // Show indices
            for (int i = 0; i < count && pos + 4 <= bytes.Length; i++)
            {
                int index = BitConverter.ToInt32(bytes, pos);
                Console.Write($"  [{i}]={index}");
                if ((i + 1) % 8 == 0) Console.WriteLine();
                pos += 4;
            }
            if (count % 8 != 0) Console.WriteLine();

            listNum++;
        }

        Console.WriteLine($"\nTotal lists: {listNum}");
    }
}
