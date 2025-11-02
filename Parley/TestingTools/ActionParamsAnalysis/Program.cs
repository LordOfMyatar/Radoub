using System;
using System.IO;

class ActionParamsAnalysis
{
    static void Main()
    {
        var files = new[] {
            ("myra_aurora", @"~\Documents\Neverwinter Nights\modules\LNS_DLG\myra_james.dlg"),
            ("myra_export", @"~\Documents\Neverwinter Nights\modules\LNS_DLG\myra01.dlg"),
            ("lista_aurora", @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_new.dlg"),
            ("lista_export", @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista01.dlg"),
            ("chef_aurora", @"~\Documents\Neverwinter Nights\modules\LNS_DLG\chef.dlg"),
            ("chef_export", @"~\Documents\Neverwinter Nights\modules\LNS_DLG\chef01.dlg")
        };

        foreach (var (name, path) in files)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"\n{name}: FILE NOT FOUND");
                continue;
            }

            var bytes = File.ReadAllBytes(path);
            int structOffset = BitConverter.ToInt32(bytes, 0x08);
            int structCount = BitConverter.ToInt32(bytes, 0x0C);
            int listIndicesCount = BitConverter.ToInt32(bytes, 0x34);

            Console.WriteLine($"\n{name}:");
            Console.WriteLine($"Total structs: {structCount}, ListIndices size: {listIndicesCount} bytes");

            // Count Entry structs (Type 0, excluding root at index 0)
            int entryCount = 0;
            for (int i = 1; i < structCount; i++)
            {
                int pos = structOffset + (i * 12);
                uint type = BitConverter.ToUInt32(bytes, pos);
                if (type == 0) entryCount++;
            }

            // Count Reply structs (Type 1)
            int replyCount = 0;
            for (int i = 0; i < structCount; i++)
            {
                int pos = structOffset + (i * 12);
                uint type = BitConverter.ToUInt32(bytes, pos);
                if (type == 1) replyCount++;
            }

            // Count Start structs (Type 2)
            int startCount = 0;
            for (int i = 0; i < structCount; i++)
            {
                int pos = structOffset + (i * 12);
                uint type = BitConverter.ToUInt32(bytes, pos);
                if (type == 2) startCount++;
            }

            Console.WriteLine($"Entry structs (Type 0, excluding root): {entryCount}");
            Console.WriteLine($"Reply structs (Type 1): {replyCount}");
            Console.WriteLine($"Start structs (Type 2): {startCount}");

            // Expected ActionParams lists: 1 per Entry + 1 per Reply (always empty in these files)
            int expectedActionParamsLists = entryCount + replyCount;
            int actionParamsBytes = expectedActionParamsLists * 4; // 4 bytes per empty list (count=0)
            Console.WriteLine($"Expected ActionParams lists: {expectedActionParamsLists} × 4 bytes = {actionParamsBytes} bytes");

            // Expected ConditionParams lists: 1 per Start + 1 per pointer struct
            // Start wrappers have ConditionParams (usually empty)
            int conditionParamsStartBytes = startCount * 4;
            Console.WriteLine($"Expected ConditionParams (Starts): {startCount} × 4 bytes = {conditionParamsStartBytes} bytes");

            // Estimate total param lists needed
            int totalParamBytes = actionParamsBytes + conditionParamsStartBytes;
            Console.WriteLine($"Total param list bytes needed: {totalParamBytes} bytes");
        }
    }
}
