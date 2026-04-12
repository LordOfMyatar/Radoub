using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Radoub.Formats.Common;
using Radoub.Formats.Search;
using Radoub.UI.Services.Search;

namespace RadoubLauncher.ViewModels;

/// <summary>
/// ViewModel for the Marlinspike search &amp; replace panel.
/// Manages search criteria, state, and results for data binding.
/// </summary>
public partial class MarlinspikePanelViewModel : ObservableObject
{
    private ModuleSearchResults? _lastResults;

    // --- Search pattern and options ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSearch))]
    private string _searchPattern = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReplace))]
    private string _replaceText = "";

    [ObservableProperty]
    private bool _isCaseSensitive;

    [ObservableProperty]
    private bool _isRegex;

    [ObservableProperty]
    private bool _isWholeWord;

    [ObservableProperty]
    private bool _searchStrRefs;

    [ObservableProperty]
    private string _selectedCategory = "All Fields";

    // --- Search state ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSearch))]
    [NotifyPropertyChangedFor(nameof(CanReplace))]
    private bool _isSearching;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private bool _showProgress;

    // --- Results ---

    public ObservableCollection<FileTypeGroupViewModel> ResultGroups { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReplace))]
    private bool _hasResults;

    // --- File type checkboxes ---

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSearch), nameof(HasAnyFileTypeSelected))] private bool _includeDlg = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSearch), nameof(HasAnyFileTypeSelected))] private bool _includeUtc = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSearch), nameof(HasAnyFileTypeSelected))] private bool _includeBic = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSearch), nameof(HasAnyFileTypeSelected))] private bool _includeUti = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSearch), nameof(HasAnyFileTypeSelected))] private bool _includeUtm = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSearch), nameof(HasAnyFileTypeSelected))] private bool _includeJrl = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSearch), nameof(HasAnyFileTypeSelected))] private bool _includeUtp = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSearch), nameof(HasAnyFileTypeSelected))] private bool _includeUtd = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSearch), nameof(HasAnyFileTypeSelected))] private bool _includeUte = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSearch), nameof(HasAnyFileTypeSelected))] private bool _includeUtt = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSearch), nameof(HasAnyFileTypeSelected))] private bool _includeUtw = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSearch), nameof(HasAnyFileTypeSelected))] private bool _includeUts = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSearch), nameof(HasAnyFileTypeSelected))] private bool _includeGit = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSearch), nameof(HasAnyFileTypeSelected))] private bool _includeAre = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSearch), nameof(HasAnyFileTypeSelected))] private bool _includeIfo = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSearch), nameof(HasAnyFileTypeSelected))] private bool _includeFac = true;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanSearch), nameof(HasAnyFileTypeSelected))] private bool _includeItp = true;

    // --- Category list ---

    public List<string> Categories { get; } = new()
    {
        "All Fields",
        "Content",
        "Identity",
        "Script",
        "Metadata",
        "Variable"
    };

    // --- Computed properties ---

    public bool HasAnyFileTypeSelected =>
        IncludeDlg || IncludeUtc || IncludeBic || IncludeUti || IncludeUtm ||
        IncludeJrl || IncludeUtp || IncludeUtd || IncludeUte || IncludeUtt ||
        IncludeUtw || IncludeUts || IncludeGit || IncludeAre || IncludeIfo ||
        IncludeFac || IncludeItp;

    public bool CanSearch => !string.IsNullOrEmpty(SearchPattern) && !IsSearching && HasAnyFileTypeSelected;

    public bool CanReplace => HasResults && !string.IsNullOrEmpty(ReplaceText) && !IsSearching;

    // --- Methods ---

    /// <summary>
    /// Update searching state. Used by codebehind to toggle state during search.
    /// </summary>
    public void SetSearching(bool isSearching)
    {
        IsSearching = isSearching;
        ShowProgress = isSearching;
    }

    /// <summary>
    /// Build a SearchCriteria from the current ViewModel state.
    /// </summary>
    /// <summary>
    /// Optional TLK resolver, set by the codebehind when TlkService is available.
    /// Used when SearchStrRefs is enabled.
    /// </summary>
    public Func<uint, string?>? TlkResolver { get; set; }

    public SearchCriteria BuildSearchCriteria()
    {
        return new SearchCriteria
        {
            Pattern = SearchPattern,
            IsRegex = IsRegex,
            CaseSensitive = IsCaseSensitive,
            WholeWord = IsWholeWord,
            SearchStrRefs = SearchStrRefs,
            TlkResolver = TlkResolver,
            CategoryFilter = BuildCategoryFilter(),
            FileTypeFilter = BuildFileTypeFilter()
        };
    }

    /// <summary>
    /// Set results from a completed search, building the tree group structure.
    /// </summary>
    public void SetResults(ModuleSearchResults results)
    {
        _lastResults = results;
        ResultGroups.Clear();

        var grouped = results.GroupByExtension();
        foreach (var (extension, files) in grouped.OrderBy(g => g.Key))
        {
            var totalMatches = files.Sum(f => f.MatchCount);
            var group = new FileTypeGroupViewModel
            {
                Extension = extension,
                DisplayName = $"{GetFileTypeLabel(extension)} (.{extension}) — {files.Count} file{(files.Count != 1 ? "s" : "")}, {totalMatches} match{(totalMatches != 1 ? "es" : "")}",
                Files = files.Select(f => new FileResultViewModel
                {
                    FileName = f.FileName,
                    FilePath = f.FilePath,
                    ResourceType = f.ResourceType,
                    ToolId = f.ToolId,
                    MatchCount = f.MatchCount,
                    HadParseError = f.HadParseError,
                    ParseError = f.ParseError,
                    DisplayName = f.HadParseError
                        ? $"{f.FileName} — ⚠ {f.ParseError}"
                        : $"{f.FileName} — {f.MatchCount} match{(f.MatchCount != 1 ? "es" : "")}",
                    Matches = f.Matches.Select(m => new MatchViewModel
                    {
                        Match = m,
                        FilePath = f.FilePath,
                        ResourceType = f.ResourceType,
                        ToolId = f.ToolId,
                        DisplayText = FormatMatchDisplay(m)
                    }).ToList()
                }).ToList()
            };
            ResultGroups.Add(group);
        }

        HasResults = ResultGroups.Count > 0;

        var duration = results.Duration.TotalSeconds;
        StatusText = $"{results.TotalMatches} match{(results.TotalMatches != 1 ? "es" : "")} in {results.FilesWithMatches} file{(results.FilesWithMatches != 1 ? "s" : "")} ({duration:F1}s)";

        if (results.WasCancelled)
            StatusText += " — cancelled";
        if (results.ParseErrors > 0)
            StatusText += $" — {results.ParseErrors} parse error{(results.ParseErrors != 1 ? "s" : "")}";
    }

    /// <summary>
    /// Clear all results and reset status.
    /// </summary>
    public void ClearResults()
    {
        _lastResults = null;
        ResultGroups.Clear();
        HasResults = false;
        StatusText = "Ready";
    }

    /// <summary>
    /// Get the last search results for replace preview.
    /// </summary>
    public ModuleSearchResults? GetLastResults() => _lastResults;

    /// <summary>
    /// Set all file type checkboxes to checked.
    /// </summary>
    public void SelectAllFileTypes()
    {
        IncludeDlg = true;
        IncludeUtc = true;
        IncludeBic = true;
        IncludeUti = true;
        IncludeUtm = true;
        IncludeJrl = true;
        IncludeUtp = true;
        IncludeUtd = true;
        IncludeUte = true;
        IncludeUtt = true;
        IncludeUtw = true;
        IncludeUts = true;
        IncludeGit = true;
        IncludeAre = true;
        IncludeIfo = true;
        IncludeFac = true;
        IncludeItp = true;
    }

    /// <summary>
    /// Set all file type checkboxes to unchecked.
    /// </summary>
    public void DeselectAllFileTypes()
    {
        IncludeDlg = false;
        IncludeUtc = false;
        IncludeBic = false;
        IncludeUti = false;
        IncludeUtm = false;
        IncludeJrl = false;
        IncludeUtp = false;
        IncludeUtd = false;
        IncludeUte = false;
        IncludeUtt = false;
        IncludeUtw = false;
        IncludeUts = false;
        IncludeGit = false;
        IncludeAre = false;
        IncludeIfo = false;
        IncludeFac = false;
        IncludeItp = false;
    }

    // --- Private helpers ---

    private IReadOnlyList<SearchFieldCategory>? BuildCategoryFilter()
    {
        if (SelectedCategory == "All Fields")
            return null;

        if (Enum.TryParse<SearchFieldCategory>(SelectedCategory, out var category))
            return new[] { category };

        return null;
    }

    private IReadOnlyList<ushort>? BuildFileTypeFilter()
    {
        // If all types are checked, return null (no filter = search all)
        if (IncludeDlg && IncludeUtc && IncludeBic && IncludeUti && IncludeUtm &&
            IncludeJrl && IncludeUtp && IncludeUtd && IncludeUte && IncludeUtt &&
            IncludeUtw && IncludeUts && IncludeGit && IncludeAre && IncludeIfo &&
            IncludeFac && IncludeItp)
            return null;

        var types = new List<ushort>();
        if (IncludeDlg) types.Add(ResourceTypes.Dlg);
        if (IncludeUtc) types.Add(ResourceTypes.Utc);
        if (IncludeBic) types.Add(ResourceTypes.Bic);
        if (IncludeUti) types.Add(ResourceTypes.Uti);
        if (IncludeUtm) types.Add(ResourceTypes.Utm);
        if (IncludeJrl) types.Add(ResourceTypes.Jrl);
        if (IncludeUtp) types.Add(ResourceTypes.Utp);
        if (IncludeUtd) types.Add(ResourceTypes.Utd);
        if (IncludeUte) types.Add(ResourceTypes.Ute);
        if (IncludeUtt) types.Add(ResourceTypes.Utt);
        if (IncludeUtw) types.Add(ResourceTypes.Utw);
        if (IncludeUts) types.Add(ResourceTypes.Uts);
        if (IncludeGit) types.Add(ResourceTypes.Git);
        if (IncludeAre) types.Add(ResourceTypes.Are);
        if (IncludeIfo) types.Add(ResourceTypes.Ifo);
        if (IncludeFac) types.Add(ResourceTypes.Fac);
        if (IncludeItp) types.Add(ResourceTypes.Itp);
        return types;
    }

    private static string GetFileTypeLabel(string extension) => extension.ToUpperInvariant() switch
    {
        "DLG" => "Dialog Files",
        "UTC" => "Creature Files",
        "BIC" => "Character Files",
        "UTI" => "Item Files",
        "UTM" => "Store Files",
        "JRL" => "Journal Files",
        "UTP" => "Placeable Files",
        "UTD" => "Door Files",
        "UTE" => "Encounter Files",
        "UTT" => "Trigger Files",
        "UTW" => "Waypoint Files",
        "UTS" => "Sound Files",
        "GIT" => "Area Instance Files",
        "ARE" => "Area Files",
        "IFO" => "Module Info",
        "FAC" => "Faction Files",
        "ITP" => "Palette Files",
        _ => $"{extension.ToUpperInvariant()} Files"
    };

    private static string FormatMatchDisplay(SearchMatch match)
    {
        var location = match.Location is DlgMatchLocation dlgLoc
            ? dlgLoc.DisplayPath
            : match.Location?.ToString() ?? match.Field.Name;
        var context = match.FullFieldValue;

        // Truncate long context
        if (context.Length > 80)
        {
            var start = Math.Max(0, match.MatchOffset - 20);
            var length = Math.Min(80, context.Length - start);
            context = "…" + context.Substring(start, length) + "…";
        }

        return $"{location}: \"{context}\"";
    }
}

// --- Tree view model classes ---

/// <summary>
/// Represents a file type group in the results tree (e.g., "Dialog Files (.dlg) — 3 files, 7 matches").
/// </summary>
public class FileTypeGroupViewModel
{
    public required string Extension { get; init; }
    public required string DisplayName { get; init; }
    public required List<FileResultViewModel> Files { get; init; }
}

/// <summary>
/// Represents a single file in the results tree (e.g., "merchant_01.dlg — 4 matches").
/// </summary>
public class FileResultViewModel
{
    public required string FileName { get; init; }
    public required string FilePath { get; init; }
    public ushort ResourceType { get; init; }
    public string ToolId { get; init; } = "";
    public int MatchCount { get; init; }
    public bool HadParseError { get; init; }
    public string? ParseError { get; init; }
    public required string DisplayName { get; init; }
    public required List<MatchViewModel> Matches { get; init; }
}

/// <summary>
/// Represents a single match in the results tree.
/// </summary>
public class MatchViewModel
{
    public required SearchMatch Match { get; init; }
    public required string FilePath { get; init; }
    public ushort ResourceType { get; init; }
    public string ToolId { get; init; } = "";
    public required string DisplayText { get; init; }
}
