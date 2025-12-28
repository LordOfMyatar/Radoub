using System;
using System.IO;
using DialogEditor.Services;
using DialogEditor.Models;

string origPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_orig.dlg";
string lista01Path = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista01.dlg";
string researchPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\lista_research.dlg";

Console.WriteLine("=== LISTA_ORIG (2.7K - Original format, instant load) ===");
await AnalyzeFile(origPath);

Console.WriteLine("\n=== LISTA01 (3.8K - Our export, 60sec load) ===");
await AnalyzeFile(lista01Path);

Console.WriteLine("\n=== LISTA_RESEARCH (3.9K - After Format correction) ===");
await AnalyzeFile(researchPath);

static async Task AnalyzeFile(string path)
{
    var service = new DialogFileService();
    var dialog = await service.LoadFromFileAsync(path);

    if (dialog != null)
    {
        Console.WriteLine($"Entries: {dialog.Entries.Count}");
        Console.WriteLine($"Replies: {dialog.Replies.Count}");
        Console.WriteLine($"Starts: {dialog.Starts.Count}");

        Console.WriteLine("\nEntry texts:");
        for (int i = 0; i < dialog.Entries.Count; i++)
        {
            var text = dialog.Entries[i].Text?.GetDefault() ?? "(empty)";
            var display = text.Length > 60 ? text.Substring(0, 60) + "..." : text;
            Console.WriteLine($"  Entry[{i}]: '{display}'");
        }

        Console.WriteLine("\nReply texts:");
        for (int i = 0; i < dialog.Replies.Count; i++)
        {
            var text = dialog.Replies[i].Text?.GetDefault() ?? "(empty)";
            var display = text.Length > 60 ? text.Substring(0, 60) + "..." : text;
            Console.WriteLine($"  Reply[{i}]: '{display}'");
        }

        Console.WriteLine("\nStarting entries:");
        for (int i = 0; i < dialog.Starts.Count; i++)
        {
            var node = dialog.Starts[i].Node;
            var nodeText = node?.Text?.GetDefault() ?? "(null node)";
            Console.WriteLine($"  Start[{i}]: Index={dialog.Starts[i].Index}, Node='{nodeText}'");
        }

        Console.WriteLine($"\nFile size: {new FileInfo(path).Length} bytes");
    }
    else
    {
        Console.WriteLine("Failed to parse file!");
    }
}
