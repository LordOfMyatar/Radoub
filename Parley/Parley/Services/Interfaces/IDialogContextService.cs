using System;
using System.Collections.Generic;
using DialogEditor.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Interface for accessing the current dialog context.
    /// Provides dialog state without direct ViewModel coupling.
    /// #1230: Phase 3 - Service interface extraction for dependency injection.
    /// </summary>
    public interface IDialogContextService
    {
        /// <summary>
        /// Current loaded dialog (may be null if no dialog is open).
        /// </summary>
        Dialog? CurrentDialog { get; set; }

        /// <summary>
        /// Current dialog file name (without path).
        /// </summary>
        string? CurrentFileName { get; set; }

        /// <summary>
        /// Current dialog file full path (for script search, etc.).
        /// </summary>
        string? CurrentFilePath { get; set; }

        /// <summary>
        /// Event fired when dialog changes (load, close, modify).
        /// </summary>
        event EventHandler? DialogChanged;

        /// <summary>
        /// Manually notify that dialog content has changed.
        /// </summary>
        void NotifyDialogChanged();

        /// <summary>
        /// Get dialog structure as nodes and links for flowchart visualization.
        /// </summary>
        (List<DialogNodeInfo> Nodes, List<DialogLinkInfo> Links) GetDialogStructure();
    }
}
