using System.Collections.Generic;

namespace DialogEditor.Services
{
    /// <summary>
    /// Container for all scrap data
    /// </summary>
    public class ScrapData
    {
        public List<ScrapEntry> Entries { get; set; } = new List<ScrapEntry>();
        public int Version { get; set; } = 1;
    }
}
