using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Services;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("=== Parameter Diagnostic Tool ===\n");

        string filePath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\__chef.dlg";

        var service = new DialogFileService();

        Console.WriteLine($"Loading: {filePath}");
        var dialog = await service.LoadFromFileAsync(filePath);

        if (dialog == null)
        {
            Console.WriteLine("Failed to load file!");
            return;
        }

        Console.WriteLine($"\nLoaded: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies\n");

        // Check Entries for ActionParams
        Console.WriteLine("=== ENTRY ACTION PARAMETERS ===");
        for (int i = 0; i < dialog.Entries.Count; i++)
        {
            var entry = dialog.Entries[i];
            if (entry.ActionParams != null && entry.ActionParams.Count > 0)
            {
                Console.WriteLine($"Entry[{i}]: Script='{entry.ScriptAction}', ActionParams.Count={entry.ActionParams.Count}");
                foreach (var param in entry.ActionParams)
                {
                    Console.WriteLine($"  - Param: {param}");
                }
            }
        }

        // Check Replies for ActionParams
        Console.WriteLine("\n=== REPLY ACTION PARAMETERS ===");
        for (int i = 0; i < dialog.Replies.Count; i++)
        {
            var reply = dialog.Replies[i];
            if (reply.ActionParams != null && reply.ActionParams.Count > 0)
            {
                Console.WriteLine($"Reply[{i}]: Script='{reply.ScriptAction}', ActionParams.Count={reply.ActionParams.Count}");
                foreach (var param in reply.ActionParams)
                {
                    Console.WriteLine($"  - Param: {param}");
                }
            }
        }

        // Check pointer ConditionParams (entries)
        Console.WriteLine("\n=== POINTER CONDITION PARAMETERS (Entries) ===");
        for (int i = 0; i < dialog.Entries.Count; i++)
        {
            var entry = dialog.Entries[i];
            for (int p = 0; p < entry.Pointers.Count; p++)
            {
                var ptr = entry.Pointers[p];
                if (ptr.ConditionParams != null && ptr.ConditionParams.Count > 0)
                {
                    Console.WriteLine($"Entry[{i}].Pointer[{p}]: Script='{ptr.ScriptAppears}', ConditionParams.Count={ptr.ConditionParams.Count}");
                    foreach (var param in ptr.ConditionParams)
                    {
                        Console.WriteLine($"  - Param: {param}");
                    }
                }
            }
        }

        // Check pointer ConditionParams (replies)
        Console.WriteLine("\n=== POINTER CONDITION PARAMETERS (Replies) ===");
        for (int i = 0; i < dialog.Replies.Count; i++)
        {
            var reply = dialog.Replies[i];
            for (int p = 0; p < reply.Pointers.Count; p++)
            {
                var ptr = reply.Pointers[p];
                if (ptr.ConditionParams != null && ptr.ConditionParams.Count > 0)
                {
                    Console.WriteLine($"Reply[{i}].Pointer[{p}]: Script='{ptr.ScriptAppears}', ConditionParams.Count={ptr.ConditionParams.Count}");
                    foreach (var param in ptr.ConditionParams)
                    {
                        Console.WriteLine($"  - Param: {param}");
                    }
                }
            }
        }

        // Check start ConditionParams
        Console.WriteLine("\n=== START CONDITION PARAMETERS ===");
        for (int i = 0; i < dialog.Starts.Count; i++)
        {
            var start = dialog.Starts[i];
            if (start.ConditionParams != null && start.ConditionParams.Count > 0)
            {
                Console.WriteLine($"Start[{i}]: Script='{start.ScriptAppears}', ConditionParams.Count={start.ConditionParams.Count}");
                foreach (var param in start.ConditionParams)
                {
                    Console.WriteLine($"  - Param: {param}");
                }
            }
        }

        Console.WriteLine("\n=== SUMMARY ===");
        int totalActionParams = 0;
        int totalConditionParams = 0;

        foreach (var entry in dialog.Entries)
        {
            if (entry.ActionParams != null) totalActionParams += entry.ActionParams.Count;
            foreach (var ptr in entry.Pointers)
                if (ptr.ConditionParams != null) totalConditionParams += ptr.ConditionParams.Count;
        }

        foreach (var reply in dialog.Replies)
        {
            if (reply.ActionParams != null) totalActionParams += reply.ActionParams.Count;
            foreach (var ptr in reply.Pointers)
                if (ptr.ConditionParams != null) totalConditionParams += ptr.ConditionParams.Count;
        }

        foreach (var start in dialog.Starts)
            if (start.ConditionParams != null) totalConditionParams += start.ConditionParams.Count;

        Console.WriteLine($"Total ActionParams: {totalActionParams}");
        Console.WriteLine($"Total ConditionParams: {totalConditionParams}");
    }
}
