using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using DialogEditor.Services;
using Radoub.Dictionary;

namespace DialogEditor.Controls
{
    /// <summary>
    /// TextBox with integrated spell-checking.
    /// Displays spelling errors with theme-aware underlines and provides
    /// right-click context menu with suggestions.
    /// </summary>
    public class SpellCheckTextBox : TextBox
    {
        // Use TextBox's default template/style instead of looking for SpellCheckTextBox-specific one
        protected override Type StyleKeyOverride => typeof(TextBox);

        private readonly List<SpellingError> _currentErrors = new();
        private SpellingError? _errorAtCaret;
        private DispatcherTimer? _spellCheckTimer;

        /// <summary>
        /// Whether spell-checking is enabled for this TextBox.
        /// </summary>
        public static readonly StyledProperty<bool> IsSpellCheckEnabledProperty =
            AvaloniaProperty.Register<SpellCheckTextBox, bool>(nameof(IsSpellCheckEnabled), true);

        public bool IsSpellCheckEnabled
        {
            get => GetValue(IsSpellCheckEnabledProperty);
            set => SetValue(IsSpellCheckEnabledProperty, value);
        }

        /// <summary>
        /// Current spelling errors in the text.
        /// </summary>
        public IReadOnlyList<SpellingError> CurrentErrors => _currentErrors;

        /// <summary>
        /// Event raised when spelling errors change.
        /// </summary>
        public event EventHandler<SpellingErrorsChangedEventArgs>? SpellingErrorsChanged;

        public SpellCheckTextBox()
        {
            // Debounce spell-check to avoid excessive checking during typing
            _spellCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _spellCheckTimer.Tick += OnSpellCheckTimerTick;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            // Watch for Text property changes
            if (change.Property == TextProperty)
            {
                if (IsSpellCheckEnabled && SpellCheckService.Instance.IsReady)
                {
                    // Restart the debounce timer
                    _spellCheckTimer?.Stop();
                    _spellCheckTimer?.Start();
                }
            }
        }

        private void OnSpellCheckTimerTick(object? sender, EventArgs e)
        {
            _spellCheckTimer?.Stop();
            CheckSpelling();
        }

        /// <summary>
        /// Perform spell check on current text.
        /// </summary>
        public void CheckSpelling()
        {
            if (!IsSpellCheckEnabled || !SpellCheckService.Instance.IsReady)
            {
                _currentErrors.Clear();
                UpdateErrorIndicator();
                return;
            }

            var text = Text ?? string.Empty;
            var previousCount = _currentErrors.Count;

            _currentErrors.Clear();
            _currentErrors.AddRange(SpellCheckService.Instance.CheckText(text));

            if (_currentErrors.Count != previousCount || _currentErrors.Count > 0)
            {
                SpellingErrorsChanged?.Invoke(this, new SpellingErrorsChangedEventArgs(_currentErrors));
                UpdateErrorIndicator();
            }
        }

