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
using Avalonia.Layout;

namespace DialogEditor.Controls
{
    /// <summary>
    /// TextBox with integrated spell-checking.
    /// Displays spelling errors with theme-aware squiggly underlines and provides
    /// right-click context menu with suggestions.
    /// </summary>
    public class SpellCheckTextBox : TextBox
    {
        // Use TextBox's default template/style instead of looking for SpellCheckTextBox-specific one
        protected override Type StyleKeyOverride => typeof(TextBox);

        private readonly List<SpellingError> _currentErrors = new();
        private SpellingError? _errorAtCaret;
        private DispatcherTimer? _spellCheckTimer;
        private SpellCheckUnderlineOverlay? _underlineOverlay;
        private TextPresenter? _textPresenter;

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

            // Subscribe to settings changes to immediately clear/show underlines
            SettingsService.Instance.PropertyChanged += OnSettingsChanged;
        }

        private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsService.SpellCheckEnabled))
            {
                // Immediately recheck spelling (will clear if disabled)
                CheckSpelling();
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            // Find the TextPresenter in the template
            _textPresenter = e.NameScope.Find<TextPresenter>("PART_TextPresenter");

            if (_textPresenter != null)
            {
                // Create the underline overlay
                _underlineOverlay = new SpellCheckUnderlineOverlay(this);

                // Find the parent of the TextPresenter and add overlay as sibling
                if (_textPresenter.Parent is Panel panel)
                {
                    panel.Children.Add(_underlineOverlay);
                }

                // Subscribe to layout updates to redraw underlines when text reflows
                _textPresenter.PropertyChanged += (s, args) =>
                {
                    if (args.Property.Name == "TextLayout")
                    {
                        _underlineOverlay?.InvalidateVisual();
                    }
                };
            }
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
            // Invalidate the underline overlay to redraw squiggly lines
            _underlineOverlay?.InvalidateVisual();

            // Remove tooltip - squiggly lines are the primary indicator now
            // Users right-click on underlined words for suggestions
            ToolTip.SetTip(this, null);
        }

        /// <summary>
        /// Get the TextPresenter for hit testing and text bounds.
        /// </summary>
        internal TextPresenter? GetTextPresenter() => _textPresenter ?? FindDescendantOfType<TextPresenter>();

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
        internal T? FindDescendantOfType<T>() where T : class
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

    /// <summary>
    /// Overlay control that draws squiggly underlines for spelling errors.
    /// Positioned over the TextPresenter to render underlines at correct text positions.
    /// </summary>
    internal class SpellCheckUnderlineOverlay : Control
    {
        private readonly SpellCheckTextBox _owner;

        public SpellCheckUnderlineOverlay(SpellCheckTextBox owner)
        {
            _owner = owner;
            IsHitTestVisible = false; // Allow clicks to pass through to TextBox
            ClipToBounds = true; // Don't draw outside our bounds
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var presenter = _owner.GetTextPresenter();
            if (presenter?.TextLayout == null)
                return;

            var errors = _owner.CurrentErrors;
            if (errors.Count == 0)
                return;

            var text = _owner.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
                return;

            // Get the brush for squiggly lines
            var brush = SpellCheckService.Instance.GetSpellingErrorBrush();
            var pen = new Pen(brush, 1.5);

            // Get scroll offset if TextBox is scrollable
            var scrollViewer = FindScrollViewer();
            var scrollOffset = scrollViewer?.Offset ?? default;

            foreach (var error in errors)
            {
                if (error.StartIndex >= text.Length)
                    continue;

                var endIndex = Math.Min(error.StartIndex + error.Length, text.Length);

                // Get the bounds of the misspelled word using HitTestTextRange
                var textRects = presenter.TextLayout.HitTestTextRange(error.StartIndex, endIndex - error.StartIndex);

                foreach (var rect in textRects)
                {
                    // Calculate position relative to this overlay (which is sibling to TextPresenter)
                    // Account for scroll offset
                    var presenterPos = presenter.Bounds.Position;
                    var startX = presenterPos.X + rect.Left - scrollOffset.X;
                    var endX = presenterPos.X + rect.Right - scrollOffset.X;
                    var y = presenterPos.Y + rect.Bottom - 2 - scrollOffset.Y; // Position just below text baseline

                    // Skip if completely off-screen
                    if (endX < 0 || startX > Bounds.Width || y < 0 || y > Bounds.Height + 10)
                        continue;

                    // Draw squiggly line
                    DrawSquigglyLine(context, pen, startX, endX, y);
                }
            }
        }

        /// <summary>
        /// Draw a squiggly/wavy underline from startX to endX at vertical position y.
        /// </summary>
        private static void DrawSquigglyLine(DrawingContext context, Pen pen, double startX, double endX, double y)
        {
            const double waveHeight = 2.0;
            const double waveLength = 4.0;

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(startX, y), false);

                var x = startX;
                var up = true;

                while (x < endX)
                {
                    var nextX = Math.Min(x + waveLength / 2, endX);
                    var nextY = up ? y - waveHeight : y + waveHeight;

                    ctx.LineTo(new Point(nextX, nextY));

                    x = nextX;
                    up = !up;
                }
            }

            context.DrawGeometry(null, pen, geometry);
        }

        /// <summary>
        /// Find ScrollViewer in owner's visual tree.
        /// </summary>
        private ScrollViewer? FindScrollViewer()
        {
            var queue = new Queue<Control>();
            queue.Enqueue(_owner);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current is ScrollViewer sv && current != _owner)
                    return sv;

                foreach (var child in current.GetVisualChildren().OfType<Control>())
                    queue.Enqueue(child);
            }

            return null;
        }
    }
}
