using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using RadoubLauncher.Services;

namespace RadoubLauncher.ViewModels;

/// <summary>
/// View model for the HAK conflict results window (#1162). Presents the
/// conflicts found across a module's HAK list. Detection only — see
/// <see cref="HakConflictCheckerService"/>.
/// </summary>
public partial class HakConflictViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _unresolvedWarning = string.Empty;

    [ObservableProperty]
    private bool _hasUnresolved;

    public ObservableCollection<HakConflictRowViewModel> Conflicts { get; } = new();

    /// <summary>Populate from a completed conflict report.</summary>
    public void Load(HakConflictReport report)
    {
        Conflicts.Clear();

        // Stable display order: by resource name, then extension.
        var ordered = report.Conflicts
            .OrderBy(c => c.ResRef, System.StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Extension, System.StringComparer.OrdinalIgnoreCase);

        foreach (var conflict in ordered)
            Conflicts.Add(new HakConflictRowViewModel(conflict));

        StatusText = Conflicts.Count == 0
            ? "No HAK conflicts found."
            : $"{Conflicts.Count} conflicting resource(s) found.";

        HasUnresolved = report.UnresolvedHaks.Count > 0;
        UnresolvedWarning = HasUnresolved
            ? $"⚠ {report.UnresolvedHaks.Count} HAK(s) not found and skipped: {string.Join(", ", report.UnresolvedHaks)}"
            : string.Empty;
    }
}

/// <summary>One conflict row: a resource present in multiple HAKs.</summary>
public sealed class HakConflictRowViewModel
{
    public HakConflictRowViewModel(HakConflict conflict)
    {
        Resource = $"{conflict.ResRef}{conflict.Extension}";
        Winner = conflict.WinnerHak;
        Haks = string.Join(" → ", conflict.ContainingHaks);
        HakCount = conflict.ContainingHaks.Count;
    }

    /// <summary>ResRef plus extension, e.g. "myscript.nss".</summary>
    public string Resource { get; }

    /// <summary>The winning HAK (first in priority order).</summary>
    public string Winner { get; }

    /// <summary>All HAKs containing the resource, in priority order (winner first).</summary>
    public string Haks { get; }

    /// <summary>Number of HAKs containing the resource.</summary>
    public int HakCount { get; }
}
