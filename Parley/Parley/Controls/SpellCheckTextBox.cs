using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
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
                // Subtle indicator: thin colored left border (like VS Code's problem gutter)
                var errorBrush = SpellCheckService.Instance.GetSpellingErrorBrush();
                BorderBrush = errorBrush;
                BorderThickness = new Thickness(3, 1, 1, 1); // Thicker left border only

                // Build clear tooltip showing exactly which words are misspelled
                var errorList = _currentErrors.Take(8).Select(e => $"• {e.Word}");
                var tooltip = $"Spelling errors ({_currentErrors.Count}):\n{string.Join("\n", errorList)}";
                if (_currentErrors.Count > 8)
                    tooltip += $"\n  ...and {_currentErrors.Count - 8} more";
                tooltip += "\n\nRight-click a word for suggestions";
                ToolTip.SetTip(this, tooltip);
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
            var point = e.GetCurrentPoint(this);

            // Check if right-click
            if (point.Properties.IsRightButtonPressed)
            {
                // Get the character index at the click position
                var clickPosition = point.Position;
                var charIndex = GetCharacterIndexFromPoint(clickPosition);

                // Find error at clicked position
                _errorAtCaret = _currentErrors.FirstOrDefault(err =>
                    charIndex >= err.StartIndex && charIndex <= err.StartIndex + err.Length);

                // Build and set context menu (replaces default Cut/Copy/Paste)
                BuildContextMenu();
            }

            base.OnPointerPressed(e);
        }

        /// <summary>
        /// Build context menu with spell-check options + standard edit options.
        /// </summary>
        private void BuildContextMenu()
        {
            var menu = new ContextMenu();

            // If on a misspelled word, show spell-check options first
            if (_errorAtCaret != null)
            {
                var error = _errorAtCaret;
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
                        var suggestionCopy = suggestion;
                        item.Click += (_, _) => ReplaceWordAtPosition(error, suggestionCopy);
                        menu.Items.Add(item);
                    }
                }
                else
                {
                    menu.Items.Add(new MenuItem { Header = "(No suggestions)", IsEnabled = false });
                }

                menu.Items.Add(new Separator());

                var ignoreItem = new MenuItem { Header = $"Ignore \"{error.Word}\"" };
                ignoreItem.Click += (_, _) => { SpellCheckService.Instance.IgnoreForSession(error.Word); CheckSpelling(); };
                menu.Items.Add(ignoreItem);

                var addItem = new MenuItem { Header = $"Add \"{error.Word}\" to Dictionary" };
                addItem.Click += (_, _) => { SpellCheckService.Instance.AddToCustomDictionary(error.Word); CheckSpelling(); };
                menu.Items.Add(addItem);

                menu.Items.Add(new Separator());
            }

            // Standard edit options - use TextBox built-in commands
            var cutItem = new MenuItem { Header = "Cut" };
            cutItem.Click += (_, _) => Cut();
            cutItem.IsEnabled = SelectedText?.Length > 0;
            menu.Items.Add(cutItem);

            var copyItem = new MenuItem { Header = "Copy" };
            copyItem.Click += (_, _) => Copy();
            copyItem.IsEnabled = SelectedText?.Length > 0;
            menu.Items.Add(copyItem);

            var pasteItem = new MenuItem { Header = "Paste" };
            pasteItem.Click += (_, _) => Paste();
            menu.Items.Add(pasteItem);

            // Set as the context menu (replaces default)
            ContextMenu = menu;
        }

        /// <summary>
        /// Replace a word at a specific position with a suggestion.
        /// </summary>
        private void ReplaceWordAtPosition(SpellingError error, string replacement)
        {
            var text = Text;
            if (string.IsNullOrEmpty(text)) return;

            var newText = text.Substring(0, error.StartIndex) +
                          replacement +
                          text.Substring(error.StartIndex + error.Length);

            Text = newText;
            CaretIndex = error.StartIndex + replacement.Length;
            CheckSpelling();
        }

        /// <summary>
        /// Get character index from a point in the TextBox.
        /// Uses simple character position estimation based on click location.
        /// </summary>
        private int GetCharacterIndexFromPoint(Point point)
        {
            var text = Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
                return 0;

            // Try to find TextPresenter for accurate hit testing
            var presenter = this.FindDescendantOfType<TextPresenter>();
            if (presenter?.TextLayout != null)
            {
                // Adjust point relative to presenter
                var presenterBounds = presenter.Bounds;
                var presenterPoint = new Point(
                    point.X - presenterBounds.X,
                    point.Y - presenterBounds.Y);
                var hit = presenter.TextLayout.HitTestPoint(presenterPoint);
                return Math.Clamp(hit.TextPosition, 0, text.Length);
            }

            // Fallback: use caret position (already set by base class on click)
            return CaretIndex;
        }

        /// <summary>
        /// Find a descendant control of a specific type.
        /// </summary>
        private T? FindDescendantOfType<T>() where T : class
        {
            var queue = new Queue<Control>();
            queue.Enqueue(this);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current is T match && current != this)
                    return match;

                foreach (var child in current.GetVisualChildren().OfType<Control>())
                    queue.Enqueue(child);
            }

            return null;
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
