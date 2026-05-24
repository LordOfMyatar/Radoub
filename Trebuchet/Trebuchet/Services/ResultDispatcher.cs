using System;
using System.Collections.Generic;

namespace RadoubLauncher.Services;

/// <summary>
/// Action chosen by <see cref="ResultDispatcher.Plan"/> for a Marlinspike result
/// double-click. The caller performs the side effect; this enum + record stays
/// pure so the decision tree is fully unit-testable.
/// </summary>
public enum DispatchAction
{
    NoFile,
    FileMissing,
    ToolLaunch,
    ExternalEditor,
    OsDefault
}

public record DispatchPlan(
    DispatchAction Action,
    string? ToolName,
    string? EditorPath,
    string? FilePath);

/// <summary>
/// Pure decision logic for Marlinspike result dispatch (#2183).
///
/// Priority:
/// 1. <c>NoFile</c> — no path supplied
/// 2. <c>FileMissing</c> — path supplied but not on disk
/// 3. <c>ToolLaunch</c> — resource type maps to a Radoub tool
/// 4. <c>ExternalEditor</c> — <paramref name="codeEditorPath"/> set and exists
/// 5. <c>OsDefault</c> — fall back to OS shell handler
/// </summary>
public static class ResultDispatcher
{
    public static DispatchPlan Plan(
        string? filePath,
        ushort resourceType,
        IReadOnlyDictionary<ushort, string> resourceToolMap,
        string? codeEditorPath,
        Func<string, bool> fileExists)
    {
        if (string.IsNullOrEmpty(filePath))
            return new DispatchPlan(DispatchAction.NoFile, null, null, null);

        if (!fileExists(filePath))
            return new DispatchPlan(DispatchAction.FileMissing, null, null, filePath);

        if (resourceToolMap.TryGetValue(resourceType, out var toolName))
            return new DispatchPlan(DispatchAction.ToolLaunch, toolName, null, filePath);

        if (!string.IsNullOrEmpty(codeEditorPath) && fileExists(codeEditorPath))
            return new DispatchPlan(DispatchAction.ExternalEditor, null, codeEditorPath, filePath);

        return new DispatchPlan(DispatchAction.OsDefault, null, null, filePath);
    }
}
