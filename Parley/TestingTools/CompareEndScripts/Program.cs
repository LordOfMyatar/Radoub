using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Services;


class Program
{
    static async Task Main(string[] args)
    {
        
        var service = new DialogFileService();
        
        var chefPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\chef.dlg";
        var exportedPath = @"~\Documents\Neverwinter Nights\modules\LNS_DLG\__chef.dlg";
        
        Console.WriteLine("Loading chef.dlg (original)...");
        var chefDialog = await service.LoadFromFileAsync(chefPath);
        
        Console.WriteLine("Loading __chef.dlg (exported)...");
        var exportedDialog = await service.LoadFromFileAsync(exportedPath);
        
        Console.WriteLine("\n=== ORIGINAL chef.dlg ===");
        Console.WriteLine($"EndConversation (ScriptEnd): '{chefDialog.ScriptEnd}'");
        Console.WriteLine($"EndConverAbort (ScriptAbort): '{chefDialog.ScriptAbort}'");
        Console.WriteLine($"PreventZoom: {chefDialog.PreventZoom}");
        
        Console.WriteLine("\n=== EXPORTED __chef.dlg ===");
        Console.WriteLine($"EndConversation (ScriptEnd): '{exportedDialog.ScriptEnd}'");
        Console.WriteLine($"EndConverAbort (ScriptAbort): '{exportedDialog.ScriptAbort}'");
        Console.WriteLine($"PreventZoom: {exportedDialog.PreventZoom}");
        
        Console.WriteLine("\n=== DIFFERENCES ===");
        if (chefDialog.ScriptEnd != exportedDialog.ScriptEnd)
        {
            Console.WriteLine($"⚠️  EndConversation DIFFERS:");
            Console.WriteLine($"   Original: '{chefDialog.ScriptEnd}'");
            Console.WriteLine($"   Exported: '{exportedDialog.ScriptEnd}'");
        }
        else
        {
            Console.WriteLine($"✓ EndConversation MATCHES: '{chefDialog.ScriptEnd}'");
        }
        
        if (chefDialog.ScriptAbort != exportedDialog.ScriptAbort)
        {
            Console.WriteLine($"⚠️  EndConverAbort DIFFERS:");
            Console.WriteLine($"   Original: '{chefDialog.ScriptAbort}'");
            Console.WriteLine($"   Exported: '{exportedDialog.ScriptAbort}'");
        }
        else
        {
            Console.WriteLine($"✓ EndConverAbort MATCHES: '{chefDialog.ScriptAbort}'");
        }
        
        if (chefDialog.PreventZoom != exportedDialog.PreventZoom)
        {
            Console.WriteLine($"⚠️  PreventZoom DIFFERS:");
            Console.WriteLine($"   Original: {chefDialog.PreventZoom}");
            Console.WriteLine($"   Exported: {exportedDialog.PreventZoom}");
        }
        else
        {
            Console.WriteLine($"✓ PreventZoom MATCHES: {chefDialog.PreventZoom}");
        }
        
        Console.WriteLine("\n=== CHECK: Are values empty? ===");
        Console.WriteLine($"Original ScriptEnd empty? {string.IsNullOrEmpty(chefDialog.ScriptEnd)}");
        Console.WriteLine($"Original ScriptAbort empty? {string.IsNullOrEmpty(chefDialog.ScriptAbort)}");
        Console.WriteLine($"Exported ScriptEnd empty? {string.IsNullOrEmpty(exportedDialog.ScriptEnd)}");
        Console.WriteLine($"Exported ScriptAbort empty? {string.IsNullOrEmpty(exportedDialog.ScriptAbort)}");
    }
}
