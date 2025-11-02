using System;
using System.Threading.Tasks;
using DialogEditor.Parsers;

class Program
{
    static async Task Main()
    {
        string basePath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG";
        string[] files = { "chef_fresh.dlg", "myra_fresh.dlg" };

        var parser = new DialogParser();

        foreach (var file in files)
        {
            Console.WriteLine($"\n=== Checking {file} ===");
            var dlg = await parser.ParseFromFileAsync($@"{basePath}\{file}");

            // Check ActionParams in entries
            int entriesWithParams = 0;
            int totalActionParams = 0;
            int entryIdx = 0;
            foreach (var entry in dlg.Entries)
            {
                if (entry.ActionParams != null && entry.ActionParams.Count > 0)
                {
                    entriesWithParams++;
                    totalActionParams += entry.ActionParams.Count;
                    Console.WriteLine($"  Entry {entryIdx}: {entry.ActionParams.Count} ActionParams");
                    foreach (var kvp in entry.ActionParams)
                    {
                        Console.WriteLine($"    {kvp.Key} = {kvp.Value}");
                    }
                }
                entryIdx++;
            }

            // Check ConditionParams in all pointers
            int pointersWithParams = 0;
            int totalCondParams = 0;
            foreach (var entry in dlg.Entries)
            {
                foreach (var ptr in entry.Pointers)
                {
                    if (ptr.ConditionParams != null && ptr.ConditionParams.Count > 0)
                    {
                        pointersWithParams++;
                        totalCondParams += ptr.ConditionParams.Count;
                    }
                }
            }

            Console.WriteLine($"\nSummary:");
            Console.WriteLine($"  Entries with ActionParams: {entriesWithParams}/{dlg.Entries.Count}, total params: {totalActionParams}");
            Console.WriteLine($"  Pointers with ConditionParams: {pointersWithParams}, total cond params: {totalCondParams}");
        }
    }
}
