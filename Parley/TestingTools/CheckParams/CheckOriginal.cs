using System;
using System.Threading.Tasks;
using DialogEditor.Parsers;

var parser = new DialogParser();
var dlg = await parser.ParseFromFileAsync(@"~\Documents\Neverwinter Nights\modules\LNS_DLG\chef.dlg");

int totalActionParams = 0;
foreach (var entry in dlg.Entries) {
    if (entry.ActionParams != null && entry.ActionParams.Count > 0) {
        totalActionParams += entry.ActionParams.Count;
    }
}
Console.WriteLine($"ORIGINAL chef.dlg has {totalActionParams} total ActionParams");
