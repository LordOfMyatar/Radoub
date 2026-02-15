using System;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Gff;

namespace RadoubLauncher.ViewModels;

// HAK list and variable management commands
public partial class ModuleEditorViewModel
{
    // HAK List Commands

    [RelayCommand]
    private void AddHak()
    {
        if (string.IsNullOrWhiteSpace(NewHakName)) return;

        var hakName = NewHakName.Trim();
        // Remove .hak extension if present
        if (hakName.EndsWith(".hak", StringComparison.OrdinalIgnoreCase))
            hakName = hakName[..^4];

        if (!HakList.Contains(hakName, StringComparer.OrdinalIgnoreCase))
        {
            HakList.Add(hakName);
            HasUnsavedChanges = true;
        }

        NewHakName = string.Empty;
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

    // Variable Commands

    [RelayCommand]
    private void AddVariable()
    {
        var newVar = new VariableViewModel(Variable.CreateInt("", 0));
        newVar.SetUniquenessCheck(IsVariableNameUnique);
        Variables.Add(newVar);
        SelectedVariable = newVar;
        HasUnsavedChanges = true;

        // Notify View to auto-focus the name field
        VariableAdded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Check if a variable name is unique within the collection.
    /// </summary>
    private bool IsVariableNameUnique(string name, VariableViewModel currentVar)
    {
        if (string.IsNullOrEmpty(name)) return true; // Empty names handled separately

        return !Variables.Any(v => v != currentVar &&
            string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    [RelayCommand]
    private void RemoveVariable()
    {
        if (SelectedVariable != null)
        {
            Variables.Remove(SelectedVariable);
            HasUnsavedChanges = true;
        }
    }
}
