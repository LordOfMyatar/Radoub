using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Search;
using Radoub.Formats.Search.Rename;
using Radoub.Formats.Settings;
using Radoub.UI.Services;
using Radoub.UI.Services.Search;
using RadoubLauncher.Services;
using RadoubLauncher.ViewModels;
using RadoubLauncher.Views;
using ModulePath = RadoubLauncher.Services.ModulePathHelper;

namespace RadoubLauncher.Controls;

public partial class MarlinspikePanel : UserControl
{
    private MarlinspikePanelViewModel? _viewModel;
    private MainWindowViewModel? _mainViewModel;
    private Window? _parentWindow;
    private ModuleSearchService? _searchService;
    private BatchReplaceService? _batchReplaceService;
    private ItemResolutionService? _itemResolutionService;
    private TlkService? _tlkService;
    private CancellationTokenSource? _searchCts;

    /// <summary>Maps resource types to Trebuchet tool names for launch dispatch.</summary>
    private static readonly Dictionary<ushort, string> ResourceTypeToToolName = new()
    {
        [ResourceTypes.Dlg] = "Parley",
        [ResourceTypes.Utc] = "Quartermaster",
        [ResourceTypes.Bic] = "Quartermaster",
        [ResourceTypes.Uti] = "Relique",
        [ResourceTypes.Utm] = "Fence",
        [ResourceTypes.Jrl] = "Manifest",
    };

    public MarlinspikePanel()
    {
        InitializeComponent();
        Unloaded += OnPanelUnloaded;
    }

    private void OnPanelUnloaded(object? sender, RoutedEventArgs e)
    {
        Unloaded -= OnPanelUnloaded;
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
    }

    public void Initialize(MarlinspikePanelViewModel viewModel, MainWindowViewModel mainViewModel, Window parentWindow)
    {
        _viewModel = viewModel;
        _mainViewModel = mainViewModel;
        _parentWindow = parentWindow;
        DataContext = viewModel;
    }

