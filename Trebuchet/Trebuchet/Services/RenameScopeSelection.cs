using System;
using System.Collections.Generic;
using System.Linq;

namespace RadoubLauncher.Services;

/// <summary>
/// Tri-state for a group (extension) checkbox in the Marlinspike results tree (#2179).
/// </summary>
public enum GroupCheckState
{
    /// <summary>No files under the group are checked.</summary>
    None,
    /// <summary>Some but not all files under the group are checked.</summary>
    Partial,
    /// <summary>All files under the group are checked.</summary>
    All
}

/// <summary>
/// Pure selection model for the Marlinspike results tree's checkbox-per-row rename
/// scope (#2179). The view binds checkboxes to this model and reads
/// <see cref="SelectedFilePaths"/> when the user clicks "Replace Selected" — no
/// dependence on tree highlight. Kept free of any Avalonia type so the cascade and
/// tri-state rules are unit-testable without FlaUI.
///
/// Files are grouped by an arbitrary group key (the file extension in the UI). Each
/// file path is unique within the model; checking a group checks every file under it,
/// and the group's tri-state is derived from its files.
/// </summary>
public sealed class RenameScopeSelection
{
    // group key -> file paths (insertion order preserved for stable UI).
    private readonly Dictionary<string, List<string>> _groups = new();
    // file path -> checked. Case-insensitive to match Aurora filename handling.
    private readonly Dictionary<string, bool> _checked = new(StringComparer.OrdinalIgnoreCase);
    // file path -> group key, for fast group lookup on a per-file toggle.
    private readonly Dictionary<string, string> _fileGroup = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Register a file under a group. Default checked state is true (every result
    /// starts selected, matching the prior "Replace All by default" expectation).
    /// Re-adding the same path is a no-op for grouping but resets its checked state.
    /// </summary>
    public void AddFile(string groupKey, string filePath, bool isChecked = true)
    {
        if (string.IsNullOrEmpty(groupKey)) throw new ArgumentException("groupKey required", nameof(groupKey));
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentException("filePath required", nameof(filePath));

        if (!_groups.TryGetValue(groupKey, out var list))
        {
            list = new List<string>();
            _groups[groupKey] = list;
        }
        if (!_fileGroup.ContainsKey(filePath))
        {
            list.Add(filePath);
            _fileGroup[filePath] = groupKey;
        }
        _checked[filePath] = isChecked;
    }

    /// <summary>Set the checked state of a single file row.</summary>
    public void SetFileChecked(string filePath, bool isChecked)
    {
        if (_checked.ContainsKey(filePath))
            _checked[filePath] = isChecked;
    }

    /// <summary>Check/uncheck every file under a group (group-checkbox cascade).</summary>
    public void SetGroupChecked(string groupKey, bool isChecked)
    {
        if (!_groups.TryGetValue(groupKey, out var list)) return;
        foreach (var path in list)
            _checked[path] = isChecked;
    }

    /// <summary>Check/uncheck every file in every group (select-all / deselect-all).</summary>
    public void SetAllChecked(bool isChecked)
    {
        foreach (var path in _checked.Keys.ToList())
            _checked[path] = isChecked;
    }

    /// <summary>Current checked state of one file (false if unknown).</summary>
    public bool IsFileChecked(string filePath) =>
        _checked.TryGetValue(filePath, out var v) && v;

    /// <summary>Derived tri-state for a group's checkbox.</summary>
    public GroupCheckState GetGroupState(string groupKey)
    {
        if (!_groups.TryGetValue(groupKey, out var list) || list.Count == 0)
            return GroupCheckState.None;

        var checkedCount = list.Count(p => _checked.TryGetValue(p, out var v) && v);
        if (checkedCount == 0) return GroupCheckState.None;
        if (checkedCount == list.Count) return GroupCheckState.All;
        return GroupCheckState.Partial;
    }

    /// <summary>
    /// The set of checked file paths — the rename scope. Feeds the existing
    /// selectionFilter consumed by OpenReplacePreviewAsync / RenameDispatchHelpers.
    /// </summary>
    public IReadOnlySet<string> SelectedFilePaths =>
        _checked.Where(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>True when at least one file is checked.</summary>
    public bool HasSelection => _checked.Values.Any(v => v);

    /// <summary>Drop all groups and files (called when the results tree is rebuilt).</summary>
    public void Clear()
    {
        _groups.Clear();
        _checked.Clear();
        _fileGroup.Clear();
    }
}
