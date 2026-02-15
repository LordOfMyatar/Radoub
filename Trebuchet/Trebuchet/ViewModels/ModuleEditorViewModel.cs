using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Radoub.Formats.Ifo;
using Radoub.Formats.Services;

namespace RadoubLauncher.ViewModels;

/// <summary>
/// ViewModel for the Module Editor window.
/// Allows editing IFO (module.ifo) files.
/// </summary>
public partial class ModuleEditorViewModel : ObservableObject
{
    private Window? _parentWindow;
    private string? _modulePath;
    private string? _modFilePath;
    private string? _workingDirectoryPath;
    private IfoFile? _ifoFile;
    private bool _isFromModFile;
    private bool _isReadOnly;
    private IGameDataService? _gameDataService;

    /// <summary>
    /// Raised when a new variable is added, to allow the View to auto-focus the name field.
    /// </summary>
    public event EventHandler? VariableAdded;

    // Status

    [ObservableProperty]
    private string _statusText = "No module loaded";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(CanUnpack))]
    [NotifyCanExecuteChangedFor(nameof(UnpackCommand))]
    private bool _isModuleLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(CanUnpack))]
    [NotifyCanExecuteChangedFor(nameof(UnpackCommand))]
    private bool _isModuleReadOnly;

    public bool CanSave => IsModuleLoaded && !IsModuleReadOnly && HasUnsavedChanges;

    /// <summary>
    /// Can unpack when loaded from .mod file (read-only state indicates packed module).
    /// </summary>
    public bool CanUnpack => IsModuleLoaded && IsModuleReadOnly && !string.IsNullOrEmpty(_modFilePath);

    // Module Metadata

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _moduleName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _moduleDescription = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _moduleTag = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _customTlk = string.Empty;

    // Version/Requirements

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _minGameVersion = "1.69";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VersionButtonText))]
    [NotifyPropertyChangedFor(nameof(VersionWarningText))]
    private bool _isVersionUnlocked;

    /// <summary>
    /// Text for the version edit/lock button.
    /// </summary>
    public string VersionButtonText => IsVersionUnlocked ? "Lock" : "Edit Version...";

    /// <summary>
    /// Warning text shown below the version field.
    /// </summary>
    public string VersionWarningText => IsVersionUnlocked
        ? "Select target version. Lower versions may not support all module features."
        : "1.69 = Universal compatibility. Click 'Edit Version...' to change.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private bool _requiresSoU;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private bool _requiresHotU;

    // HAK List

    [ObservableProperty]
    private ObservableCollection<string> _hakList = new();

    [ObservableProperty]
    private string? _selectedHak;

    [ObservableProperty]
    private string _newHakName = string.Empty;

    // Time Settings

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private byte _dawnHour = 6;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private byte _duskHour = 18;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private byte _minutesPerHour = 2;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private uint _startYear = 1372;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private byte _startMonth = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private byte _startDay = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private byte _startHour = 13;

    // Entry Point

    [ObservableProperty]
    private ObservableCollection<string> _areaList = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _entryArea = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private float _entryX;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private float _entryY;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private float _entryZ;

    // Scripts

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onModuleLoad = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onClientEnter = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onClientLeave = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onHeartbeat = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onAcquireItem = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onActivateItem = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onUnacquireItem = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onPlayerDeath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onPlayerDying = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onPlayerRest = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onPlayerEquipItem = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onPlayerUnequipItem = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onPlayerLevelUp = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onUserDefined = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onSpawnButtonDown = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onCutsceneAbort = string.Empty;

    // NWN:EE Extended Scripts

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onModuleStart = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onPlayerChat = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onPlayerTarget = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onPlayerGuiEvent = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onPlayerTileAction = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _onNuiEvent = string.Empty;

    // Other Settings

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private byte _xpScale = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _defaultBic = string.Empty;

    /// <summary>
    /// Whether a default character (BIC) is enabled for this module.
    /// When enabled, Load Module is disabled (only Test Module works).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    [NotifyPropertyChangedFor(nameof(IsDefaultBicDropdownEnabled))]
    private bool _useDefaultBic;

    /// <summary>
    /// Available BIC files found in the module's working directory.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _availableBicFiles = new();

    /// <summary>
    /// Whether the DefaultBic dropdown should be enabled.
    /// </summary>
    public bool IsDefaultBicDropdownEnabled => UseDefaultBic && AvailableBicFiles.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private string _startMovie = string.Empty;

    // Variables

    [ObservableProperty]
    private ObservableCollection<VariableViewModel> _variables = new();

    [ObservableProperty]
    private VariableViewModel? _selectedVariable;

    // Version dropdown options
    public static ObservableCollection<string> GameVersions { get; } = new()
    {
        "1.69",  // Diamond Edition - universal compatibility
        "1.79",  // 64-bit binaries
        "1.80",  // March 2020 stable
        "1.81",  // New lighting engine
        "1.83",
        "1.85",
        "1.87",
        "1.89"   // Current stable
    };

    public ModuleEditorViewModel()
    {
        // Initialize GameDataService for script browser (built-in scripts from game BIFs)
        _ = InitializeGameDataServiceAsync();
    }
}
