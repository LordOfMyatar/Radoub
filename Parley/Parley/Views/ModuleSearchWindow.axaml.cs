using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Search;
using Radoub.UI.Services.Search;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DialogEditor.Views
{
    /// <summary>
    /// Module-wide search window — searches all DLG files in the module directory.
    /// Uses ModuleSearchService for orchestration.
    /// </summary>
    public partial class ModuleSearchWindow : Window
    {
        private readonly ModuleSearchService _searchService = new();
        private string _modulePath = "";
        private string? _currentFilePath;
        private CancellationTokenSource? _searchCts;

        public ModuleSearchWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initialize with module directory and optional current file.
        /// </summary>
        public void Initialize(string modulePath, string? currentFilePath)
        {
            _modulePath = modulePath;
            _currentFilePath = currentFilePath;

            var dirName = Path.GetFileName(modulePath);
            Title = $"Search Module \u2014 {dirName}";
            ModulePathText.Text = modulePath;

            SearchPatternBox.Focus();
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
            if (e.Key == Key.Enter)
            {
                await ExecuteSearchAsync();
                e.Handled = true;
            }
        }

        private async Task ExecuteSearchAsync()
        {
            var pattern = SearchPatternBox?.Text;
            if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrEmpty(_modulePath))
                return;

            // Cancel any previous search
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            // Build criteria
            var criteria = BuildCriteria(pattern);
            var validationError = criteria.Validate();
            if (validationError != null)
            {
                StatusText.Text = $"Invalid pattern: {validationError}";
                return;
            }

            // UI: show progress
            SetSearchingState(true);
            ResultsTree.ItemsSource = null;
            DurationText.Text = "";

            var progress = new Progress<ScanProgress>(p =>
            {
                StatusText.Text = p.Phase == "Searching"
                    ? $"Searching {p.CurrentFile}... ({p.FilesScanned}/{p.TotalFiles}, {p.MatchesFound} matches)"
                    : p.Phase;
            });

            try
            {
                var results = await _searchService.ScanModuleAsync(_modulePath, criteria, progress, token);
                DisplayResults(results);
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "Search cancelled.";
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Module search failed: {ex.Message}");
                StatusText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                SetSearchingState(false);
            }
        }

        private SearchCriteria BuildCriteria(string pattern)
        {
            SearchFieldCategory[]? categoryFilter = null;
            if (FieldFilterCombo?.SelectedItem is ComboBoxItem item && item.Tag is string categoryTag)
            {
                if (Enum.TryParse<SearchFieldCategory>(categoryTag, out var category))
                    categoryFilter = new[] { category };
            }

            return new SearchCriteria
            {
                Pattern = pattern,
                IsRegex = RegexCheck?.IsChecked == true,
                CaseSensitive = CaseSensitiveCheck?.IsChecked == true,
                CategoryFilter = categoryFilter,
                // Only search DLG files (Parley's domain)
                FileTypeFilter = new[] { ResourceTypes.Dlg }
            };
        }

        private void SetSearchingState(bool searching)
        {
            SearchButton.IsEnabled = !searching;
            CancelButton.IsVisible = searching;
            SearchProgress.IsVisible = searching;
            SearchPatternBox.IsEnabled = !searching;
        }

        private void DisplayResults(ModuleSearchResults results)
        {
            var duration = results.Duration;
            DurationText.Text = duration.TotalSeconds < 1
                ? $"{duration.TotalMilliseconds:F0}ms"
                : $"{duration.TotalSeconds:F1}s";

            if (results.TotalMatches == 0)
            {
                StatusText.Text = results.WasCancelled
                    ? $"Cancelled. Scanned {results.TotalFilesScanned} files."
                    : $"No matches found in {results.TotalFilesScanned} files.";
                ResultsTree.ItemsSource = null;
                return;
            }

            StatusText.Text = $"{results.TotalMatches} matches in {results.FilesWithMatches} files ({results.TotalFilesScanned} scanned)";

            // Build tree items: File > Matches
            var treeItems = new List<TreeViewItem>();

            foreach (var fileResult in results.Files.Where(f => f.MatchCount > 0).OrderBy(f => f.FileName))
            {
                var fileNode = new TreeViewItem
                {
                    Header = $"{fileResult.FileName} ({fileResult.MatchCount} matches)",
                    Tag = fileResult,
                    IsExpanded = true
                };

                foreach (var match in fileResult.Matches)
                {
                    var locationText = match.Location is DlgMatchLocation dlgLoc
                        ? dlgLoc.DisplayPath
                        : match.Field.Name;

                    var preview = match.FullFieldValue.Length > 80
                        ? match.FullFieldValue[..80] + "..."
                        : match.FullFieldValue;

                    var matchNode = new TreeViewItem
                    {
                        Header = $"[{locationText}] {match.Field.Name}: {preview}",
                        Tag = new MatchInfo(fileResult.FilePath, match)
                    };

                    fileNode.Items.Add(matchNode);
                }

                treeItems.Add(fileNode);
            }

            // Add parse error nodes
            foreach (var fileResult in results.Files.Where(f => f.HadParseError))
            {
                var errorNode = new TreeViewItem
                {
                    Header = $"{fileResult.FileName} (parse error: {fileResult.ParseError})",
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

            if (item.Tag is MatchInfo matchInfo)
            {
                filePath = matchInfo.FilePath;
            }
            else if (item.Tag is FileSearchResult fileResult)
            {
                filePath = fileResult.FilePath;
            }

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            // Open the file in a new Parley instance
            try
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        ArgumentList = { "--file", filePath },
                        UseShellExecute = false
                    });
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to open file: {ex.Message}");
                StatusText.Text = $"Could not open: {Path.GetFileName(filePath)}";
            }
        }

        /// <summary>Pairs a match with its source file path for double-click navigation</summary>
        private record MatchInfo(string FilePath, SearchMatch Match);
    }
}
