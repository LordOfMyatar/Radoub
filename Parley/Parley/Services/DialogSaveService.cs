using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DialogEditor.Models;
using DialogEditor.Utils;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service for saving dialog files with validation, cleanup, and status updates.
    /// Extracted from MainViewModel.cs to reduce complexity (Epic #163 pattern).
    /// </summary>
    public class DialogSaveService
    {
        private readonly OrphanNodeManager _orphanManager;
        private readonly IndexManager _indexManager;
        private readonly DialogFileService _dialogFileService;

        public DialogSaveService()
        {
            _orphanManager = new OrphanNodeManager();
            _indexManager = new IndexManager();
            _dialogFileService = new DialogFileService();
        }

        /// <summary>
        /// Result of a save operation with status information
        /// </summary>
        public class SaveResult
        {
            public bool Success { get; init; }
            public string StatusMessage { get; init; }
            public string? ErrorMessage { get; init; }

            private SaveResult(bool success, string statusMessage, string? errorMessage = null)
            {
                Success = success;
                StatusMessage = statusMessage;
                ErrorMessage = errorMessage;
            }

            public static SaveResult Successful(string statusMessage) =>
                new(true, statusMessage);

            public static SaveResult Failed(string statusMessage, string? errorMessage = null) =>
                new(false, statusMessage, errorMessage);
        }

        /// <summary>
        /// Saves a dialog to a file with full validation and cleanup.
        /// </summary>
        /// <param name="dialog">Dialog to save</param>
        /// <param name="filePath">File path to save to</param>
        /// <returns>SaveResult with success status and messages</returns>
        public async Task<SaveResult> SaveDialogAsync(Dialog dialog, string filePath)
        {
            if (dialog == null)
            {
                return SaveResult.Failed("No dialog to save", "Dialog is null");
            }

            if (string.IsNullOrEmpty(filePath))
            {
                return SaveResult.Failed("No file path specified", "File path is null or empty");
            }

            try
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Saving dialog to: {UnifiedLogger.SanitizePath(filePath)}");

                // CLEANUP: Remove orphaned nodes before save (nodes with no incoming pointers)
                var orphanedNodes = _orphanManager.RemoveOrphanedNodes(dialog);
                if (orphanedNodes.Count > 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"Removed {orphanedNodes.Count} orphaned nodes before save");
                    // Note: Orphaned nodes are removed from dialog, not added to scrap
                    // This is cleanup, not user-initiated deletion
                }

                // SAFETY VALIDATION: Validate all pointer indices before save (Issue #6 fix)
                var validationErrors = dialog.ValidatePointerIndices();
                if (validationErrors.Count > 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"⚠️ PRE-SAVE VALIDATION: Found {validationErrors.Count} index issues:");
                    foreach (var error in validationErrors)
                    {
                        UnifiedLogger.LogApplication(LogLevel.WARN, $"  - {error}");
                    }

                    // Attempt to fix by rebuilding LinkRegistry and recalculating indices
                    UnifiedLogger.LogApplication(LogLevel.INFO, "Attempting to auto-fix index issues...");
                    dialog.RebuildLinkRegistry();
                    _indexManager.RecalculatePointerIndices(dialog);

                    // Re-validate after fix attempt
                    var errorsAfterFix = dialog.ValidatePointerIndices();
                    if (errorsAfterFix.Count > 0)
                    {
                        UnifiedLogger.LogApplication(LogLevel.ERROR, $"❌ CRITICAL: {errorsAfterFix.Count} index issues remain after auto-fix!");
                        return SaveResult.Failed(
                            $"ERROR: Dialog has {errorsAfterFix.Count} pointer index issues. Save aborted to prevent corruption.",
                            $"{errorsAfterFix.Count} pointer index validation errors");
                    }
                    else
                    {
                        UnifiedLogger.LogApplication(LogLevel.INFO, "✅ All index issues resolved successfully");
                    }
                }

                // Log parameter counts before writing
                int totalActionParams = dialog.Entries.Sum(e => e.ActionParams.Count) + dialog.Replies.Sum(r => r.ActionParams.Count);
                int totalConditionParams = dialog.Entries.Sum(e => e.Pointers.Sum(p => p.ConditionParams.Count))
                                         + dialog.Replies.Sum(r => r.Pointers.Sum(p => p.ConditionParams.Count));
                UnifiedLogger.LogApplication(LogLevel.INFO, $"💾 SAVE: Dialog model has TotalActionParams={totalActionParams}, TotalConditionParams={totalConditionParams} before write");

                // Log entry order at save time
                UnifiedLogger.LogApplication(LogLevel.INFO, $"💾 SAVE: Entry list order (Count={dialog.Entries.Count}):");
                for (int i = 0; i < Math.Min(dialog.Entries.Count, 10); i++) // Limit to first 10 for brevity
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"  Entry[{i}] = '{dialog.Entries[i].Text}'");
                }
                if (dialog.Entries.Count > 10)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"  ... and {dialog.Entries.Count - 10} more entries");
                }

                // Determine output format based on file extension
                var extension = Path.GetExtension(filePath).ToLower();
                bool success = false;

                if (extension == ".json")
                {
                    var json = await _dialogFileService.ConvertToJsonAsync(dialog);
                    if (!string.IsNullOrEmpty(json))
                    {
                        await File.WriteAllTextAsync(filePath, json);
                        success = true;
                    }
                }
                else
                {
                    success = await _dialogFileService.SaveToFileAsync(dialog, filePath);
                }

                if (success)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, "Dialog saved successfully");
                    return SaveResult.Successful("Dialog saved successfully");
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR, "Failed to save dialog - parser returned false");
                    return SaveResult.Failed("Failed to save dialog", "DialogFileService returned false");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save dialog: {ex.Message}");
                return SaveResult.Failed($"Error saving dialog: {ex.Message}", ex.ToString());
            }
        }
    }
}
