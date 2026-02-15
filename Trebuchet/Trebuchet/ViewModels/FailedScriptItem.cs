using CommunityToolkit.Mvvm.ComponentModel;

namespace RadoubLauncher.ViewModels;

/// <summary>
/// Represents a script that failed compilation, shown in the Build & Test tab.
/// </summary>
public partial class FailedScriptItem : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected = true;

    public string ScriptName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string ErrorSummary { get; set; } = "";
}
