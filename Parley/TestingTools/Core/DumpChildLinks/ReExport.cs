using System;
using System.Threading.Tasks;
using DialogEditor.Services;

namespace DumpChildLinks
{
    public static class ReExport
    {
        public static async Task ExportFile()
        {
            var service = new DialogFileService();

            Console.WriteLine("Loading original file...");
            var dialog = await service.LoadFromFileAsync(@"~\Documents\Neverwinter Nights\modules\LNS_DLG\act1_g_ashera.dlg");

            if (dialog == null)
            {
                Console.WriteLine("ERROR: Failed to load original file");
                return;
            }

            Console.WriteLine($"Loaded: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies");
            Console.WriteLine($"Entry[1] has {dialog.Entries[1].Pointers.Count} pointers");
            foreach (var ptr in dialog.Entries[1].Pointers)
            {
                Console.WriteLine($"  -> Reply[{ptr.Index}] Script='{ptr.ScriptAppears}' Params={ptr.ConditionParams.Count}");
            }

            Console.WriteLine("\nExporting to ashera01_new.dlg...");
            bool success = await service.SaveToFileAsync(dialog, @"~\Documents\Neverwinter Nights\modules\LNS_DLG\ashera01_new.dlg");

            if (!success)
            {
                Console.WriteLine("ERROR: Export failed");
                return;
            }

            Console.WriteLine("Export successful!");

            Console.WriteLine("\nReloading exported file...");
            var exported = await service.LoadFromFileAsync(@"~\Documents\Neverwinter Nights\modules\LNS_DLG\ashera01_new.dlg");

            if (exported == null)
            {
                Console.WriteLine("ERROR: Failed to reload exported file");
                return;
            }

            Console.WriteLine($"Reloaded: {exported.Entries.Count} entries, {exported.Replies.Count} replies");
            Console.WriteLine($"Entry[1] has {exported.Entries[1].Pointers.Count} pointers");
            foreach (var ptr in exported.Entries[1].Pointers)
            {
                Console.WriteLine($"  -> Reply[{ptr.Index}] Script='{ptr.ScriptAppears}' Params={ptr.ConditionParams.Count}");
            }
        }
    }
}
