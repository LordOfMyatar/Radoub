using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Radoub.Formats.Tokens;
using Radoub.UI.Services;

namespace Radoub.UI.Views;

public partial class TokenInsertionWindow : Window
{
    private readonly QuickTokenService _quickTokenService;
    private QuickTokenSlot[] _quickSlots;
    private UserColorConfig? _colorConfig;
    private bool _isInitialized;

    public string? SelectedToken { get; private set; }

    public TokenInsertionWindow() : this(new QuickTokenService()) { }

    public TokenInsertionWindow(QuickTokenService quickTokenService)
    {
        _quickTokenService = quickTokenService;
        _quickSlots = _quickTokenService.Load();

        InitializeComponent();

        LoadStandardTokens();
        LoadColorTokens();
        UpdateQuickSlotDisplay();
        _isInitialized = true;
    }

    private void LoadStandardTokens()
    {
        var includedCategories = new[] { "Name", "Gender (Capitalized)", "Gender (Lowercase)", "Character" };

        foreach (var category in includedCategories)
        {
            if (!TokenDefinitions.TokensByCategory.TryGetValue(category, out var tokens))
                continue;

            // Category header
            StandardTokenList.Items.Add(new ListBoxItem
            {
                Content = category,
                FontWeight = FontWeight.Bold,
                IsEnabled = false,
                IsHitTestVisible = false
            });

            foreach (var token in tokens)
            {
                StandardTokenList.Items.Add(new ListBoxItem
                {
                    Content = $"<{token}>",
                    Tag = $"<{token}>"
                });
            }
        }
    }

    private void LoadColorTokens()
    {
        _colorConfig = UserColorConfigLoader.Load();
        ColorTokenList.Items.Clear();

        if (_colorConfig == null || _colorConfig.Colors.Count == 0)
        {
            NoColorsMessage.IsVisible = true;
            return;
        }

        NoColorsMessage.IsVisible = false;

        // Close token first
        if (!string.IsNullOrEmpty(_colorConfig.CloseToken))
        {
            var closeItem = new ListBoxItem
            {
                Content = $"Close Color  ({_colorConfig.CloseToken})",
                Tag = _colorConfig.CloseToken
            };
            ColorTokenList.Items.Add(closeItem);
        }

        // Color tokens
        foreach (var kvp in _colorConfig.Colors)
        {
            var colorName = kvp.Key;
            var token = kvp.Value;
            var hexColor = _colorConfig.GetHexColor(colorName);

            var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };

            if (hexColor != null)
            {
                try
                {
                    panel.Children.Add(new Border
                    {
                        Width = 16, Height = 16,
                        Background = SolidColorBrush.Parse(hexColor),
                        BorderBrush = Brushes.Gray,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(2),
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    });
                }
                catch
                {
                    // Invalid hex color in token-colors.json — skip the swatch
                }
            }

            panel.Children.Add(new TextBlock
            {
                Text = $"{colorName}  ({token})",
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });

            ColorTokenList.Items.Add(new ListBoxItem { Content = panel, Tag = token });
        }
    }

    private void UpdateQuickSlotDisplay()
    {
        var labels = new[] { QuickSlot1Label, QuickSlot2Label, QuickSlot3Label };
        for (int i = 0; i < 3; i++)
        {
            labels[i].Text = _quickSlots[i].Token != null
                ? $"{_quickSlots[i].Label ?? _quickSlots[i].Token}"
                : "(empty)";
        }
    }

    private (string? token, string? label) GetCurrentSelection()
    {
        if (MainTabControl.SelectedIndex == 0)
        {
            // Standard tab
            if (StandardTokenList.SelectedItem is ListBoxItem item && item.Tag is string token)
                return (token, token.Trim('<', '>'));
        }
        else
        {
            // Custom tab — check color list first, then manual input
            if (ColorTokenList.SelectedItem is ListBoxItem item && item.Tag is string token)
            {
                // Try to find a friendly label
                var label = _colorConfig?.Colors
                    .FirstOrDefault(kvp => kvp.Value == token).Key;
                if (label == null && token == _colorConfig?.CloseToken)
                    label = "Close Color";
                return (token, label ?? token);
            }

            if (!string.IsNullOrWhiteSpace(CustomNumberInput.Text)
                && int.TryParse(CustomNumberInput.Text, out var num) && num >= 0)
            {
                var customToken = $"<CUSTOM{num}>";
                return (customToken, $"CUSTOM{num}");
            }
        }

        return (null, null);
    }

    private void OnStandardTokenSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateInsertButton();
    }

    private void OnColorTokenSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        CustomNumberInput.Text = ""; // Clear manual input when selecting from list
        UpdateInsertButton();
    }

    private void OnCustomNumberChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_isInitialized) return;
        ColorTokenList.SelectedItem = null; // Clear list selection when typing
        var text = CustomNumberInput.Text?.Trim() ?? "";
        CustomPreview.Text = int.TryParse(text, out var num) && num >= 0
            ? $"<CUSTOM{num}>"
            : "";
        UpdateInsertButton();
    }

    private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        UpdateInsertButton();
    }

    private void UpdateInsertButton()
    {
        var (token, _) = GetCurrentSelection();
        InsertButton.IsEnabled = token != null;
    }

    private void OnSetSlotClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || !int.TryParse(btn.Tag?.ToString(), out var slotNum))
            return;

        var (token, label) = GetCurrentSelection();
        if (token == null) return;

        _quickSlots[slotNum - 1] = new QuickTokenSlot(slotNum, token, label);
        _quickTokenService.Save(_quickSlots);
        UpdateQuickSlotDisplay();
    }

    private void OnInsertClick(object? sender, RoutedEventArgs e)
    {
        var (token, _) = GetCurrentSelection();
        if (token != null)
        {
            SelectedToken = token;
            Close(true);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnTokenDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        var (token, _) = GetCurrentSelection();
        if (token != null)
        {
            SelectedToken = token;
            Close(true);
        }
    }
}
