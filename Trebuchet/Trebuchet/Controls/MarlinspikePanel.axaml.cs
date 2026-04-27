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

    private void OnReplaceAllClick(object? sender, RoutedEventArgs e)
    {
        OpenReplacePreview(selectAll: true);
    }

    private void OnReplaceSelectedClick(object? sender, RoutedEventArgs e)
    {
        OpenReplacePreview(selectAll: true);
    }

    private void OpenReplacePreview(bool selectAll)
    {
        if (_viewModel == null || _parentWindow == null) return;

        var lastResults = _viewModel.GetLastResults();
        if (lastResults == null || lastResults.TotalMatches == 0) return;

        EnsureServices();

        var filesWithMatches = lastResults.Files.Where(f => f.MatchCount > 0).ToList();
        if (filesWithMatches.Count == 0) return;

        var replaceText = _viewModel.ReplaceText;
        var preview = _batchReplaceService!.PreviewReplace(filesWithMatches, replaceText);

        var previewWindow = new ReplacePreviewWindow();
        previewWindow.Initialize(preview, _viewModel.SearchPattern, replaceText, _batchReplaceService);
        previewWindow.ReplacementComplete += OnReplacementComplete;
        previewWindow.Show(_parentWindow);
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
