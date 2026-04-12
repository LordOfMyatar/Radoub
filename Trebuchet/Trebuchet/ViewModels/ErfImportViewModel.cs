using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;
using RadoubLauncher.Services;

namespace RadoubLauncher.ViewModels;

public partial class ErfImportViewModel : ObservableObject
{
    private readonly ErfImportService _importService = new();
    private readonly string _moduleDirectory;
    private CancellationTokenSource? _importCts;
    private List<ErfResourceViewModel> _allResources = new();

    [ObservableProperty]
    private string _erfFilePath = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedTypeFilter = "All Types";

    [ObservableProperty]
    private bool _overwriteExisting;

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isErfLoaded;

    public ObservableCollection<ErfResourceViewModel> FilteredResources { get; } = new();
    public ObservableCollection<string> TypeFilters { get; } = new() { "All Types" };

    public int SelectedCount => _allResources.Count(r => r.IsSelected);
    public int TotalCount => _allResources.Count;

    public bool CanImport => IsErfLoaded && !IsImporting && SelectedCount > 0;

    public ErfImportViewModel(string moduleDirectory)
    {
        _moduleDirectory = moduleDirectory;
    }

    public async Task LoadErfAsync(string path)
    {
        try
        {
            ErfFilePath = path;
            StatusText = "Loading ERF metadata...";

            var erf = await Task.Run(() => ErfReader.ReadMetadataOnly(path));
            var conflicts = _importService.DetectConflicts(erf.Resources, _moduleDirectory);

            _allResources = erf.Resources.Select(entry =>
            {
                var vm = new ErfResourceViewModel(entry);
                vm.ExistsInModule = conflicts.Contains(entry.ResRef);
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(ErfResourceViewModel.IsSelected))
                    {
                        OnPropertyChanged(nameof(SelectedCount));
                        OnPropertyChanged(nameof(CanImport));
                    }
                };
                return vm;
            }).ToList();

            TypeFilters.Clear();
            TypeFilters.Add("All Types");
            foreach (var label in _allResources.Select(r => r.TypeLabel).Distinct().OrderBy(l => l))
                TypeFilters.Add(label);

            SelectedTypeFilter = "All Types";
            IsErfLoaded = true;
            ApplyFilter();
            StatusText = $"Loaded {_allResources.Count} resources ({conflicts.Count} already exist in module)";
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load ERF: {ex.Message}");
            StatusText = $"Error loading ERF: {ex.Message}";
            IsErfLoaded = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedTypeFilterChanged(string value) => ApplyFilter();

    partial void OnIsImportingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanImport));
    }

    partial void OnIsErfLoadedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanImport));
    }

    public void ApplyFilter()
    {
        FilteredResources.Clear();

        foreach (var resource in _allResources)
        {
            if (!MatchesFilter(resource))
                continue;
            FilteredResources.Add(resource);
        }

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(CanImport));
    }

    private bool MatchesFilter(ErfResourceViewModel resource)
    {
        if (!string.IsNullOrEmpty(SearchText) &&
            !resource.ResRef.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            return false;

        if (SelectedTypeFilter != "All Types" && resource.TypeLabel != SelectedTypeFilter)
            return false;

        return true;
    }

    [RelayCommand]
    public void SelectAll()
    {
        foreach (var resource in FilteredResources)
            resource.IsSelected = true;
    }

    [RelayCommand]
    public void DeselectAll()
    {
        foreach (var resource in FilteredResources)
            resource.IsSelected = false;
    }

    [RelayCommand]
    public void CancelImport()
    {
        _importCts?.Cancel();
    }

    public async Task<ErfImportResult?> ImportAsync()
    {
        if (!CanImport) return null;

        var selected = _allResources
            .Where(r => r.IsSelected)
            .Select(r => r.Entry)
            .ToList();

        _importCts?.Cancel();
        _importCts = new CancellationTokenSource();
        var token = _importCts.Token;

        IsImporting = true;

        var progress = new Progress<ImportProgress>(p =>
        {
            StatusText = $"Importing {p.Current} of {p.Total}: {p.CurrentResRef}...";
        });

        try
        {
            var result = await _importService.ImportResourcesAsync(
                ErfFilePath, selected, _moduleDirectory, OverwriteExisting, progress, token);

            var parts = new List<string> { $"Imported {result.ImportedCount} resources" };
            if (result.SkippedCount > 0)
                parts.Add($"{result.SkippedCount} skipped (already exist)");
            if (result.ErrorCount > 0)
                parts.Add($"{result.ErrorCount} errors");
            StatusText = string.Join(", ", parts);

            return result;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Import cancelled.";
            return null;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"ERF import failed: {ex.Message}");
            StatusText = $"Import failed: {ex.Message}";
            return null;
        }
        finally
        {
            IsImporting = false;
        }
    }
}
