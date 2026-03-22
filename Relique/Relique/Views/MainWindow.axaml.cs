using Avalonia.Controls;
using ItemEditor.Services;
using ItemEditor.ViewModels;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;
using Radoub.UI.Services;
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

        RestoreWindowPosition();
        UpdateUseRadoubThemeMenuState();
        UpdateModuleIndicator();
        PopulateRecentFiles();

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
}

internal enum SavePromptResult
{
    Save,
    DontSave,
    Cancel
}
