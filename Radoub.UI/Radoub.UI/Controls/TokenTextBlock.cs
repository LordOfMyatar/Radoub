using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Radoub.Formats.Tokens;

namespace Radoub.UI.Controls
{
    /// <summary>
    /// TextBlock that renders NWN tokens with appropriate formatting.
    /// Supports standard tokens, highlight tokens, color tokens, and user-defined color tokens.
    /// </summary>
    public class TokenTextBlock : SelectableTextBlock
    {
        private TokenParser? _parser;

        /// <summary>
        /// The raw text containing tokens to display.
        /// </summary>
        public static readonly StyledProperty<string?> TokenTextProperty =
            AvaloniaProperty.Register<TokenTextBlock, string?>(nameof(TokenText));

        /// <summary>
        /// User color configuration for custom CUSTOM token colors.
        /// </summary>
        public static readonly StyledProperty<UserColorConfig?> UserColorConfigProperty =
            AvaloniaProperty.Register<TokenTextBlock, UserColorConfig?>(nameof(UserColorConfig));

        /// <summary>
        /// Color for standard tokens (e.g., FirstName, Boy/Girl).
        /// </summary>
        public static readonly StyledProperty<IBrush> StandardTokenBrushProperty =
            AvaloniaProperty.Register<TokenTextBlock, IBrush>(nameof(StandardTokenBrush),
                new SolidColorBrush(Color.Parse("#00BCD4"))); // Cyan

        /// <summary>
        /// Color for CUSTOM tokens.
        /// </summary>
        public static readonly StyledProperty<IBrush> CustomTokenBrushProperty =
            AvaloniaProperty.Register<TokenTextBlock, IBrush>(nameof(CustomTokenBrush),
                new SolidColorBrush(Color.Parse("#9C27B0"))); // Purple

        /// <summary>
        /// Color for Action highlight tokens.
        /// </summary>
        public static readonly StyledProperty<IBrush> ActionTokenBrushProperty =
            AvaloniaProperty.Register<TokenTextBlock, IBrush>(nameof(ActionTokenBrush),
                new SolidColorBrush(Color.Parse("#00FF00"))); // Green

        /// <summary>
        /// Color for Check highlight tokens.
        /// </summary>
        public static readonly StyledProperty<IBrush> CheckTokenBrushProperty =
            AvaloniaProperty.Register<TokenTextBlock, IBrush>(nameof(CheckTokenBrush),
                new SolidColorBrush(Color.Parse("#FF0000"))); // Red

        /// <summary>
        /// Color for Highlight tokens.
        /// </summary>
        public static readonly StyledProperty<IBrush> HighlightTokenBrushProperty =
            AvaloniaProperty.Register<TokenTextBlock, IBrush>(nameof(HighlightTokenBrush),
                new SolidColorBrush(Color.Parse("#0080FF"))); // Blue

        public string? TokenText
        {
            get => GetValue(TokenTextProperty);
            set => SetValue(TokenTextProperty, value);
        }

        public UserColorConfig? UserColorConfig
        {
            get => GetValue(UserColorConfigProperty);
            set => SetValue(UserColorConfigProperty, value);
        }

        public IBrush StandardTokenBrush
        {
            get => GetValue(StandardTokenBrushProperty);
            set => SetValue(StandardTokenBrushProperty, value);
        }

        public IBrush CustomTokenBrush
        {
            get => GetValue(CustomTokenBrushProperty);
            set => SetValue(CustomTokenBrushProperty, value);
        }

        public IBrush ActionTokenBrush
        {
            get => GetValue(ActionTokenBrushProperty);
            set => SetValue(ActionTokenBrushProperty, value);
        }

        public IBrush CheckTokenBrush
        {
            get => GetValue(CheckTokenBrushProperty);
            set => SetValue(CheckTokenBrushProperty, value);
        }

        public IBrush HighlightTokenBrush
        {
            get => GetValue(HighlightTokenBrushProperty);
            set => SetValue(HighlightTokenBrushProperty, value);
        }

        public TokenTextBlock()
        {
            _parser = new TokenParser();
            // Ensure Inlines collection exists
            Inlines ??= new InlineCollection();
        }

        static TokenTextBlock()
        {
            TokenTextProperty.Changed.AddClassHandler<TokenTextBlock>((x, _) => x.UpdateInlines());
            UserColorConfigProperty.Changed.AddClassHandler<TokenTextBlock>((x, _) => x.OnConfigChanged());
        }

        private void OnConfigChanged()
        {
            _parser = UserColorConfig != null
                ? new TokenParser(UserColorConfig)
                : new TokenParser();
            UpdateInlines();
        }

        private void UpdateInlines()
        {
            // Inlines may be null during initialization
            if (Inlines == null)
            {
                return;
            }

            Inlines.Clear();

            var text = TokenText;
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // Parser may be null if called before constructor
            if (_parser == null)
            {
                _parser = new TokenParser();
            }

            try
            {
                var segments = _parser.Parse(text);

                foreach (var segment in segments)
                {
                    var run = CreateRunForSegment(segment);
                    Inlines.Add(run);
                }
            }
            catch (Exception)
            {
                // Fallback to plain text if parsing fails
                Inlines.Add(new Run(text));
            }
        }

        private Run CreateRunForSegment(TokenSegment segment)
        {
            return segment switch
            {
                PlainTextSegment plain => new Run(plain.DisplayText),

                StandardToken standard => new Run(standard.DisplayText)
                {
                    Foreground = StandardTokenBrush,
                    FontStyle = FontStyle.Italic
                },

                CustomToken custom => new Run(custom.DisplayText)
                {
                    Foreground = CustomTokenBrush,
                    FontStyle = FontStyle.Italic
                },

                HighlightToken highlight => CreateHighlightRun(highlight),

                ColorToken color => new Run(color.DisplayText)
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue))
                },

                UserColorToken userColor => CreateUserColorRun(userColor),

                _ => new Run(segment.DisplayText)
            };
        }

        private Run CreateHighlightRun(HighlightToken highlight)
        {
            var brush = highlight.Type switch
            {
                HighlightType.Action => ActionTokenBrush,
                HighlightType.Check => CheckTokenBrush,
                HighlightType.Highlight => HighlightTokenBrush,
                _ => ActionTokenBrush
            };

            return new Run(highlight.DisplayText)
            {
                Foreground = brush,
                FontWeight = FontWeight.Medium
            };
        }

        private Run CreateUserColorRun(UserColorToken userColor)
        {
            IBrush brush;

            // Try to get hex color from config
            var hexColor = UserColorConfig?.GetHexColor(userColor.ColorName);
            if (!string.IsNullOrEmpty(hexColor) && Color.TryParse(hexColor, out var color))
            {
                brush = new SolidColorBrush(color);
            }
            else
            {
                // Fallback to custom token brush
                brush = CustomTokenBrush;
            }

            return new Run(userColor.DisplayText)
            {
                Foreground = brush
            };
        }
    }
}
