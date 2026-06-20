using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace Radoub.UI.Views;

/// <summary>
/// Shared portrait browser window with race/gender filtering and preview.
/// Driven by an <see cref="IPortraitBrowserContext"/> so any tool can reuse it
/// without depending on tool-specific game-data or icon services (#2291,
/// Epic #959 UI Uniformity).
/// </summary>
public partial class PortraitBrowserWindow : Window
{
    private readonly IPortraitBrowserContext? _context;
    private readonly ListBox _portraitListBox;
    private readonly ComboBox _raceFilterComboBox;
    private readonly ComboBox _genderFilterComboBox;
    private readonly TextBox _searchBox;
    private readonly TextBlock _portraitCountLabel;
    private readonly TextBlock _selectedPortraitLabel;
    private readonly TextBlock _portraitNameLabel;
    private readonly Image _portraitPreviewImage;

    private List<PortraitEntry> _allPortraits = new();
    private List<PortraitEntry> _filteredPortraits = new();
    private PortraitEntry? _selectedPortrait;

    // Incremented on each list rebuild; in-flight async thumbnail loads from an
    // older filter state stop applying when this changes (#2291).
    private int _thumbnailGeneration;

    /// <summary>
    /// Gets the selected portrait ID, or null if cancelled.
    /// </summary>
    public ushort? SelectedPortraitId => _selectedPortrait?.Id;

    /// <summary>
    /// Number of portraits currently loaded from the context (pre-filter).
    /// Exposed for tests and host diagnostics.
    /// </summary>
    public int PortraitCount => _allPortraits.Count;

