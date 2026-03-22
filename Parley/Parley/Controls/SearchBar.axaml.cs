using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DialogEditor.Services;
using Radoub.Formats.Search;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DialogEditor.Controls
{
    /// <summary>
    /// Search bar control for finding text in the current dialog.
    /// Communicates with parent via events.
    /// </summary>
    public partial class SearchBar : UserControl
    {
        private readonly DialogSearchService _searchService = new();
        private string? _currentFilePath;
        private CancellationTokenSource? _debounceCts;

        /// <summary>Fired when the user navigates to a match</summary>
        public event EventHandler<SearchMatch?>? NavigateToMatch;

        /// <summary>Fired when search results change (for highlighting)</summary>
        public event EventHandler<IReadOnlyList<SearchMatch>>? SearchResultsChanged;

        public SearchBar()
        {
            InitializeComponent();

            // Wire events in code-behind — AXAML event wiring doesn't connect
            // reliably when UserControl is inside TabItem deferred content.
            // Use Initialized event to ensure controls are resolved.
            Initialized += (_, _) =>
            {
                var textBox = GetSearchTextBox();
                if (textBox != null)
                {
                    textBox.TextChanged += OnSearchTextChanged;
                    textBox.KeyDown += OnSearchTextKeyDown;
                }
            };
        }

        /// <summary>
        /// Show the search bar and focus the text input.
        /// </summary>
        public void Show(string? filePath)
        {
            _currentFilePath = filePath;
            IsVisible = true;

            // Defer focus — control must be visible and rendered first
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var textBox = GetSearchTextBox();
                textBox?.Focus();
                textBox?.SelectAll();
            }, Avalonia.Threading.DispatcherPriority.Input);
        }

        /// <summary>
        /// Hide the search bar and clear results.
        /// </summary>
        public void Hide()
        {
            IsVisible = false;
            _searchService.Clear();
            UpdateMatchCount();
            SearchResultsChanged?.Invoke(this, Array.Empty<SearchMatch>());
        }

        /// <summary>
        /// Update the file path for searching (e.g., after save or file switch).
        /// </summary>
        public void UpdateFilePath(string? filePath)
        {
            _currentFilePath = filePath;
        }

        /// <summary>Navigate to next match</summary>
        public void FindNext()
        {
            var match = _searchService.NextMatch();
            UpdateMatchCount();
            NavigateToMatch?.Invoke(this, match);
        }

        /// <summary>Navigate to previous match</summary>
        public void FindPrevious()
        {
            var match = _searchService.PreviousMatch();
            UpdateMatchCount();
            NavigateToMatch?.Invoke(this, match);
        }

        /// <summary>Fired when the file is modified by a replace operation</summary>
        public event EventHandler? FileModified;

        /// <summary>
        /// Show the search bar in replace mode (Ctrl+H).
        /// </summary>
        public void ShowReplace(string? filePath)
        {
            Show(filePath);
            var replaceRow = this.FindControl<DockPanel>("ReplaceRow");
            if (replaceRow != null) replaceRow.IsVisible = true;
        }

        /// <summary>
        /// Toggle replace row visibility.
        /// </summary>
        public void ToggleReplace()
        {
            var replaceRow = this.FindControl<DockPanel>("ReplaceRow");
            if (replaceRow != null) replaceRow.IsVisible = !replaceRow.IsVisible;
        }

        // Resolve named controls — auto-generated fields may be null inside TabItem
        private TextBox? GetSearchTextBox() => this.FindControl<TextBox>("SearchTextBox");
        private TextBox? GetReplaceTextBox() => this.FindControl<TextBox>("ReplaceTextBox");
        private TextBlock? GetMatchCountText() => this.FindControl<TextBlock>("MatchCountText");
        private ComboBox? GetFieldFilterCombo() => this.FindControl<ComboBox>("FieldFilterCombo");
        private CheckBox? GetRegexCheck() => this.FindControl<CheckBox>("RegexCheck");
        private CheckBox? GetCaseSensitiveCheck() => this.FindControl<CheckBox>("CaseSensitiveCheck");

        private void ExecuteSearch()
        {
            var textBox = GetSearchTextBox();
            var pattern = textBox?.Text;

            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(_currentFilePath))
            {
                _searchService.Clear();
                UpdateMatchCount();
                SearchResultsChanged?.Invoke(this, Array.Empty<SearchMatch>());
                return;
            }

            var criteria = BuildCriteria(pattern);
            _searchService.Search(_currentFilePath, criteria);
            UpdateMatchCount();
            SearchResultsChanged?.Invoke(this, _searchService.Matches);

            // Auto-navigate to first match
            if (_searchService.MatchCount > 0)
            {
                NavigateToMatch?.Invoke(this, _searchService.CurrentMatch);
            }
        }

        private SearchCriteria BuildCriteria(string pattern)
        {
            SearchFieldCategory[]? categoryFilter = null;

            // Apply field category filter based on combo selection
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
            if (matchCountText == null) return;

            var count = _searchService.MatchCount;
            var index = _searchService.CurrentIndex;

            matchCountText.Text = count switch
            {
                0 when string.IsNullOrEmpty(GetSearchTextBox()?.Text) => "",
                0 => "No matches",
                _ => $"{index + 1} of {count}"
            };
        }

        #region Event Handlers

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        {
            // Debounce: wait 300ms after last keystroke before searching.
            // Prevents searching partial words ("t", "te", "tes") and
            // avoids re-reading the file from disk on every keystroke.
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
