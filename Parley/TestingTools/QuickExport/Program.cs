using System;
using DialogEditor.Services;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: QuickExport <input.dlg> <output.dlg>");
            return;
        }

        var service = new DialogFileService();
        Console.WriteLine($"Loading {args[0]}...");
        var dialog = parser.LoadFrom(args[0]);

        Console.WriteLine($"Saving to {args[1]}...");
        parser.SaveTo(dialog, args[1]);

        Console.WriteLine("Done!");
    }
}
