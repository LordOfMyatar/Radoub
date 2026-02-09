using DialogEditor.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Result of a restore operation with status information
    /// </summary>
    public class RestoreResult
    {
        public bool Success { get; set; }
        public string StatusMessage { get; set; } = "";
        public DialogNode? RestoredNode { get; set; }
    }
}
