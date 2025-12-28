using System;
using System.IO;
using System.Linq;
using DialogEditor.Services;

namespace DumpChildLinks
{
    public static class CheckScripts
    {
        public static void DumpScriptNames(string filePath)
        {
            var service = new DialogFileService();
            var dialog = service.LoadFromFileAsync(filePath).Result;

            if (dialog == null)
            {
                Console.WriteLine("Failed to load dialog");
                return;
            }

            Console.WriteLine("=== STARTING LIST SCRIPTS ===");
            for (int i = 0; i < dialog.Starts.Count; i++)
            {
                var start = dialog.Starts[i];
                Console.WriteLine($"Start[{i}] Index={start.Index}");
                Console.WriteLine($"  ScriptAppears: '{start.ScriptAppears}'");
                Console.WriteLine($"  Length: {start.ScriptAppears?.Length ?? 0}");
                if (!string.IsNullOrEmpty(start.ScriptAppears))
                {
                    Console.WriteLine($"  First char: '{start.ScriptAppears[0]}' (code: {(int)start.ScriptAppears[0]})");
                }
            }

            Console.WriteLine();
            Console.WriteLine("=== ENTRY SCRIPTS ===");
            for (int i = 0; i < dialog.Entries.Count; i++)
            {
                var entry = dialog.Entries[i];
                foreach (var ptr in entry.Pointers)
                {
                    if (!string.IsNullOrEmpty(ptr.ScriptAppears))
                    {
                        Console.WriteLine($"Entry[{i}] -> Reply[{ptr.Index}]");
                        Console.WriteLine($"  ScriptAppears: '{ptr.ScriptAppears}'");
                        Console.WriteLine($"  Length: {ptr.ScriptAppears.Length}");
                        Console.WriteLine($"  First char: '{ptr.ScriptAppears[0]}' (code: {(int)ptr.ScriptAppears[0]})");
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("=== REPLY SCRIPTS ===");
            for (int i = 0; i < dialog.Replies.Count; i++)
            {
                var reply = dialog.Replies[i];
                foreach (var ptr in reply.Pointers)
                {
                    if (!string.IsNullOrEmpty(ptr.ScriptAppears))
                    {
                        Console.WriteLine($"Reply[{i}] -> Entry[{ptr.Index}]");
                        Console.WriteLine($"  ScriptAppears: '{ptr.ScriptAppears}'");
                        Console.WriteLine($"  Length: {ptr.ScriptAppears.Length}");
                        Console.WriteLine($"  First char: '{ptr.ScriptAppears[0]}' (code: {(int)ptr.ScriptAppears[0]})");
                    }
                }
            }
        }
    }
}
