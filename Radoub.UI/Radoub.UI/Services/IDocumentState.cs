using System;
using System.Runtime.CompilerServices;
using Radoub.Formats.Logging;

namespace Radoub.UI.Services;

/// <summary>
/// Interface for tracking document dirty state and generating title bar text.
/// Provides a consistent pattern across all Radoub tools for:
/// - Dirty flag tracking with load guard
/// - Title bar text generation with dirty indicator
/// - MarkDirty/ClearDirty lifecycle
/// </summary>
public interface IDocumentState
{
    /// <summary>
    /// Whether the document has unsaved changes.
    /// </summary>
    bool IsDirty { get; }

    /// <summary>
    /// Whether the document is currently loading (suppresses dirty marking).
    /// </summary>
    bool IsLoading { get; set; }

    /// <summary>
    /// Current file path, or null if no file is loaded.
    /// </summary>
    string? CurrentFilePath { get; set; }

    /// <summary>
    /// Mark the document as having unsaved changes.
    /// Blocked while IsLoading is true or when no file is loaded.
    /// </summary>
    void MarkDirty([CallerMemberName] string? caller = null);

    /// <summary>
    /// Clear the dirty flag (after save or load).
    /// </summary>
    void ClearDirty();

    /// <summary>
    /// Raised when the dirty state changes. Subscribe to update the title bar.
    /// </summary>
    event Action? DirtyStateChanged;
}

/// <summary>
/// Default implementation of IDocumentState.
/// Tools can use this directly or implement IDocumentState themselves.
///
/// Usage:
///   _documentState = new DocumentState("Quartermaster");
///   _documentState.DirtyStateChanged += () => Title = _documentState.GetTitle();
/// </summary>
public class DocumentState : IDocumentState
{
    private readonly string _toolName;
    private readonly string? _titleSuffix;
    private bool _isDirty;

    /// <summary>
    /// Creates a new DocumentState tracker.
    /// </summary>
    /// <param name="toolName">Tool name for title bar (e.g., "Quartermaster")</param>
    /// <param name="titleSuffix">Optional suffix for the tool name (e.g., " - Merchant Editor")</param>
    public DocumentState(string toolName, string? titleSuffix = null)
    {
        _toolName = toolName;
        _titleSuffix = titleSuffix;
    }

    public bool IsDirty => _isDirty;
    public bool IsLoading { get; set; }
    public string? CurrentFilePath { get; set; }

    public event Action? DirtyStateChanged;

    public void MarkDirty([CallerMemberName] string? caller = null)
    {
        if (IsLoading)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"MarkDirty: Blocked (isLoading=true) from {caller}");
            return;
        }

        if (CurrentFilePath == null)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"MarkDirty: Blocked (no file loaded) from {caller}");
            return;
        }

        if (!_isDirty)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"MarkDirty: Setting dirty from {caller}");
            _isDirty = true;
            DirtyStateChanged?.Invoke();
        }
    }

    public void ClearDirty()
    {
        if (_isDirty)
        {
            _isDirty = false;
            DirtyStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Generates a title bar string with the standard format:
    /// "ToolName - filepath*" or "ToolName" if no file loaded.
    /// </summary>
    /// <param name="extraInfo">Optional extra info to include (e.g., " (Player)" for BIC files)</param>
    public string GetTitle(string? extraInfo = null)
    {
        if (CurrentFilePath == null)
        {
            return _titleSuffix != null ? $"{_toolName}{_titleSuffix}" : _toolName;
        }

        var displayPath = UnifiedLogger.SanitizePath(CurrentFilePath);
        var dirty = _isDirty ? "*" : "";
        var extra = extraInfo ?? "";
        return $"{_toolName} - {displayPath}{extra}{dirty}";
    }
}
