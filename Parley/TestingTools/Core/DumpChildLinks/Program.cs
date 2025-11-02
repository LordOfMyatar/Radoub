using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

// Add reference to ArcReactor parser
#nullable enable

namespace DumpChildLinks
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Re-export file with fixed code
            await ReExport.ExportFile();
        }
    }
}