    /// <summary>
    /// Focus the search pattern box (for Ctrl+Shift+F shortcut).
    /// </summary>
    public void FocusSearchBox()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            SearchPatternBox?.Focus();
            SearchPatternBox?.SelectAll();
        }, Avalonia.Threading.DispatcherPriority.Input);
    }

    private void EnsureServices()
    {
        if (_searchService == null)
        {
            var gameDataService = _mainViewModel?.ModuleEditorViewModel?.GameDataService;
            _itemResolutionService = new ItemResolutionService(gameDataService);
            var modulePath = ModulePath.GetWorkingDirectory(RadoubSettings.Instance.CurrentModulePath);
            if (!string.IsNullOrEmpty(modulePath))
                _itemResolutionService.SetModuleDirectory(modulePath);

            _searchService = new ModuleSearchService(
                resRef => _itemResolutionService?.ResolveItem(resRef)?.DisplayName);
        }
        _batchReplaceService ??= new BatchReplaceService(new BackupService());
        EnsureTlkResolver();
    }

    private void EnsureTlkResolver()
    {
        if (_viewModel == null) return;
        if (_viewModel.TlkResolver != null) return;

        _tlkService ??= new TlkService();
        if (_tlkService.IsAvailable)
            _viewModel.TlkResolver = _tlkService.ResolveStrRef;
    }

    private async void OnSearchClick(object? sender, RoutedEventArgs e)
    {
        await ExecuteSearchAsync();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        _searchCts?.Cancel();
    }

    private async void OnSearchPatternKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _viewModel?.CanSearch == true)
        {
            await ExecuteSearchAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _viewModel?.IsSearching == true)
        {
            _searchCts?.Cancel();
            e.Handled = true;
        }
    }

    private void OnSelectAllClick(object? sender, RoutedEventArgs e)
    {
        _viewModel?.SelectAllFileTypes();
    }

    private void OnDeselectAllClick(object? sender, RoutedEventArgs e)
    {
        _viewModel?.DeselectAllFileTypes();
    }

    private async Task ExecuteSearchAsync()
    {
        if (_viewModel == null) return;

        var modulePath = ModulePath.GetWorkingDirectory(RadoubSettings.Instance.CurrentModulePath);
        if (string.IsNullOrEmpty(modulePath))
        {
            _viewModel.StatusText = "No module loaded or unpacked. Open and unpack a module in Trebuchet first.";
            return;
        }

        EnsureServices();

        var criteria = _viewModel.BuildSearchCriteria();
        var validationError = criteria.Validate();
        if (validationError != null)
        {
            _viewModel.StatusText = $"Invalid pattern: {validationError}";
            return;
        }

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _viewModel.SetSearching(true);
        _viewModel.ClearResults();
        ResultsTree.ItemsSource = null;
        DurationText.Text = "";
        ModulePathText.Text = $"Module: {Path.GetFileName(modulePath)}";

        var progress = new Progress<ScanProgress>(p =>
        {
            _viewModel.StatusText = p.Phase == "Searching"
                ? $"Searching {p.CurrentFile}... ({p.FilesScanned}/{p.TotalFiles}, {p.MatchesFound} matches)"
                : p.Phase;
        });

        try
        {
            var results = await _searchService!.ScanModuleAsync(modulePath, criteria, progress, token);
            _viewModel.SetResults(results);
            DisplayResultsTree(results);

            var duration = results.Duration;
            DurationText.Text = duration.TotalSeconds < 1
                ? $"{duration.TotalMilliseconds:F0}ms"
                : $"{duration.TotalSeconds:F1}s";
        }
        catch (OperationCanceledException)
        {
            _viewModel.StatusText = "Search cancelled.";
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Marlinspike search failed: {ex.Message}");
            _viewModel.StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            _viewModel.SetSearching(false);
        }
    }

    private void DisplayResultsTree(ModuleSearchResults results)
    {
        if (results.TotalMatches == 0)
        {
            ResultsTree.ItemsSource = null;
            return;
        }

        var treeItems = new List<TreeViewItem>();
        var grouped = results.GroupByExtension();

        foreach (var (extension, files) in grouped.OrderBy(g => g.Key))
        {
            var totalMatches = files.Sum(f => f.MatchCount);
            var groupNode = new TreeViewItem
            {
                Header = $"{GetFileTypeLabel(extension)} (.{extension}) \u2014 {files.Count} file{(files.Count != 1 ? "s" : "")}, {totalMatches} match{(totalMatches != 1 ? "es" : "")}",
                IsExpanded = true
            };

            foreach (var fileResult in files.OrderBy(f => f.FileName))
            {
                var fileNode = new TreeViewItem
                {
                    Header = fileResult.HadParseError
                        ? $"{fileResult.FileName} \u2014 \u26a0 {fileResult.ParseError}"
                        : $"{fileResult.FileName} \u2014 {fileResult.MatchCount} match{(fileResult.MatchCount != 1 ? "es" : "")}",
                    Tag = fileResult,
                    IsExpanded = true
                };

                foreach (var match in fileResult.Matches)
                {
                    var locationText = match.Location is DlgMatchLocation dlgLoc
                        ? dlgLoc.DisplayPath
                        : match.Location?.ToString() ?? match.Field.Name;
                    var preview = match.FullFieldValue.Length > 80
                        ? match.FullFieldValue[..80] + "..."
                        : match.FullFieldValue;

                    var matchNode = new TreeViewItem
                    {
                        Header = $"[{locationText}] {match.Field.Name}: {preview}",
                        Tag = new MatchInfo(fileResult.FilePath, fileResult.ResourceType, fileResult.ToolId, match)
                    };
                    fileNode.Items.Add(matchNode);
                }

                groupNode.Items.Add(fileNode);
            }

            treeItems.Add(groupNode);
        }

        // Parse error nodes (files that failed to parse but had no matches)
        foreach (var fileResult in results.Files.Where(f => f.HadParseError && f.MatchCount == 0))
        {
            var errorNode = new TreeViewItem
            {
                Header = $"\u26a0 {fileResult.FileName} \u2014 parse error: {fileResult.ParseError}",
                Tag = fileResult
            };
            treeItems.Add(errorNode);
        }

        ResultsTree.ItemsSource = treeItems;
    }

    private void OnResultDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ResultsTree.SelectedItem is not TreeViewItem item)
            return;

        string? filePath = null;
        ushort resourceType = 0;

        if (item.Tag is MatchInfo matchInfo)
        {
            filePath = matchInfo.FilePath;
            resourceType = matchInfo.ResourceType;
        }
        else if (item.Tag is FileSearchResult fileResult)
        {
            filePath = fileResult.FilePath;
            resourceType = fileResult.ResourceType;
        }

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;

        if (ResourceTypeToToolName.TryGetValue(resourceType, out var toolName))
        {
            var launched = ToolLauncherService.Instance.LaunchTool(toolName, $"--file \"{filePath}\"");
            if (!launched && _viewModel != null)
                _viewModel.StatusText = $"Could not launch {toolName} for: {Path.GetFileName(filePath)}";
        }
        else if (_viewModel != null)
        {
            _viewModel.StatusText = $"No editor for .{Path.GetExtension(filePath).TrimStart('.')} files";
        }
    }

    private async void OnReplaceAllClick(object? sender, RoutedEventArgs e)
    {
        await OpenReplacePreviewAsync(selectAll: true);
    }

    private async void OnReplaceSelectedClick(object? sender, RoutedEventArgs e)
    {
        await OpenReplacePreviewAsync(selectAll: true);
    }

    private async Task OpenReplacePreviewAsync(bool selectAll)
    {
        if (_viewModel == null || _parentWindow == null) return;

        var lastResults = _viewModel.GetLastResults();
        if (lastResults == null || lastResults.TotalMatches == 0) return;

        EnsureServices();

        var filesWithMatches = lastResults.Files.Where(f => f.MatchCount > 0).ToList();
        if (filesWithMatches.Count == 0) return;

        var replaceText = _viewModel.ReplaceText;
        var preview = _batchReplaceService!.PreviewReplace(
            filesWithMatches,
            replaceText,
            allowResRefReplace: _viewModel.SearchFilenameResRef);

        // Dispatch to ResRef rename orchestrator when filename matches present.
        if (RenameDispatchHelpers.HasFilenameMatches(preview))
        {
            await DispatchResRefRenameAsync(preview);
            return;
        }

        var previewWindow = new ReplacePreviewWindow();
        previewWindow.Initialize(preview, _viewModel.SearchPattern, replaceText, _batchReplaceService);
        previewWindow.ReplacementComplete += OnReplacementComplete;
        previewWindow.Show(_parentWindow);
    }

    /// <summary>
    /// Run the ResRef rename pipeline: build plans, populate references, surface
    /// auto-suffix collision dialogs, and execute via ResRefRenameOrchestrator.
    /// </summary>
    private async Task DispatchResRefRenameAsync(BatchReplacePreview preview)
    {
        if (_viewModel == null || _parentWindow == null) return;

        var moduleDir = ModulePath.GetWorkingDirectory(RadoubSettings.Instance.CurrentModulePath);
        if (string.IsNullOrEmpty(moduleDir))
        {
            _viewModel.StatusText = "No module loaded — cannot rename.";
            return;
        }

        var validator = new ResRefValidator();
        var plans = RenameDispatchHelpers.BuildRenamePlansFromPreview(preview, moduleDir, validator);

        if (plans.Count == 0)
        {
            _viewModel.StatusText = "Rename skipped — no valid filename targets (validator rejected all proposed names).";
            return;
        }

        // Surface auto-suffix collision dialogs before proceeding.
        // Each plan whose validation triggered an auto-suffix must be confirmed.
        var confirmedPlans = new List<ResRefRenamePlan>();
        foreach (var plan in plans)
        {
            if (!plan.Validation.AutoSuffixApplied)
            {
                confirmedPlans.Add(plan);
                continue;
            }

            var originalProposed = ComputeOriginalProposedName(preview, plan);
            var dialog = new AutoSuffixCollisionDialog(originalProposed, plan.NewName, plan.SourceFilePath);
            await dialog.ShowDialog(_parentWindow);

            switch (dialog.Result)
            {
                case AutoSuffixDialogResult.Continue:
                    confirmedPlans.Add(plan);
                    break;
                case AutoSuffixDialogResult.PickAnother:
                    _viewModel.StatusText = "Rename cancelled — adjust the replacement text and click Replace again to choose a different name.";
                    return;
                case AutoSuffixDialogResult.Cancel:
                default:
                    _viewModel.StatusText = "Rename cancelled.";
                    return;
            }
        }

        if (confirmedPlans.Count == 0)
        {
            _viewModel.StatusText = "Rename cancelled — no plans confirmed.";
            return;
        }

        _viewModel.StatusText = "Scanning module for references...";

        try
        {
            var criteria = _viewModel.BuildSearchCriteria();
            await RenameDispatchHelpers.PopulateReferencesAsync(
                confirmedPlans, moduleDir, _viewModel.IncludeNss, criteria);

            var snapshotPaths = confirmedPlans
                .SelectMany(p => p.References.Select(r => r.FilePath)
                    .Concat(new[] { p.SourceFilePath }))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            var snapshots = ResRefRenameOrchestrator.CaptureSnapshots(snapshotPaths);

            var moduleName = !string.IsNullOrEmpty(RadoubSettings.Instance.CurrentModulePath)
                ? Path.GetFileName(RadoubSettings.Instance.CurrentModulePath)
                : "unknown";

            var orchestrator = new ResRefRenameOrchestrator(new BackupService());
            var result = await orchestrator.ExecuteAsync(confirmedPlans, moduleName, snapshots);

            if (result.Success)
            {
                _viewModel.StatusText =
                    $"Renamed {result.RenamedFiles} file{(result.RenamedFiles != 1 ? "s" : "")}, " +
                    $"updated {result.ReferencesUpdated} reference{(result.ReferencesUpdated != 1 ? "s" : "")}. Backup created.";
                _viewModel.ClearResults();
                ResultsTree.ItemsSource = null;
            }
            else
            {
                var rollbackNote = result.RollbackAttempted
                    ? (result.RollbackSucceeded ? " — rolled back" : " — ROLLBACK FAILED, see backup manifest")
                    : string.Empty;
                _viewModel.StatusText = $"Rename failed: {result.Error}{rollbackNote}";
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"ResRef rename failed: {ex.Message}");
            _viewModel.StatusText = $"Rename error: {ex.Message}";
        }
    }

    /// <summary>
    /// Recover the user's original proposed name (before validator auto-suffixing)
    /// by re-running ApplyReplacement against the matching PendingChange. Used
    /// to populate the auto-suffix collision dialog text.
    /// </summary>
    private static string ComputeOriginalProposedName(BatchReplacePreview preview, ResRefRenamePlan plan)
    {
        var change = preview.Changes.FirstOrDefault(c =>
            string.Equals(c.FilePath, plan.SourceFilePath, StringComparison.OrdinalIgnoreCase) &&
            c.Match.Field.GffPath == FilenameSearchProvider.FilenameField.GffPath);

        if (change == null) return plan.NewName;

        var oldName = Path.GetFileNameWithoutExtension(plan.SourceFilePath);
        var raw = RenameDispatchHelpers.ApplyReplacement(oldName, change.Match, change.ReplacementText);
        return raw.ToLowerInvariant();
    }

    private void OnReplacementComplete(object? sender, BatchReplaceResult result)
    {
        if (_viewModel == null) return;

        if (result.Success)
        {
            _viewModel.StatusText = $"Replaced {result.ReplacementsMade} matches in {result.FilesModified} files. Backup created.";
            _viewModel.ClearResults();
            ResultsTree.ItemsSource = null;
        }
        else
        {
            _viewModel.StatusText = $"Replace failed: {result.Error}";
        }
    }

    /// <summary>
    /// Clear results when a different module is opened.
    /// </summary>
    public void OnModuleChanged()
    {
        _itemResolutionService = null;
        _searchService = null;
        _viewModel?.ClearResults();
        ResultsTree.ItemsSource = null;
        DurationText.Text = "";
        ModulePathText.Text = "";
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

    private record MatchInfo(string FilePath, ushort ResourceType, string ToolId, Radoub.Formats.Search.SearchMatch Match);
}
