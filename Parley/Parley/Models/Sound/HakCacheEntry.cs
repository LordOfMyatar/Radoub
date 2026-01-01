using System;
using System.Collections.Generic;

namespace DialogEditor.Models.Sound
{
    /// <summary>
    /// Cached HAK file data to avoid re-scanning on each browser open.
    /// </summary>
    public class HakCacheEntry
    {
        public string HakPath { get; set; } = "";
        public DateTime LastModified { get; set; }
        public List<SoundFileInfo> Sounds { get; set; } = new();
    }
}
