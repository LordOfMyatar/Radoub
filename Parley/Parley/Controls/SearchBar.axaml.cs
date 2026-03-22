using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DialogEditor.Services;
using Radoub.Formats.Search;
using System;
using System.Collections.Generic;

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

        /// <summary>Fired when the user navigates to a match</summary>
        public event EventHandler<SearchMatch?>? NavigateToMatch;

        /// <summary>Fired when search results change (for highlighting)</summary>
        public event EventHandler<IReadOnlyList<SearchMatch>>? SearchResultsChanged;

        public SearchBar()
        {
            InitializeComponent();

            // Wire events in code-behind as a safety net — AXAML event wiring
            // may not connect reliably inside TabItem deferred content
            var textBox = this.FindControl<TextBox>("SearchTextBox");
            if (textBox != null)
            {
                textBox.TextChanged += OnSearchTextChanged;
                textBox.KeyDown += OnSearchTextKeyDown;
            }
        }

        /// <summary>
        /// Show the search bar and focus the text input.
        /// </summary>
        public void Show(string? filePath)
        {
            _currentFilePath = filePath;
            IsVisible = true;
            var textBox = GetSearchTextBox();
            textBox?.Focus();
            textBox?.SelectAll();
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

        // Resolve named controls — auto-generated fields may be null inside TabItem
        private TextBox? GetSearchTextBox() => this.FindControl<TextBox>("SearchTextBox");
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
            ExecuteSearch();
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
