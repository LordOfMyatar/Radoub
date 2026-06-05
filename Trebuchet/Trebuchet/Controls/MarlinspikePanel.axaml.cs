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
    private DateTime? _indexedModuleDirMtime;
    private string? _indexedModuleDirPath;

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
        var modulePath = ModulePath.GetWorkingDirectory(RadoubSettings.Instance.CurrentModulePath);

        // #2072 — invalidate cached services if the module working directory's
        // mtime moved (ERF import wrote new files, external editor saved, etc.)
        // or if the working directory itself changed.
        if (_searchService != null)
        {
            var currentMtime = GetDirectoryMtime(modulePath);
            var pathChanged = !string.Equals(_indexedModuleDirPath, modulePath, StringComparison.OrdinalIgnoreCase);
            if (pathChanged || SearchIndexStaleness.IsStale(currentMtime, _indexedModuleDirMtime))
            {
                _itemResolutionService = null;
                _searchService = null;
            }
        }

        if (_searchService == null)
        {
            var gameDataService = _mainViewModel?.ModuleEditorViewModel?.GameDataService;
            _itemResolutionService = new ItemResolutionService(gameDataService);
            if (!string.IsNullOrEmpty(modulePath))
                _itemResolutionService.SetModuleDirectory(modulePath);

            _searchService = new ModuleSearchService(
                resRef => _itemResolutionService?.ResolveItem(resRef)?.DisplayName);

            _indexedModuleDirPath = modulePath;
            _indexedModuleDirMtime = GetDirectoryMtime(modulePath);
        }
        _batchReplaceService ??= new BatchReplaceService(new BackupService());
        EnsureTlkResolver();
    }

    private static DateTime? GetDirectoryMtime(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        try
        {
            var di = new DirectoryInfo(path);
            return di.Exists ? di.LastWriteTimeUtc : (DateTime?)null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                   or System.Security.SecurityException or ArgumentException)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"GetDirectoryMtime failed for {UnifiedLogger.SanitizePath(path)}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Public invalidation hook (#2072) — called by callers that know the
    /// working directory contents just changed (e.g. ErfImportWindow on
    /// successful import). Forces the next search to rebuild the index.
    /// </summary>
    public void InvalidateSearchIndex()
    {
        _itemResolutionService = null;
        _searchService = null;
        _indexedModuleDirMtime = null;
        _indexedModuleDirPath = null;
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

        var plan = ResultDispatcher.Plan(
            filePath,
            resourceType,
            ResourceTypeToToolName,
            SettingsService.Instance.CodeEditorPath,
            File.Exists);

        ExecuteDispatchPlan(plan);
    }

    private void ExecuteDispatchPlan(DispatchPlan plan)
    {
        switch (plan.Action)
        {
            case DispatchAction.NoFile:
            case DispatchAction.FileMissing:
                return;

            case DispatchAction.ToolLaunch:
                var launched = ToolLauncherService.Instance.LaunchToolWithFile(
                    plan.ToolName!, plan.FilePath!);
                if (!launched && _viewModel != null)
                    _viewModel.StatusText =
                        $"Could not launch {plan.ToolName} for: {Path.GetFileName(plan.FilePath)}";
                return;

            case DispatchAction.ExternalEditor:
                StartExternalEditor(plan.EditorPath!, plan.FilePath!);
                return;

            case DispatchAction.OsDefault:
                StartOsDefault(plan.FilePath!);
                return;
        }
    }

    private void StartExternalEditor(string editorPath, string filePath)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = editorPath,
                UseShellExecute = false
            };
            foreach (var arg in ProcessArgumentBuilder.SingleFileArg(filePath))
            {
                startInfo.ArgumentList.Add(arg);
            }
            System.Diagnostics.Process.Start(startInfo)?.Dispose();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"External editor launch failed for {Path.GetFileName(filePath)}: {ex.Message}");
            StartOsDefault(filePath);
        }
    }

    private void StartOsDefault(string filePath)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(startInfo)?.Dispose();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"OS default handler failed for {Path.GetFileName(filePath)}: {ex.Message}");
            if (_viewModel != null)
                _viewModel.StatusText = $"No editor for {Path.GetFileName(filePath)}";
        }
    }

    private async void OnReplaceAllClick(object? sender, RoutedEventArgs e)
    {
        await OpenReplacePreviewAsync(selectionFilter: null);
    }

    private async void OnReplaceSelectedClick(object? sender, RoutedEventArgs e)
    {
        var selectedFilePaths = GetSelectedFilePaths();
        if (selectedFilePaths.Count == 0)
        {
            if (_viewModel != null)
                _viewModel.StatusText =
                    "Select a row first (click a file, match, or group), then click Replace Selected. Or use Replace All.";
            return;
        }

        await OpenReplacePreviewAsync(selectionFilter: selectedFilePaths);
    }

    /// <summary>
    /// Get file paths for the user's tree selection. The tree is populated
    /// programmatically with raw TreeViewItem instances that carry their row
    /// data in the .Tag property (FileSearchResult, MatchInfo, or null for
    /// group nodes). Inherited DataContext is the panel VM, so we read .Tag
    /// directly — same pattern OnResultDoubleTapped uses.
    ///
    /// Selecting a file row → that file.
    /// Selecting a match row → its parent file.
    /// Selecting a group row → every file under that group.
    /// </summary>
    private HashSet<string> GetSelectedFilePaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (ResultsTree.SelectedItem is not Avalonia.Controls.TreeViewItem item)
            return paths;

        switch (item.Tag)
        {
            case MatchInfo matchInfo:
                paths.Add(matchInfo.FilePath);
                break;
            case FileSearchResult fileResult:
                paths.Add(fileResult.FilePath);
                break;
            default:
                // Group node (no Tag) — walk its child file-result items.
                foreach (var child in item.Items)
                {
                    if (child is Avalonia.Controls.TreeViewItem childItem
                        && childItem.Tag is FileSearchResult childFile)
                    {
                        paths.Add(childFile.FilePath);
                    }
                }
                break;
        }
        return paths;
    }

    private async Task OpenReplacePreviewAsync(IReadOnlySet<string>? selectionFilter)
    {
        if (_viewModel == null || _parentWindow == null) return;

        var lastResults = _viewModel.GetLastResults();
        if (lastResults == null || lastResults.TotalMatches == 0) return;

        EnsureServices();

        var filesWithMatches = lastResults.Files.Where(f => f.MatchCount > 0).ToList();
        if (selectionFilter != null)
        {
            filesWithMatches = filesWithMatches
                .Where(f => selectionFilter.Contains(f.FilePath))
                .ToList();
        }
        if (filesWithMatches.Count == 0) return;

        var replaceText = _viewModel.ReplaceText;
        var preview = _batchReplaceService!.PreviewReplace(
            filesWithMatches,
            replaceText,
            allowResRefReplace: _viewModel.SearchFilenameResRef);

        // Dispatch to ResRef rename orchestrator when filename matches present.
        // Pass the user's selection set down so reference updates are surgical —
        // only files the user explicitly selected get their references rewritten.
        if (RenameDispatchHelpers.HasFilenameMatches(preview))
        {
            await DispatchResRefRenameAsync(preview, selectionFilter);
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
    ///
    /// <paramref name="selectionFilter"/>, when non-null, restricts the reference
    /// scan to only those file paths — the "surgical rename" mode (Path 1 per
    /// design discussion). When null, references are populated module-wide.
    /// </summary>
    private async Task DispatchResRefRenameAsync(
        BatchReplacePreview preview,
        IReadOnlySet<string>? selectionFilter)
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

        // Confirm the rename before touching any files (#2346). Filename rename used
        // to run with no review step — a typo in the replacement text renamed files
        // silently. Show the full old → new list and require explicit confirmation.
        var confirmDialog = new RenameConfirmDialog(plans);
        await confirmDialog.ShowDialog(_parentWindow);
        if (!confirmDialog.Confirmed)
        {
            _viewModel.StatusText = "Rename cancelled.";
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

        _viewModel.StatusText = selectionFilter != null
            ? $"Scanning {selectionFilter.Count} selected file(s) for references..."
            : "Scanning module for references...";

        try
        {
            var criteria = _viewModel.BuildSearchCriteria();
            await RenameDispatchHelpers.PopulateReferencesAsync(
                confirmedPlans, moduleDir, _viewModel.IncludeNss, criteria, selectionFilter);

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
                // Process any non-filename content matches that were in the same preview
                // but weren't consumed by the rename (e.g. ITP Name field replaces).
                // Without this, the user has to click Replace All twice to clean up
                // residual content rows.
                var renameMap = confirmedPlans.ToDictionary(
                    p => p.SourceFilePath,
                    p => p.TargetFilePath,
                    StringComparer.OrdinalIgnoreCase);
                var residualPreview = RenameDispatchHelpers.BuildResidualPreview(preview, renameMap);

                int residualReplacements = 0;
                if (residualPreview.Changes.Count > 0)
                {
                    var residualResult = await _batchReplaceService!.ExecuteReplaceAsync(
                        residualPreview, moduleName);
                    residualReplacements = residualResult.ReplacementsMade;
                }

                var totalRefs = result.ReferencesUpdated + residualReplacements;
                _viewModel.StatusText =
                    $"Renamed {result.RenamedFiles} file{(result.RenamedFiles != 1 ? "s" : "")}, " +
                    $"updated {totalRefs} reference{(totalRefs != 1 ? "s" : "")}. " +
                    "Backup created. Re-run search to refresh results.";
                // Don't clear results tree — leaves the user wondering whether everything
                // got renamed. Stale entries (renamed files at the old name) will simply
                // not be found on the next search.
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
        _indexedModuleDirMtime = null;
        _indexedModuleDirPath = null;
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
