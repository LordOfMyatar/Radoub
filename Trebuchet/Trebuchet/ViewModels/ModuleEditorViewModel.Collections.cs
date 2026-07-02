using System;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Gff;
using VariableViewModel = Radoub.UI.ViewModels.VariableViewModel;

namespace RadoubLauncher.ViewModels;

// HAK list and variable management commands
public partial class ModuleEditorViewModel
{
    // HAK List Commands

    [RelayCommand]
    private void AddHak()
    {
        AddHakByName(NewHakName);
        NewHakName = string.Empty;
    }

    /// <summary>
    /// Append a HAK to the list by name (extension optional), deduplicating case-insensitively
    /// and marking the module dirty when it actually changes. Shared by the HAK-list "Add" field
    /// and the "New HAK → register in module IFO" flow (#2267).
    /// </summary>
    /// <returns>True if the HAK was added; false when blank or already present.</returns>
    public bool AddHakByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        var hakName = name.Trim();
        // Remove .hak extension if present
        if (hakName.EndsWith(".hak", StringComparison.OrdinalIgnoreCase))
            hakName = hakName[..^4];

        if (string.IsNullOrEmpty(hakName)) return false;
        if (HakList.Contains(hakName, StringComparer.OrdinalIgnoreCase)) return false;

        HakList.Add(hakName);
        HasUnsavedChanges = true;
        return true;
    }

    [RelayCommand]
    private void RemoveHak()
    {
        if (SelectedHak != null)
        {
            HakList.Remove(SelectedHak);
            HasUnsavedChanges = true;
        }
    }

    [RelayCommand]
    private void MoveHakUp()
    {
        if (SelectedHak == null) return;

        var index = HakList.IndexOf(SelectedHak);
        if (index > 0)
        {
            HakList.Move(index, index - 1);
            HasUnsavedChanges = true;
        }
    }

    [RelayCommand]
    private void MoveHakDown()
    {
        if (SelectedHak == null) return;

        var index = HakList.IndexOf(SelectedHak);
        if (index < HakList.Count - 1)
        {
            HakList.Move(index, index + 1);
            HasUnsavedChanges = true;
        }
    }

    // Variable operations (invoked by the shared VariablesPanel's Add/Delete events, #2293)

    /// <summary>Append a new int variable and select it. Called from the panel's AddRequested event.</summary>
    public void AddVariable()
    {
        var newVar = new VariableViewModel
        {
            Name = VariableViewModel.NextDefaultName(Variables.Select(v => v.Name)),
            Type = VariableType.Int
        };
        Variables.Add(newVar);
        SelectedVariable = newVar;
        HasUnsavedChanges = true;

        // Notify View to auto-focus the name field
        VariableAdded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Remove the given variables. Called from the panel's DeleteRequested event.</summary>
    public void RemoveVariables(System.Collections.Generic.IEnumerable<VariableViewModel> toRemove)
    {
        var removed = false;
        foreach (var v in toRemove)
            removed |= Variables.Remove(v);

        if (removed)
            HasUnsavedChanges = true;
    }
}
