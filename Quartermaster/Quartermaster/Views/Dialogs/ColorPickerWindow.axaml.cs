using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Quartermaster.Services;

namespace Quartermaster.Views.Dialogs;

public partial class ColorPickerWindow : Window
{
    private readonly ItemsControl _colorGrid;
    private readonly Border _currentColorSwatch;
    private readonly TextBlock _currentColorLabel;
    private readonly TextBlock _titleLabel;
    private readonly PaletteColorService _paletteColorService;
    private readonly string _paletteName;
    private readonly List<Border> _colorSwatches = new();
    private Border? _selectedSwatch;

    public bool Confirmed { get; private set; }
    public byte SelectedColorIndex { get; private set; }

    public ColorPickerWindow() : this(null!, "", 0)
    {
    }

    public ColorPickerWindow(PaletteColorService paletteColorService, string paletteName, byte currentColorIndex)
    {
        InitializeComponent();

        _paletteColorService = paletteColorService;
        _paletteName = paletteName;
        SelectedColorIndex = currentColorIndex;

        _colorGrid = this.FindControl<ItemsControl>("ColorGrid")!;
        _currentColorSwatch = this.FindControl<Border>("CurrentColorSwatch")!;
        _currentColorLabel = this.FindControl<TextBlock>("CurrentColorLabel")!;
        _titleLabel = this.FindControl<TextBlock>("TitleLabel")!;

        // Set title based on palette type
        _titleLabel.Text = GetPaletteDisplayName(paletteName);

        // Build the color grid
        BuildColorGrid();

        // Select the current color
        SelectColor(currentColorIndex);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private string GetPaletteDisplayName(string paletteName)
    {
        return paletteName switch
        {
            PaletteColorService.Palettes.Skin => "Select Skin Color",
            PaletteColorService.Palettes.Hair => "Select Hair Color",
            PaletteColorService.Palettes.Tattoo1 or PaletteColorService.Palettes.Tattoo2 => "Select Tattoo Color",
            _ => "Select Color"
        };
    }

    private void BuildColorGrid()
    {
        var items = new List<Border>();

        // NWN palettes have 176 color rows (0-175)
        // Layout: 16 columns x 11 rows = 176 swatches (matches Aurora Toolset)
        // Each swatch shows a gradient from dark to light
        for (byte i = 0; i < 176; i++)
        {
            IBrush background;
            if (_paletteColorService != null)
            {
                background = _paletteColorService.CreateGradientBrush(_paletteName, i);
            }
            else
            {
                background = GetBorderBrush();
            }

            var swatch = new Border
            {
                Width = 26,   // Square-ish swatches like Aurora Toolset
                Height = 26,
                Margin = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                BorderThickness = new Thickness(1),
                BorderBrush = GetBorderBrush(),
                Background = background,
                Tag = i,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            swatch.PointerPressed += OnSwatchClicked;
            _colorSwatches.Add(swatch);
            items.Add(swatch);
        }

        _colorGrid.ItemsSource = items;
    }

    private void OnSwatchClicked(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border swatch && swatch.Tag is byte index)
        {
            SelectColor(index);

            // Double-click confirms
            if (e.ClickCount == 2)
            {
                Confirmed = true;
                Close();
            }
        }
    }

    private void SelectColor(byte index)
    {
        SelectedColorIndex = index;

        // Update current color display with gradient
        if (_paletteColorService != null)
        {
            _currentColorSwatch.Background = _paletteColorService.CreateGradientBrush(_paletteName, index);
        }
        else
        {
            _currentColorSwatch.Background = GetBorderBrush();
        }
        _currentColorLabel.Text = $"Index: {index}";

        // Update selection highlight
        if (_selectedSwatch != null)
        {
            _selectedSwatch.BorderBrush = GetBorderBrush();
            _selectedSwatch.BorderThickness = new Thickness(1);
        }

        if (index < _colorSwatches.Count)
        {
            _selectedSwatch = _colorSwatches[index];
            _selectedSwatch.BorderBrush = GetSelectionBrush();
            _selectedSwatch.BorderThickness = new Thickness(2);
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    #region Theme-Aware Colors

    private static readonly IBrush DefaultBorderBrush = new SolidColorBrush(Color.Parse("#757575"));
    private static readonly IBrush DefaultSelectionBrush = new SolidColorBrush(Color.Parse("#FFC107"));

    private IBrush GetBorderBrush()
    {
        var app = Application.Current;
        if (app?.Resources.TryGetResource("ThemeBorder", ThemeVariant.Default, out var brush) == true
            && brush is IBrush b)
            return b;
        return DefaultBorderBrush;
    }

    private IBrush GetSelectionBrush()
    {
        var app = Application.Current;
        if (app?.Resources.TryGetResource("ThemeAccent", ThemeVariant.Default, out var brush) == true
            && brush is IBrush b)
            return b;
        return DefaultSelectionBrush;
    }

    #endregion
}
