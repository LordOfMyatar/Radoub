using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DialogEditor.Parsers;

class PointerDump
{
    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: PointerDump <path-to-dlg-file>");
            return;
        }

        var parser = new DialogParser();
        var dialog = await parser.ParseFromFileAsync(args[0]);

        if (dialog == null)
        {
            Console.WriteLine("Failed to parse dialog");
            return;
        }

        Console.WriteLine("\n=== ENTRY POINTERS ===");
        for (int i = 0; i < dialog.Entries.Count; i++)
        {
            var entry = dialog.Entries[i];
            Console.WriteLine($"\nEntry[{i}]: {entry.DisplayText}");
            Console.WriteLine($"  Pointers: {entry.Pointers.Count}");

            for (int p = 0; p < entry.Pointers.Count; p++)
            {
                var ptr = entry.Pointers[p];
                Console.WriteLine($"    [{p}] Index={ptr.Index}, IsLink={ptr.IsLink}, Type={ptr.Type}, Target={ptr.Node?.DisplayText}");
            }
        }

        Console.WriteLine("\n=== REPLY POINTERS ===");
        for (int i = 0; i < dialog.Replies.Count; i++)
        {
            var reply = dialog.Replies[i];
            Console.WriteLine($"\nReply[{i}]: {reply.DisplayText}");
            Console.WriteLine($"  Pointers: {reply.Pointers.Count}");

            for (int p = 0; p < reply.Pointers.Count; p++)
            {
                var ptr = reply.Pointers[p];
                Console.WriteLine($"    [{p}] Index={ptr.Index}, IsLink={ptr.IsLink}, Type={ptr.Type}, Target={ptr.Node?.DisplayText}");
            }
        }
    }
}
