using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;

namespace Quartermaster.Views;

/// <summary>
/// Portrait browser window with race/gender filtering and preview.
/// </summary>
public partial class PortraitBrowserWindow : Window
{
    private readonly IGameDataService? _gameDataService;
    private readonly ItemIconService? _itemIconService;
    private readonly ListBox _portraitListBox;
    private readonly ComboBox _raceFilterComboBox;
    private readonly ComboBox _genderFilterComboBox;
    private readonly TextBox _searchBox;
    private readonly TextBlock _portraitCountLabel;
    private readonly TextBlock _selectedPortraitLabel;
    private readonly TextBlock _portraitNameLabel;
    private readonly Image _portraitPreviewImage;

    private List<PortraitInfo> _allPortraits = new();
    private List<PortraitInfo> _filteredPortraits = new();
    private PortraitInfo? _selectedPortrait;

    /// <summary>
    /// Gets the selected portrait ID, or null if cancelled.
    /// </summary>
    public ushort? SelectedPortraitId => _selectedPortrait?.Id;

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

    /// <summary>
    /// Creates a new portrait browser window.
    /// </summary>
    /// <param name="gameDataService">Game data service for 2DA lookups</param>
    /// <param name="itemIconService">Item icon service for loading portrait images</param>
    public PortraitBrowserWindow(IGameDataService gameDataService, ItemIconService itemIconService) : this()
    {
        _gameDataService = gameDataService ?? throw new ArgumentNullException(nameof(gameDataService));
        _itemIconService = itemIconService ?? throw new ArgumentNullException(nameof(itemIconService));

        InitializeFilters();
        LoadPortraits();
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
        if (_gameDataService == null) return;

        _allPortraits.Clear();
        var races = new HashSet<int>();

        // Load portraits from portraits.2da
        for (int i = 0; i < 500; i++)
        {
            var baseResRef = _gameDataService.Get2DAValue("portraits", i, "BaseResRef");
            if (string.IsNullOrEmpty(baseResRef) || baseResRef == "****")
            {
                if (_allPortraits.Count > 50)
                    break;
                continue;
            }

            // Get race and sex columns
            var raceStr = _gameDataService.Get2DAValue("portraits", i, "Race");
            var sexStr = _gameDataService.Get2DAValue("portraits", i, "Sex");

            int race = -1;
            int sex = -1;

            if (!string.IsNullOrEmpty(raceStr) && raceStr != "****")
                int.TryParse(raceStr, out race);

            if (!string.IsNullOrEmpty(sexStr) && sexStr != "****")
                int.TryParse(sexStr, out sex);

            if (race >= 0)
                races.Add(race);

            _allPortraits.Add(new PortraitInfo
            {
                Id = (ushort)i,
                ResRef = baseResRef,
                Race = race,
                Sex = sex
            });
        }

        // Populate race filter with found races
        foreach (var raceId in races.OrderBy(r => r))
        {
            var raceName = GetRaceName(raceId);
            _raceFilterComboBox.Items.Add(new ComboBoxItem { Content = raceName, Tag = raceId });
        }

        UpdatePortraitList();
    }

    private string GetRaceName(int raceId)
    {
        if (raceId < 0 || _gameDataService == null)
            return "Unknown";

        var strRef = _gameDataService.Get2DAValue("racialtypes", raceId, "Name");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****" && uint.TryParse(strRef, out var tlkRef))
        {
            var name = _gameDataService.GetString(tlkRef);
            if (!string.IsNullOrEmpty(name))
                return name;
        }

        // Fallback names for common races
        return raceId switch
        {
            0 => "Dwarf",
            1 => "Elf",
            2 => "Gnome",
            3 => "Halfling",
            4 => "Half-Elf",
            5 => "Half-Orc",
            6 => "Human",
            _ => $"Race {raceId}"
        };
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

        // Populate list
        foreach (var portrait in _filteredPortraits)
        {
            _portraitListBox.Items.Add(new ListBoxItem
            {
                Content = portrait.ResRef,
                Tag = portrait
            });
        }

        _portraitCountLabel.Text = $"{_filteredPortraits.Count} portrait{(_filteredPortraits.Count == 1 ? "" : "s")}";
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
        if (_portraitListBox.SelectedItem is ListBoxItem item && item.Tag is PortraitInfo portrait)
        {
            _selectedPortrait = portrait;
            _selectedPortraitLabel.Text = portrait.ResRef;
            _portraitNameLabel.Text = portrait.ResRef;

            // Load portrait preview
            if (_itemIconService != null)
            {
                try
                {
                    var bitmap = _itemIconService.GetPortrait(portrait.ResRef);
                    _portraitPreviewImage.Source = bitmap;
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load portrait preview: {ex.Message}");
                    _portraitPreviewImage.Source = null;
                }
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

    private void OnPortraitDoubleClicked(object? sender, RoutedEventArgs e)
    {
        if (_selectedPortrait != null)
        {
            Close(_selectedPortrait.Id);
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedPortrait != null)
        {
            Close(_selectedPortrait.Id);
        }
        else
        {
            Close(null);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    /// <summary>
    /// Internal class to hold portrait information.
    /// </summary>
    private class PortraitInfo
    {
        public ushort Id { get; set; }
        public string ResRef { get; set; } = "";
        public int Race { get; set; } = -1;
        public int Sex { get; set; } = -1;
    }
}
