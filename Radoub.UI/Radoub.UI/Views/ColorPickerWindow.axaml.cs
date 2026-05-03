using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Radoub.UI.Services;

namespace Radoub.UI.Views;

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

    /// <summary>
    /// Designer-only constructor. Do not use at runtime.
    /// </summary>
    [Obsolete("Designer use only", error: true)]
    public ColorPickerWindow() => throw new NotSupportedException("Use parameterized constructor");

    /// <param name="paletteName">Palette TGA file name (e.g. "pal_cloth01"). Per the
    /// Aurora item format spec, both "1" and "2" slots for a material share one palette,
    /// so this no longer uniquely identifies the slot — pass <paramref name="dialogTitle"/>
    /// for slot-specific titling.</param>
    /// <param name="dialogTitle">User-facing dialog title (e.g. "Select Cloth 2 Color").</param>
    public ColorPickerWindow(
        PaletteColorService paletteColorService,
        string paletteName,
        byte currentColorIndex,
        string? dialogTitle = null)
    {
        InitializeComponent();

        _paletteColorService = paletteColorService;
        _paletteName = paletteName;
        SelectedColorIndex = currentColorIndex;

        _colorGrid = this.FindControl<ItemsControl>("ColorGrid")!;
        _currentColorSwatch = this.FindControl<Border>("CurrentColorSwatch")!;
        _currentColorLabel = this.FindControl<TextBlock>("CurrentColorLabel")!;
        _titleLabel = this.FindControl<TextBlock>("TitleLabel")!;

        _titleLabel.Text = dialogTitle ?? GetPaletteDisplayName(paletteName);

        BuildColorGrid();
        SelectColor(currentColorIndex);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Fallback title for callers that don't pass an explicit dialogTitle. Cannot
    /// distinguish "1" from "2" slots since they share a palette file — callers should
    /// pass <c>dialogTitle</c> for slot-specific labels.
    /// </summary>
    private static string GetPaletteDisplayName(string paletteName)
    {
        return paletteName switch
        {
            PaletteColorService.Palettes.Skin => "Select Skin Color",
            PaletteColorService.Palettes.Hair => "Select Hair Color",
            PaletteColorService.Palettes.Tattoo1 => "Select Tattoo Color",
            PaletteColorService.Palettes.Cloth1 => "Select Cloth Color",
            PaletteColorService.Palettes.Leather1 => "Select Leather Color",
            PaletteColorService.Palettes.Metal1 => "Select Metal Color",
            _ => "Select Color"
        };
    }

    private void BuildColorGrid()
    {
        var items = new List<Border>();

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
                Width = 26,
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

        if (_paletteColorService != null)
        {
            _currentColorSwatch.Background = _paletteColorService.CreateGradientBrush(_paletteName, index);
        }
        else
        {
            _currentColorSwatch.Background = GetBorderBrush();
        }
        _currentColorLabel.Text = $"Index: {index}";

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

    private IBrush GetBorderBrush() =>
        BrushManager.GetDisabledBrush(this);

    private IBrush GetSelectionBrush() =>
        BrushManager.GetWarningBrush(this);
}
