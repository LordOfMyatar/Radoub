using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Radoub.Formats.Logging;
using Radoub.Formats.Search;
using Radoub.UI.Services.Search;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Radoub.UI.Controls
{
    /// <summary>
    /// Shared search bar control for finding and replacing text in GFF-based files.
    /// Must be initialized with a FileSearchService via <see cref="Initialize"/>.
    /// </summary>
    public partial class SearchBar : UserControl
    {
        private FileSearchService? _searchService;
        private string? _currentFilePath;
        private CancellationTokenSource? _debounceCts;
        private bool _textBoxEventsWired;

        /// <summary>Fired when the user navigates to a match</summary>
        public event EventHandler<SearchMatch?>? NavigateToMatch;

        /// <summary>Fired when search results change (for highlighting)</summary>
        public event EventHandler<IReadOnlyList<SearchMatch>>? SearchResultsChanged;

        /// <summary>Fired when the file is modified by a replace operation</summary>
        public event EventHandler? FileModified;

        public SearchBar()
        {
            InitializeComponent();

            Initialized += (_, _) => WireTextBoxEvents("Initialized");
            Loaded += (_, _) => WireTextBoxEvents("Loaded");
        }

        private void WireTextBoxEvents(string source)
        {
            if (_textBoxEventsWired) return;
            var textBox = GetSearchTextBox();
            if (textBox != null)
            {
                textBox.TextChanged += OnSearchTextChanged;
                textBox.KeyDown += OnSearchTextKeyDown;
                _textBoxEventsWired = true;
                UnifiedLogger.LogUI(LogLevel.DEBUG, $"SearchBar: TextBox events wired via {source}");
            }
            else
            {
                UnifiedLogger.LogUI(LogLevel.WARN, $"SearchBar: TextBox NOT found during {source}");
            }
        }

        /// <summary>
        /// Initialize the search bar with a search service and optional field filter items.
        /// Call this once after construction before using the search bar.
        /// </summary>
        /// <param name="searchService">The file search service wrapping a format-specific provider</param>
        /// <param name="fieldFilters">Optional field filter definitions (label → SearchFieldCategory).
        /// If null or empty, only "All Fields" is shown.</param>
        public void Initialize(FileSearchService searchService,
            IReadOnlyList<(string Label, SearchFieldCategory Category)>? fieldFilters = null)
        {
            _searchService = searchService;
            UnifiedLogger.LogUI(LogLevel.INFO, "SearchBar: Initialized with search service");

            var combo = GetFieldFilterCombo();
            if (combo != null && fieldFilters != null)
            {
                foreach (var (label, category) in fieldFilters)
                {
                    combo.Items.Add(new ComboBoxItem
                    {
                        Content = label,
                        Tag = category.ToString()
                    });
                }
            }
        }

        /// <summary>Show the search bar and focus the text input.</summary>
        public void Show(string? filePath)
        {
            _currentFilePath = filePath;
            IsVisible = true;
            UnifiedLogger.LogUI(LogLevel.DEBUG, $"SearchBar.Show: filePath={(!string.IsNullOrEmpty(filePath) ? UnifiedLogger.SanitizePath(filePath) : "(null)")}, searchService={((_searchService != null) ? "set" : "NULL")}");

            Dispatcher.UIThread.Post(() =>
            {
                var textBox = GetSearchTextBox();
                textBox?.Focus();
                textBox?.SelectAll();
            }, DispatcherPriority.Input);
        }

        /// <summary>Hide the search bar and clear results.</summary>
        public void Hide()
        {
            IsVisible = false;
            _searchService?.Clear();
            UpdateMatchCount();
            SearchResultsChanged?.Invoke(this, Array.Empty<SearchMatch>());
        }

        /// <summary>Update the file path for searching (e.g., after save or file switch).</summary>
        public void UpdateFilePath(string? filePath)
        {
            _currentFilePath = filePath;
        }

        /// <summary>Navigate to next match</summary>
        public void FindNext()
        {
            if (_searchService == null) return;
            var match = _searchService.NextMatch();
            UpdateMatchCount();
            NavigateToMatch?.Invoke(this, match);
        }

        /// <summary>Navigate to previous match</summary>
        public void FindPrevious()
        {
            if (_searchService == null) return;
            var match = _searchService.PreviousMatch();
            UpdateMatchCount();
            NavigateToMatch?.Invoke(this, match);
        }

        /// <summary>Show the search bar in replace mode (Ctrl+H).</summary>
        public void ShowReplace(string? filePath)
        {
            Show(filePath);
            var replaceRow = this.FindControl<DockPanel>("ReplaceRow");
            if (replaceRow != null) replaceRow.IsVisible = true;
        }

        /// <summary>Toggle replace row visibility.</summary>
        public void ToggleReplace()
        {
            var replaceRow = this.FindControl<DockPanel>("ReplaceRow");
            if (replaceRow != null) replaceRow.IsVisible = !replaceRow.IsVisible;
        }

        private TextBox? GetSearchTextBox() => this.FindControl<TextBox>("SearchTextBox");
        private TextBox? GetReplaceTextBox() => this.FindControl<TextBox>("ReplaceTextBox");
        private TextBlock? GetMatchCountText() => this.FindControl<TextBlock>("MatchCountText");
        private ComboBox? GetFieldFilterCombo() => this.FindControl<ComboBox>("FieldFilterCombo");
        private CheckBox? GetRegexCheck() => this.FindControl<CheckBox>("RegexCheck");
        private CheckBox? GetCaseSensitiveCheck() => this.FindControl<CheckBox>("CaseSensitiveCheck");

        private void ExecuteSearch()
        {
            if (_searchService == null)
            {
                UnifiedLogger.LogUI(LogLevel.WARN, "SearchBar.ExecuteSearch: _searchService is null — Initialize() not called?");
                return;
            }

            var textBox = GetSearchTextBox();
            var pattern = textBox?.Text;

            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(_currentFilePath))
            {
                if (string.IsNullOrEmpty(_currentFilePath))
                    UnifiedLogger.LogUI(LogLevel.DEBUG, "SearchBar.ExecuteSearch: _currentFilePath is null/empty — no file loaded?");
                _searchService.Clear();
                UpdateMatchCount();
                SearchResultsChanged?.Invoke(this, Array.Empty<SearchMatch>());
                return;
            }

            UnifiedLogger.LogUI(LogLevel.DEBUG, $"SearchBar.ExecuteSearch: pattern='{pattern}', file='{UnifiedLogger.SanitizePath(_currentFilePath)}'");
            var criteria = BuildCriteria(pattern);
            _searchService.Search(_currentFilePath, criteria);
            UnifiedLogger.LogUI(LogLevel.DEBUG, $"SearchBar.ExecuteSearch: {_searchService.MatchCount} matches found");
            UpdateMatchCount();
            SearchResultsChanged?.Invoke(this, _searchService.Matches);
        }

        private SearchCriteria BuildCriteria(string pattern)
        {
            SearchFieldCategory[]? categoryFilter = null;

            if (GetFieldFilterCombo()?.SelectedItem is ComboBoxItem item && item.Tag is string categoryTag)
            {
                if (Enum.TryParse<SearchFieldCategory>(categoryTag, out var category))
                {
                    categoryFilter = new[] { category };
                }
            }

            return new SearchCriteria
            {
                Pattern = pattern,
                IsRegex = GetRegexCheck()?.IsChecked == true,
                CaseSensitive = GetCaseSensitiveCheck()?.IsChecked == true,
                CategoryFilter = categoryFilter
            };
        }

        private void UpdateMatchCount()
        {
            var matchCountText = GetMatchCountText();
            if (matchCountText == null)
            {
                UnifiedLogger.LogUI(LogLevel.WARN, "SearchBar.UpdateMatchCount: MatchCountText control not found");
                return;
            }

            var count = _searchService?.MatchCount ?? 0;
            var index = _searchService?.CurrentIndex ?? -1;

            matchCountText.Text = count switch
            {
                0 when string.IsNullOrEmpty(GetSearchTextBox()?.Text) => "",
                0 => "No matches",
                _ => $"{index + 1} of {count}"
            };
            UnifiedLogger.LogUI(LogLevel.DEBUG, $"SearchBar.UpdateMatchCount: '{matchCountText.Text}'");
        }

        #region Event Handlers

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(300, token);
                    if (!token.IsCancellationRequested)
                        ExecuteSearch();
                }
                catch (OperationCanceledException)
                {
                    // Expected — user typed another character
                }
            });
        }

        private void OnSearchTextKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                        FindPrevious();
                    else
                        FindNext();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    Hide();
                    e.Handled = true;
                    break;
            }
        }

        private void OnNextClick(object? sender, RoutedEventArgs e) => FindNext();
        private void OnPreviousClick(object? sender, RoutedEventArgs e) => FindPrevious();
        private void OnCloseClick(object? sender, RoutedEventArgs e) => Hide();

        private void OnReplaceClick(object? sender, RoutedEventArgs e)
        {
            if (_searchService == null) return;

            var replaceText = GetReplaceTextBox()?.Text ?? "";
            var pattern = GetSearchTextBox()?.Text;
            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(_currentFilePath))
                return;

            var criteria = BuildCriteria(pattern);
            var result = _searchService.ReplaceCurrent(_currentFilePath, replaceText, criteria);

            if (result?.Success == true)
            {
                UpdateMatchCount();
                SearchResultsChanged?.Invoke(this, _searchService.Matches);
                FileModified?.Invoke(this, EventArgs.Empty);

                if (_searchService.MatchCount > 0)
                    NavigateToMatch?.Invoke(this, _searchService.CurrentMatch);
            }
        }

        private void OnReplaceAllClick(object? sender, RoutedEventArgs e)
        {
            if (_searchService == null) return;

            var replaceText = GetReplaceTextBox()?.Text ?? "";
            var pattern = GetSearchTextBox()?.Text;
            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(_currentFilePath))
                return;

            var criteria = BuildCriteria(pattern);
            var count = _searchService.ReplaceAll(_currentFilePath, replaceText, criteria);

            if (count > 0)
            {
                UpdateMatchCount();
                SearchResultsChanged?.Invoke(this, _searchService.Matches);
                FileModified?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnFieldFilterChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (IsVisible && !string.IsNullOrEmpty(GetSearchTextBox()?.Text))
                ExecuteSearch();
        }

        private void OnOptionsChanged(object? sender, RoutedEventArgs e)
        {
            if (IsVisible && !string.IsNullOrEmpty(GetSearchTextBox()?.Text))
                ExecuteSearch();
        }

        #endregion
    }
}
