using System;
using System.Linq;

namespace DumpChildLinks
{
    public static class CompareFiles
    {
        public static async System.Threading.Tasks.Task CompareDialogs()
        {
            var parser = new ArcReactor.Parsers.DialogParser();
            
            var original = await parser.ParseFromFileAsync(@"~\Documents\Neverwinter Nights\modules\LNS_DLG\act1_g_ashera.dlg");
            var exported = await parser.ParseFromFileAsync(@"~\Documents\Neverwinter Nights\modules\LNS_DLG\ashera01.dlg");
            
            Console.WriteLine("=== ORIGINAL FILE ===");
            Console.WriteLine($"Entries: {original.Entries.Count}, Replies: {original.Replies.Count}");
            Console.WriteLine($"Entry[1] pointers with scripts: {original.Entries[1].Pointers.Count(p => !string.IsNullOrEmpty(p.ScriptAppears))}");
            
            Console.WriteLine();
            Console.WriteLine("=== EXPORTED FILE ===");
            Console.WriteLine($"Entries: {exported.Entries.Count}, Replies: {exported.Replies.Count}");
            Console.WriteLine($"Entry[1] pointers with scripts: {exported.Entries[1].Pointers.Count(p => !string.IsNullOrEmpty(p.ScriptAppears))}");
            
            Console.WriteLine();
            Console.WriteLine("=== ORIGINAL Entry[1] Pointers ===");
            foreach (var ptr in original.Entries[1].Pointers)
            {
                Console.WriteLine($"  -> Reply[{ptr.Index}] Script='{ptr.ScriptAppears}' ConditionParams={ptr.ConditionParams.Count}");
            }
            
            Console.WriteLine();
            Console.WriteLine("=== EXPORTED Entry[1] Pointers ===");
            foreach (var ptr in exported.Entries[1].Pointers)
            {
                Console.WriteLine($"  -> Reply[{ptr.Index}] Script='{ptr.ScriptAppears}' ConditionParams={ptr.ConditionParams.Count}");
            }
        }
    }
}