    /// <summary>
    /// Remove duplicate portraits by ResRef, keeping the first occurrence and
    /// preserving order. ResRefs are case-insensitive in Aurora. Defensive guard so
    /// no context can reintroduce the duplicate-portrait bug (#2329).
    /// </summary>
    public static IEnumerable<PortraitEntry> DedupeByResRef(IEnumerable<PortraitEntry> portraits)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var portrait in portraits)
        {
            if (seen.Add(portrait.ResRef))
                yield return portrait;
        }
    }

    /// <summary>
    /// Parameterless constructor for XAML designer.
    /// </summary>
    public PortraitBrowserWindow()
    {
        InitializeComponent();
        _portraitListBox = this.FindControl<ListBox>("PortraitListBox")!;
        _raceFilterComboBox = this.FindControl<ComboBox>("RaceFilterComboBox")!;
        _genderFilterComboBox = this.FindControl<ComboBox>("GenderFilterComboBox")!;
        _searchBox = this.FindControl<TextBox>("SearchBox")!;
        _portraitCountLabel = this.FindControl<TextBlock>("PortraitCountLabel")!;
        _selectedPortraitLabel = this.FindControl<TextBlock>("SelectedPortraitLabel")!;
        _portraitNameLabel = this.FindControl<TextBlock>("PortraitNameLabel")!;
        _portraitPreviewImage = this.FindControl<Image>("PortraitPreviewImage")!;
    }

    private PortraitBrowserWindow(IPortraitBrowserContext context) : this()
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        InitializeFilters();
        LoadPortraits();
    }

    /// <summary>
    /// Creates a new portrait browser window driven by the given context.
    /// </summary>
    /// <param name="context">Tool-provided portrait data source.</param>
    public static PortraitBrowserWindow Create(IPortraitBrowserContext context)
        => new PortraitBrowserWindow(context);

    /// <summary>
    /// Pre-selects the race and gender filters before the window is shown.
    /// Call after construction, before ShowDialog.
    /// </summary>
    /// <param name="raceId">Race ID to filter by, or -1 for all races</param>
    /// <param name="gender">0=Male, 1=Female, or -1 for all</param>
    public void SetInitialFilters(int raceId, int gender)
    {
        // Find and select the matching race filter item
        if (raceId >= 0)
        {
            for (int i = 0; i < _raceFilterComboBox.Items.Count; i++)
            {
                if (_raceFilterComboBox.Items[i] is ComboBoxItem item && item.Tag is int tag && tag == raceId)
                {
                    _raceFilterComboBox.SelectedIndex = i;
                    break;
                }
            }
        }

        // Find and select the matching gender filter item
        if (gender >= 0)
        {
            for (int i = 0; i < _genderFilterComboBox.Items.Count; i++)
            {
                if (_genderFilterComboBox.Items[i] is ComboBoxItem item && item.Tag is int tag && tag == gender)
                {
                    _genderFilterComboBox.SelectedIndex = i;
                    break;
                }
            }
        }

        // Refresh the list with new filters
        UpdatePortraitList();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeFilters()
    {
        // Race filter - populated from unique race values in portraits.2da
        _raceFilterComboBox.Items.Clear();
        _raceFilterComboBox.Items.Add(new ComboBoxItem { Content = "All Races", Tag = -1 });

        // Gender filter
        _genderFilterComboBox.Items.Clear();
        _genderFilterComboBox.Items.Add(new ComboBoxItem { Content = "All", Tag = -1 });
        _genderFilterComboBox.Items.Add(new ComboBoxItem { Content = "Male", Tag = 0 });
        _genderFilterComboBox.Items.Add(new ComboBoxItem { Content = "Female", Tag = 1 });

        _raceFilterComboBox.SelectedIndex = 0;
        _genderFilterComboBox.SelectedIndex = 0;
    }

    private void LoadPortraits()
    {
        if (_context == null)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "PortraitBrowser: context is null");
            return;
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, "PortraitBrowser: Loading portraits from context");
        // Defensive dedupe (#2329): contexts should already list each portrait once,
        // but guard here so no context can reintroduce duplicate ResRefs into the list.
        _allPortraits = DedupeByResRef(_context.ListPortraits()).ToList();

        var races = new HashSet<int>();
        foreach (var portrait in _allPortraits)
        {
            if (portrait.Race >= 0)
                races.Add(portrait.Race);
        }

        // Populate race filter with found races
        foreach (var raceId in races.OrderBy(r => r))
        {
            var raceName = _context.GetRaceName(raceId);
            _raceFilterComboBox.Items.Add(new ComboBoxItem { Content = raceName, Tag = raceId });
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"PortraitBrowser: Loaded {_allPortraits.Count} portraits from context");
        UpdatePortraitList();
    }

    private void UpdatePortraitList()
    {
        _portraitListBox.Items.Clear();

        // Get filter values
        int raceFilter = -1;
        int genderFilter = -1;

        if (_raceFilterComboBox.SelectedItem is ComboBoxItem raceItem && raceItem.Tag is int race)
            raceFilter = race;

        if (_genderFilterComboBox.SelectedItem is ComboBoxItem genderItem && genderItem.Tag is int gender)
            genderFilter = gender;

        var searchText = _searchBox?.Text?.ToLowerInvariant() ?? "";

        // Filter portraits
        _filteredPortraits = _allPortraits
            .Where(p =>
            {
                if (raceFilter >= 0 && p.Race != raceFilter)
                    return false;

                if (genderFilter >= 0 && p.Sex != genderFilter)
                    return false;

                if (!string.IsNullOrEmpty(searchText) &&
                    !p.ResRef.ToLowerInvariant().Contains(searchText))
                    return false;

                return true;
            })
            .OrderBy(p => p.ResRef)
            .ToList();

        // Populate with mini icon items. Thumbnails are decoded lazily off the
        // UI thread (LoadThumbnailsAsync) so the dialog opens instantly even with
        // hundreds of portraits (#2291) — decoding inline here blocked on open.
        var pending = new List<(PortraitEntry Portrait, Image Image)>(_filteredPortraits.Count);
        foreach (var portrait in _filteredPortraits)
        {
            var item = CreatePortraitItem(portrait, out var image);
            _portraitListBox.Items.Add(item);
            pending.Add((portrait, image));
        }

        _portraitCountLabel.Text = $"{_filteredPortraits.Count} portrait{(_filteredPortraits.Count == 1 ? "" : "s")}";

        // Bump the generation so any in-flight load from a previous filter state
        // stops applying stale bitmaps to recycled images.
        int generation = ++_thumbnailGeneration;
        _ = LoadThumbnailsAsync(pending, generation);
    }

    /// <summary>
    /// Decodes portrait thumbnails on a background thread and applies them to the
    /// list images on the UI thread. Cancels itself if a newer filter pass has
    /// started (generation mismatch).
    /// </summary>
    private async Task LoadThumbnailsAsync(List<(PortraitEntry Portrait, Image Image)> pending, int generation)
    {
        if (_context == null)
            return;

        foreach (var (portrait, image) in pending)
        {
            if (generation != _thumbnailGeneration)
                return; // a newer filter pass superseded this one

            Bitmap? bitmap = null;
            try
            {
                bitmap = await Task.Run(() => _context.GetPortraitBitmap(portrait.ResRef));
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Failed to load thumbnail for {portrait.ResRef}: {ex.Message}");
            }

            if (bitmap == null || generation != _thumbnailGeneration)
                continue;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (generation == _thumbnailGeneration)
                    image.Source = bitmap;
            });
        }
    }

    private ListBoxItem CreatePortraitItem(PortraitEntry portrait, out Image image)
    {
        // Create mini icon (roughly 50% size = ~32x40 for typical NWN portraits).
        // Source is left null here and filled in by LoadThumbnailsAsync.
        const int thumbnailWidth = 32;
        const int thumbnailHeight = 40;

        image = new Image
        {
            Width = thumbnailWidth,
            Height = thumbnailHeight,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Wrap image in a border for visual feedback
        var border = new Border
        {
            Child = image,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(2),
            Margin = new Thickness(2)
        };
        ToolTip.SetTip(border, portrait.ResRef);

        return new ListBoxItem
        {
            Content = border,
            Tag = portrait,
            Padding = new Thickness(1)
        };
    }

    private void OnFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdatePortraitList();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdatePortraitList();
    }

    private void OnClearSearchClick(object? sender, RoutedEventArgs e)
    {
        _searchBox.Text = "";
    }

    private void OnPortraitSelected(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (_portraitListBox.SelectedItem is ListBoxItem item && item.Tag is PortraitEntry portrait)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"PortraitBrowser: Selected portrait ID={portrait.Id} ResRef='{portrait.ResRef}'");
                _selectedPortrait = portrait;
                _selectedPortraitLabel.Text = portrait.ResRef;
                _portraitNameLabel.Text = portrait.ResRef;

                // Load portrait preview
                try
                {
                    var bitmap = _context?.GetPortraitBitmap(portrait.ResRef);
                    _portraitPreviewImage.Source = bitmap;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"PortraitBrowser: Preview loaded: {(bitmap != null ? "success" : "null")}");
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load portrait preview: {ex.Message}");
                    _portraitPreviewImage.Source = null;
                }
            }
            else
            {
                _selectedPortrait = null;
                _selectedPortraitLabel.Text = "(none)";
                _portraitNameLabel.Text = "";
                _portraitPreviewImage.Source = null;
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"PortraitBrowser: OnPortraitSelected crashed: {ex}");
        }
    }

    private void OnPortraitDoubleClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_selectedPortrait != null)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"PortraitBrowser: Double-clicked portrait ID={_selectedPortrait.Id}");
                Close(_selectedPortrait.Id);
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"PortraitBrowser: OnPortraitDoubleClicked crashed: {ex}");
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_selectedPortrait != null)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"PortraitBrowser: OK clicked, returning portrait ID={_selectedPortrait.Id}");
                Close(_selectedPortrait.Id);
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "PortraitBrowser: OK clicked with no selection");
                Close(null);
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"PortraitBrowser: OnOkClick crashed: {ex}");
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        UnifiedLogger.LogApplication(LogLevel.DEBUG, "PortraitBrowser: Cancel clicked");
        Close(null);
    }

    #region Title Bar Events

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    #endregion
}