        /// <summary>
        /// Update visual indicator for spelling errors.
        /// </summary>
        private void UpdateErrorIndicator()
        {
            if (_currentErrors.Count > 0)
            {
                // Set border to spelling error color
                BorderBrush = SpellCheckService.Instance.GetSpellingErrorBrush();
                BorderThickness = new Thickness(2);

                // Update tooltip with error summary
                var errorWords = string.Join(", ", _currentErrors.Take(5).Select(e => e.Word));
                if (_currentErrors.Count > 5)
                    errorWords += $" (+{_currentErrors.Count - 5} more)";
                ToolTip.SetTip(this, $"Spelling: {_currentErrors.Count} error(s): {errorWords}");
            }
            else
            {
                // Reset to default
                BorderBrush = null;
                BorderThickness = new Thickness(1);
                ToolTip.SetTip(this, null);
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            // Check if right-click on a misspelled word
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            {
                UpdateErrorAtCaret();

                // Show spell-check context menu if on an error
                if (_errorAtCaret != null)
                {
                    e.Handled = true;
                    ShowSpellCheckContextMenu();
                }
            }
        }

        /// <summary>
        /// Show context menu with spelling suggestions.
        /// </summary>
        private void ShowSpellCheckContextMenu()
        {
            var error = _errorAtCaret;
            if (error == null) return;

            var menu = new ContextMenu();
            var suggestions = SpellCheckService.Instance.GetSuggestions(error.Word, 5).ToList();

            if (suggestions.Any())
            {
                foreach (var suggestion in suggestions)
                {
                    var item = new MenuItem
                    {
                        Header = suggestion,
                        FontWeight = FontWeight.Bold
                    };
                    var suggestionCopy = suggestion; // Capture for lambda
                    item.Click += (_, _) => ReplaceWordAtCaret(suggestionCopy);
                    menu.Items.Add(item);
                }

                menu.Items.Add(new Separator());
            }
            else
            {
                menu.Items.Add(new MenuItem
                {
                    Header = "(No suggestions)",
                    IsEnabled = false
                });
                menu.Items.Add(new Separator());
            }

            // Ignore for session
            var ignoreItem = new MenuItem { Header = $"Ignore \"{error.Word}\"" };
            ignoreItem.Click += (_, _) => IgnoreWordAtCaret();
            menu.Items.Add(ignoreItem);

            // Add to dictionary
            var addItem = new MenuItem { Header = $"Add \"{error.Word}\" to Dictionary" };
            addItem.Click += (_, _) => AddWordAtCaretToDictionary();
            menu.Items.Add(addItem);

            menu.Open(this);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Update error at caret when navigating with keyboard
            if (e.Key == Key.Left || e.Key == Key.Right ||
                e.Key == Key.Up || e.Key == Key.Down ||
                e.Key == Key.Home || e.Key == Key.End)
            {
                Dispatcher.UIThread.Post(UpdateErrorAtCaret);
            }
        }

        private void UpdateErrorAtCaret()
        {
            var caretIndex = CaretIndex;
            _errorAtCaret = _currentErrors.FirstOrDefault(err =>
                caretIndex >= err.StartIndex && caretIndex <= err.StartIndex + err.Length);
        }

        /// <summary>
        /// Get the misspelled word at the current caret position, if any.
        /// </summary>
        public SpellingError? GetErrorAtCaret()
        {
            UpdateErrorAtCaret();
            return _errorAtCaret;
        }

        /// <summary>
        /// Get spelling suggestions for the word at caret.
        /// </summary>
        public IEnumerable<string> GetSuggestionsAtCaret(int maxSuggestions = 5)
        {
            var error = GetErrorAtCaret();
            if (error == null)
                return Enumerable.Empty<string>();

            return SpellCheckService.Instance.GetSuggestions(error.Word, maxSuggestions);
        }

        /// <summary>
        /// Replace the misspelled word at caret with a suggestion.
        /// </summary>
        public void ReplaceWordAtCaret(string replacement)
        {
            var error = GetErrorAtCaret();
            if (error == null || string.IsNullOrEmpty(Text))
                return;

            var text = Text;
            var newText = text.Substring(0, error.StartIndex) +
                          replacement +
                          text.Substring(error.StartIndex + error.Length);

            Text = newText;
            CaretIndex = error.StartIndex + replacement.Length;
            CheckSpelling();
        }

        /// <summary>
        /// Ignore the word at caret for this session.
        /// </summary>
        public void IgnoreWordAtCaret()
        {
            var error = GetErrorAtCaret();
            if (error == null)
                return;

            SpellCheckService.Instance.IgnoreForSession(error.Word);
            CheckSpelling();
        }

        /// <summary>
        /// Add the word at caret to the custom dictionary.
        /// </summary>
        public void AddWordAtCaretToDictionary()
        {
            var error = GetErrorAtCaret();
            if (error == null)
                return;

            SpellCheckService.Instance.AddToCustomDictionary(error.Word);
            CheckSpelling();
        }

        /// <summary>
        /// Get error count for status display.
        /// </summary>
        public int ErrorCount => _currentErrors.Count;

        /// <summary>
        /// Check if a specific position has a spelling error.
        /// </summary>
        public bool HasErrorAtPosition(int position)
        {
            return _currentErrors.Any(err =>
                position >= err.StartIndex && position < err.StartIndex + err.Length);
        }

        /// <summary>
        /// Get the error at a specific position.
        /// </summary>
        public SpellingError? GetErrorAtPosition(int position)
        {
            return _currentErrors.FirstOrDefault(err =>
                position >= err.StartIndex && position < err.StartIndex + err.Length);
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            _spellCheckTimer?.Stop();
            _spellCheckTimer = null;
        }
    }

    /// <summary>
    /// Event args for spelling errors changed.
    /// </summary>
    public class SpellingErrorsChangedEventArgs : EventArgs
    {
        public IReadOnlyList<SpellingError> Errors { get; }

        public SpellingErrorsChangedEventArgs(IReadOnlyList<SpellingError> errors)
        {
            Errors = errors;
        }
    }
}
