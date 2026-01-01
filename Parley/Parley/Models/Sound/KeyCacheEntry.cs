using System;
using Radoub.Formats.Key;

namespace DialogEditor.Models.Sound
{
    /// <summary>
    /// Cached KEY file data to avoid re-parsing on each browser open.
    /// </summary>
    public class KeyCacheEntry
    {
        public string KeyPath { get; set; } = "";
        public DateTime LastModified { get; set; }
        public KeyFile? KeyFile { get; set; }
    }
}
