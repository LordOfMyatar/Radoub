using Avalonia.Controls;
using ItemEditor.Services;
using ItemEditor.ViewModels;
using Radoub.Formats.Logging;
using Radoub.Formats.Search;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.Formats.Uti;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using Radoub.UI.Services.Search;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ItemEditor.Views;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private UtiFile? _currentItem;
    private ItemViewModel? _itemViewModel;
    private readonly DocumentState _documentState = new("Relique");
    private IGameDataService? _gameDataService;
    private List<BaseItemTypeInfo>? _baseItemTypes;
    private List<PaletteCategory> _paletteCategories = new();
    private ItemPropertyService? _itemPropertyService;
    private ItemStatisticsService? _itemStatisticsService;
    private PropertyTypeInfo? _selectedPropertyType;
    private int _editingPropertyIndex = -1; // -1 = add mode, >= 0 = editing that index
    private readonly HashSet<int> _checkedPropertyIndices = new();
    private readonly ObservableCollection<VariableViewModel> _variables = new();
    private ItemIconService? _itemIconService;
    private PaletteColorService? _paletteColorService;

    // Convenience accessors for document state
    private string? _currentFilePath
    {
        get => _documentState.CurrentFilePath;
        set => _documentState.CurrentFilePath = value;
    }
    private bool _isDirty => _documentState.IsDirty;
    private bool _isLoading
    {
        get => _documentState.IsLoading;
        set => _documentState.IsLoading = value;
    }

    public bool HasFile => _currentItem != null;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();

        // Wire up shared document state for title bar updates
        _documentState.DirtyStateChanged += () => Title = _documentState.GetTitle();

        // Listen for module changes from Trebuchet (#1802)
        RadoubSettings.Instance.PropertyChanged += OnRadoubSettingsChanged;

        RestoreWindowPosition();
        UpdateModuleIndicator();
        PopulateRecentFiles();

        // Initialize search bar with UTI search provider
        var searchBar = this.FindControl<SearchBar>("FileSearchBar");
        if (searchBar != null)
        {
            searchBar.Initialize(
                new FileSearchService(new UtiSearchProvider()),
                new (string, SearchFieldCategory)[]
                {
                    ("Text", SearchFieldCategory.Content),
                    ("Tags", SearchFieldCategory.Identity),
                    ("Metadata", SearchFieldCategory.Metadata),
                });
            searchBar.FileModified += OnSearchFileModified;
            searchBar.NavigateToMatch += OnSearchNavigateToMatch;
        }

        Closing += OnWindowClosing;
        Opened += OnWindowOpened;

        UnifiedLogger.LogApplication(LogLevel.INFO, "Relique MainWindow initialized");
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void MarkDirty()
    {
        _documentState.MarkDirty();
    }

    /// <summary>
    /// Explicitly updates the title bar from document state.
    /// Called defensively — ClearDirty() only fires DirtyStateChanged on dirty→clean transitions,
    /// so callers that change file path on a clean document must call this explicitly.
    /// </summary>
    private void UpdateTitle()
    {
        Title = _documentState.GetTitle();
    }
}

internal enum SavePromptResult
{
    Save,
    DontSave,
    Cancel
}
