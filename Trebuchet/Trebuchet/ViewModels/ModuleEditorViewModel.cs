using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Gff;
using Radoub.Formats.Ifo;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.UI.Views;
using RadoubLauncher.Services;

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

    private async Task InitializeGameDataServiceAsync()
    {
        try
        {
            _gameDataService = await Task.Run(() => new GameDataService());
            if (_gameDataService.IsConfigured)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "ModuleEditor: GameDataService initialized");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "ModuleEditor: GameDataService not configured (no game path)");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"ModuleEditor: Failed to initialize GameDataService: {ex.Message}");
        }
    }

    public void SetParentWindow(Window window)
    {
        _parentWindow = window;
    }

    public async Task LoadCurrentModuleAsync()
    {
        var currentPath = RadoubSettings.Instance.CurrentModulePath;
        if (string.IsNullOrEmpty(currentPath))
        {
            StatusText = "No module selected. Open a module in Trebuchet first.";
            return;
        }

        await LoadModuleAsync(currentPath);
    }

    public async Task LoadModuleAsync(string path)
    {
        IsLoading = true;
        StatusText = "Loading module...";

        try
        {
            _modulePath = path;
            _modFilePath = null;
            _workingDirectoryPath = null;
            _isFromModFile = false;
            _isReadOnly = false;

            // Check if path is a .mod file or a directory
            if (File.Exists(path) && path.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
            {
                // Check for unpacked working directory
                var moduleName = Path.GetFileNameWithoutExtension(path);
                var moduleDir = Path.GetDirectoryName(path);
                var workingDir = FindWorkingDirectory(moduleDir, moduleName);

                if (workingDir != null)
                {
                    // Unpacked directory exists - load from there (editable)
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Found unpacked module directory: {UnifiedLogger.SanitizePath(workingDir)}");
                    _workingDirectoryPath = workingDir;
                    _modFilePath = path;
                    _modulePath = workingDir;  // Save to working directory, not .mod file
                    await LoadFromDirectoryAsync(workingDir);
                    _isReadOnly = false;
                }
                else
                {
                    // No unpacked directory - load from .mod file (read-only)
                    await LoadFromModFileAsync(path);
                    _isReadOnly = true;
                }
            }
            else if (Directory.Exists(path))
            {
                await LoadFromDirectoryAsync(path);
                _isReadOnly = false;
            }
            else
            {
                StatusText = "Invalid module path";
                return;
            }

            PopulateViewModelFromIfo();
            SyncToRadoubSettings();
            SyncDefaultBicToSettings();
            IsModuleLoaded = true;
            IsModuleReadOnly = _isReadOnly;
            IsVersionUnlocked = false;  // Always start with version locked
            HasUnsavedChanges = false;

            var statusSuffix = _isReadOnly ? " (Read-only - module not unpacked)" : "";
            StatusText = $"Loaded: {Path.GetFileName(_modulePath)}{statusSuffix}";
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load module: {ex.Message}");
            StatusText = $"Error: {ex.Message}";
            IsModuleLoaded = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Find the unpacked working directory for a module.
    /// NWN toolset uses temp0, temp01, or the module name as working directory.
    /// </summary>
    private static string? FindWorkingDirectory(string? moduleDir, string moduleName)
    {
        if (string.IsNullOrEmpty(moduleDir))
            return null;

        // Check in priority order:
        // 1. Module name folder (e.g., "MyModule/")
        // 2. temp0 folder (NWN toolset default)
        // 3. temp1 folder (alternate toolset folder)
        var candidates = new[]
        {
            Path.Combine(moduleDir, moduleName),
            Path.Combine(moduleDir, "temp0"),
            Path.Combine(moduleDir, "temp1")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate) &&
                File.Exists(Path.Combine(candidate, "module.ifo")))
            {
                return candidate;
            }
        }

        return null;
    }

    private async Task LoadFromModFileAsync(string modPath)
    {
        await Task.Run(() =>
        {
            var erf = ErfReader.ReadMetadataOnly(modPath);
            var ifoEntry = erf.FindResource("module", ResourceTypes.Ifo);

            if (ifoEntry == null)
            {
                throw new InvalidDataException("module.ifo not found in MOD file");
            }

            var ifoData = ErfReader.ExtractResource(modPath, ifoEntry);
            _ifoFile = IfoReader.Read(ifoData);
            _modFilePath = modPath;
            _isFromModFile = true;
        });
    }

    private async Task LoadFromDirectoryAsync(string dirPath)
    {
        await Task.Run(() =>
        {
            var ifoPath = Path.Combine(dirPath, "module.ifo");
            if (!File.Exists(ifoPath))
            {
                throw new FileNotFoundException("module.ifo not found in directory");
            }

            _ifoFile = IfoReader.Read(ifoPath);
            _isFromModFile = false;
        });
    }

    private void PopulateViewModelFromIfo()
    {
        if (_ifoFile == null) return;

        // Metadata
        ModuleName = _ifoFile.ModuleName.GetString();
        ModuleDescription = _ifoFile.ModuleDescription.GetString();
        ModuleTag = _ifoFile.Tag;
        CustomTlk = _ifoFile.CustomTlk;

        // Version/Requirements
        MinGameVersion = _ifoFile.MinGameVersion;
        RequiresSoU = (_ifoFile.ExpansionPack & (ushort)ExpansionPackFlags.ShadowsOfUndrentide) != 0;
        RequiresHotU = (_ifoFile.ExpansionPack & (ushort)ExpansionPackFlags.HordesOfTheUnderdark) != 0;

        // HAK List
        HakList = new ObservableCollection<string>(_ifoFile.HakList);

        // Time Settings
        DawnHour = _ifoFile.DawnHour;
        DuskHour = _ifoFile.DuskHour;
        MinutesPerHour = _ifoFile.MinutesPerHour;
        StartYear = _ifoFile.StartYear;
        StartMonth = _ifoFile.StartMonth;
        StartDay = _ifoFile.StartDay;
        StartHour = _ifoFile.StartHour;

        // Entry Point
        AreaList = new ObservableCollection<string>(_ifoFile.AreaList);
        EntryArea = _ifoFile.EntryArea;
        EntryX = _ifoFile.EntryX;
        EntryY = _ifoFile.EntryY;
        EntryZ = _ifoFile.EntryZ;

        // Scripts
        OnModuleLoad = _ifoFile.OnModuleLoad;
        OnClientEnter = _ifoFile.OnClientEnter;
        OnClientLeave = _ifoFile.OnClientLeave;
        OnHeartbeat = _ifoFile.OnHeartbeat;
        OnAcquireItem = _ifoFile.OnAcquireItem;
        OnActivateItem = _ifoFile.OnActivateItem;
        OnUnacquireItem = _ifoFile.OnUnacquireItem;
        OnPlayerDeath = _ifoFile.OnPlayerDeath;
        OnPlayerDying = _ifoFile.OnPlayerDying;
        OnPlayerRest = _ifoFile.OnPlayerRest;
        OnPlayerEquipItem = _ifoFile.OnPlayerEquipItem;
        OnPlayerUnequipItem = _ifoFile.OnPlayerUnequipItem;
        OnPlayerLevelUp = _ifoFile.OnPlayerLevelUp;
        OnUserDefined = _ifoFile.OnUserDefined;
        OnSpawnButtonDown = _ifoFile.OnSpawnButtonDown;
        OnCutsceneAbort = _ifoFile.OnCutsceneAbort;

        // NWN:EE Extended Scripts
        OnModuleStart = _ifoFile.OnModuleStart;
        OnPlayerChat = _ifoFile.OnPlayerChat;
        OnPlayerTarget = _ifoFile.OnPlayerTarget;
        OnPlayerGuiEvent = _ifoFile.OnPlayerGuiEvent;
        OnPlayerTileAction = _ifoFile.OnPlayerTileAction;
        OnNuiEvent = _ifoFile.OnNuiEvent;

        // Other
        XpScale = _ifoFile.XPScale;
        StartMovie = _ifoFile.StartMovie;

        // Scan for available BIC files before setting DefaultBic
        ScanForBicFiles();

        // Set DefaultBic and UseDefaultBic checkbox state
        // Find matching BIC in available files (case-insensitive) to ensure ComboBox selection works
        var ifoDefaultBic = _ifoFile.DefaultBic;
        if (!string.IsNullOrEmpty(ifoDefaultBic))
        {
            var matchingBic = AvailableBicFiles.FirstOrDefault(
                b => string.Equals(b, ifoDefaultBic, StringComparison.OrdinalIgnoreCase));
            DefaultBic = matchingBic ?? ifoDefaultBic;  // Use matching case if found
            UseDefaultBic = true;
        }
        else
        {
            DefaultBic = string.Empty;
            UseDefaultBic = false;
        }

        // Variables
        Variables = new ObservableCollection<VariableViewModel>(
            _ifoFile.VarTable.Select(v =>
            {
                var vm = new VariableViewModel(v);
                vm.SetUniquenessCheck(IsVariableNameUnique);
                return vm;
            }));
    }

    /// <summary>
    /// Scan the working directory for available .bic files.
    /// </summary>
    private void ScanForBicFiles()
    {
        AvailableBicFiles.Clear();

        if (string.IsNullOrEmpty(_modulePath))
            return;

        string? searchDir = null;

        // Determine the directory to scan
        if (Directory.Exists(_modulePath))
        {
            // _modulePath is already a directory
            searchDir = _modulePath;
        }
        else if (!string.IsNullOrEmpty(_workingDirectoryPath) && Directory.Exists(_workingDirectoryPath))
        {
            // Use the unpacked working directory
            searchDir = _workingDirectoryPath;
        }

        if (string.IsNullOrEmpty(searchDir))
            return;

        try
        {
            var bicFiles = Directory.GetFiles(searchDir, "*.bic", SearchOption.TopDirectoryOnly);
            foreach (var bicFile in bicFiles.OrderBy(f => f))
            {
                var resRef = Path.GetFileNameWithoutExtension(bicFile);
                AvailableBicFiles.Add(resRef);
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Found {AvailableBicFiles.Count} BIC files in module directory");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to scan for BIC files: {ex.Message}");
        }

        OnPropertyChanged(nameof(IsDefaultBicDropdownEnabled));
    }

    /// <summary>
    /// Sync the DefaultBic setting to RadoubSettings for MainWindow to check.
    /// </summary>
    private void SyncDefaultBicToSettings()
    {
        var settings = RadoubSettings.Instance;

        // Set the shared DefaultBic property (used by MainWindow to disable Load Module)
        if (UseDefaultBic && !string.IsNullOrEmpty(DefaultBic))
        {
            settings.CurrentModuleDefaultBic = DefaultBic;
        }
        else
        {
            settings.CurrentModuleDefaultBic = string.Empty;
        }
    }

    /// <summary>
    /// Sync module's custom content settings to RadoubSettings for cross-tool use.
    /// Called after loading a module so other tools can access the custom TLK.
    /// </summary>
    private void SyncToRadoubSettings()
    {
        var settings = RadoubSettings.Instance;

        // Resolve CustomTlk name to full path
        if (!string.IsNullOrEmpty(CustomTlk))
        {
            var tlkPath = ResolveTlkPath(CustomTlk);
            if (!string.IsNullOrEmpty(tlkPath))
            {
                settings.CustomTlkPath = tlkPath;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Set shared CustomTlkPath: {UnifiedLogger.SanitizePath(tlkPath)}");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Custom TLK not found: {CustomTlk}");
                settings.CustomTlkPath = "";
            }
        }
        else
        {
            settings.CustomTlkPath = "";
        }
    }

    /// <summary>
    /// Resolve a TLK name (without extension) to a full path.
    /// Searches in the module directory, then NWN documents tlk folder.
    /// </summary>
    private string? ResolveTlkPath(string tlkName)
    {
        if (string.IsNullOrEmpty(tlkName)) return null;

        var tlkFileName = tlkName.EndsWith(".tlk", StringComparison.OrdinalIgnoreCase)
            ? tlkName
            : tlkName + ".tlk";

        // Check module directory first
        if (!string.IsNullOrEmpty(_modulePath))
        {
            var moduleDir = Directory.Exists(_modulePath) ? _modulePath : Path.GetDirectoryName(_modulePath);
            if (!string.IsNullOrEmpty(moduleDir))
            {
                var moduleTlkPath = Path.Combine(moduleDir, tlkFileName);
                if (File.Exists(moduleTlkPath))
                    return moduleTlkPath;
            }
        }

        // Check NWN documents tlk folder
        var nwnPath = RadoubSettings.Instance.NeverwinterNightsPath;
        if (!string.IsNullOrEmpty(nwnPath))
        {
            var tlkFolder = Path.Combine(nwnPath, "tlk");
            var tlkPath = Path.Combine(tlkFolder, tlkFileName);
            if (File.Exists(tlkPath))
                return tlkPath;
        }

        return null;
    }

    /// <summary>
    /// Browse for a custom TLK file.
    /// </summary>
    [RelayCommand]
    private async Task BrowseCustomTlkAsync()
    {
        if (_parentWindow == null) return;

        var storage = _parentWindow.StorageProvider;

        // Build list of suggested starting locations
        var suggestedLocations = new List<IStorageFolder>();

        // Prefer NWN documents tlk folder
        var nwnPath = RadoubSettings.Instance.NeverwinterNightsPath;
        if (!string.IsNullOrEmpty(nwnPath))
        {
            var tlkFolder = Path.Combine(nwnPath, "tlk");
            if (Directory.Exists(tlkFolder))
            {
                var folder = await storage.TryGetFolderFromPathAsync(tlkFolder);
                if (folder != null)
                    suggestedLocations.Add(folder);
            }
        }

        // Also try module directory
        if (!string.IsNullOrEmpty(_modulePath))
        {
            var moduleDir = Directory.Exists(_modulePath) ? _modulePath : Path.GetDirectoryName(_modulePath);
            if (!string.IsNullOrEmpty(moduleDir) && Directory.Exists(moduleDir))
            {
                var folder = await storage.TryGetFolderFromPathAsync(moduleDir);
                if (folder != null)
                    suggestedLocations.Add(folder);
            }
        }

        var options = new FilePickerOpenOptions
        {
            Title = "Select Custom TLK File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("TLK Files") { Patterns = new[] { "*.tlk" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            },
            SuggestedStartLocation = suggestedLocations.FirstOrDefault()
        };

        var result = await storage.OpenFilePickerAsync(options);

        if (result.Count > 0)
        {
            var selectedFile = result[0];
            var filePath = selectedFile.TryGetLocalPath();
            if (!string.IsNullOrEmpty(filePath))
            {
                // Store just the name without extension (as IFO expects)
                CustomTlk = Path.GetFileNameWithoutExtension(filePath);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Selected custom TLK: {CustomTlk}");
            }
        }
    }

    /// <summary>
    /// Browse for a script using the shared ScriptBrowserWindow.
    /// </summary>
    /// <param name="scriptFieldName">The name of the script property to update (e.g., "OnModuleLoad")</param>
    [RelayCommand]
    private async Task BrowseScriptAsync(string scriptFieldName)
    {
        if (_parentWindow == null) return;

        // Create context with current module's working directory
        var context = new TrebuchetScriptBrowserContext(_workingDirectoryPath ?? _modulePath, _gameDataService);
        var browser = new ScriptBrowserWindow(context);

        var result = await browser.ShowDialog<string?>(_parentWindow);

        if (!string.IsNullOrEmpty(result))
        {
            // Use reflection to set the script property
            SetScriptProperty(scriptFieldName, result);
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Selected script for {scriptFieldName}: {result}");
        }
    }

    /// <summary>
    /// Open a script file in the system's default editor.
    /// If the file doesn't exist, offers to create it.
    /// </summary>
    /// <param name="scriptFieldName">The name of the script property to edit</param>
    [RelayCommand]
    private async Task EditScriptAsync(string scriptFieldName)
    {
        var scriptName = GetScriptProperty(scriptFieldName);
        if (string.IsNullOrEmpty(scriptName))
        {
            StatusText = "No script to edit - select or enter a script name first";
            return;
        }

        // Find the script file in the module directory
        var searchDir = _workingDirectoryPath ?? _modulePath;
        if (string.IsNullOrEmpty(searchDir) || !Directory.Exists(searchDir))
        {
            StatusText = "Module directory not available";
            return;
        }

        var scriptPath = Path.Combine(searchDir, $"{scriptName}.nss");
        if (!File.Exists(scriptPath))
        {
            // Offer to create the file
            if (_parentWindow != null && await ConfirmCreateScriptAsync(scriptName))
            {
                try
                {
                    // Create empty script file with header comment
                    var scriptContent = $$"""
                        // {{scriptName}}.nss
                        // Created by Trebuchet Module Editor

                        void main()
                        {

                        }
                        """;
                    await File.WriteAllTextAsync(scriptPath, scriptContent);
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Created new script: {UnifiedLogger.SanitizePath(scriptPath)}");
                    StatusText = $"Created {scriptName}.nss";
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to create script: {ex.Message}");
                    StatusText = $"Failed to create script: {ex.Message}";
                    return;
                }
            }
            else
            {
                return; // User cancelled or no parent window
            }
        }

        try
        {
            // Open with system default editor
            var psi = new ProcessStartInfo
            {
                FileName = scriptPath,
                UseShellExecute = true
            };
            Process.Start(psi);
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Opened script in editor: {UnifiedLogger.SanitizePath(scriptPath)}");
            StatusText = $"Opened {scriptName}.nss in editor";
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open script: {ex.Message}");
            StatusText = $"Failed to open script: {ex.Message}";
        }
    }

    /// <summary>
    /// Show confirmation dialog to create a new script file.
    /// </summary>
    private async Task<bool> ConfirmCreateScriptAsync(string scriptName)
    {
        if (_parentWindow == null) return false;

        var tcs = new TaskCompletionSource<bool>();

        var messageText = new Avalonia.Controls.TextBlock
        {
            Text = $"Script file '{scriptName}.nss' does not exist.\n\nDo you want to create it?",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var createButton = new Avalonia.Controls.Button
        {
            Content = "Create",
            Margin = new Avalonia.Thickness(8, 0, 0, 0),
            Padding = new Avalonia.Thickness(16, 6)
        };

        var cancelButton = new Avalonia.Controls.Button
        {
            Content = "Cancel",
            Padding = new Avalonia.Thickness(16, 6)
        };

        var buttonPanel = new Avalonia.Controls.StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 16, 0, 0),
            Children = { cancelButton, createButton }
        };

        var contentPanel = new Avalonia.Controls.StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Children = { messageText, buttonPanel }
        };

        var dialog = new Avalonia.Controls.Window
        {
            Title = "Create Script?",
            Width = 350,
            SizeToContent = Avalonia.Controls.SizeToContent.Height,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = contentPanel
        };

        createButton.Click += (_, _) => { tcs.TrySetResult(true); dialog.Close(); };
        cancelButton.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };
        dialog.Closed += (_, _) => tcs.TrySetResult(false); // Handle window close button

        dialog.Show(_parentWindow);
        return await tcs.Task;
    }

    /// <summary>
    /// Set a script property by name using reflection.
    /// </summary>
    private void SetScriptProperty(string propertyName, string value)
    {
        var property = GetType().GetProperty(propertyName);
        if (property != null && property.CanWrite)
        {
            property.SetValue(this, value);
        }
    }

    /// <summary>
    /// Get a script property value by name using reflection.
    /// </summary>
    private string? GetScriptProperty(string propertyName)
    {
        var property = GetType().GetProperty(propertyName);
        return property?.GetValue(this) as string;
    }

    private void UpdateIfoFromViewModel()
    {
        if (_ifoFile == null) return;

        // Metadata
        _ifoFile.ModuleName.LocalizedStrings[0] = ModuleName;
        _ifoFile.ModuleDescription.LocalizedStrings[0] = ModuleDescription;
        _ifoFile.Tag = ModuleTag;
        _ifoFile.CustomTlk = CustomTlk;

        // Version/Requirements
        _ifoFile.MinGameVersion = MinGameVersion;
        ushort expansion = 0;
        if (RequiresSoU) expansion |= (ushort)ExpansionPackFlags.ShadowsOfUndrentide;
        if (RequiresHotU) expansion |= (ushort)ExpansionPackFlags.HordesOfTheUnderdark;
        _ifoFile.ExpansionPack = expansion;

        // HAK List
        _ifoFile.HakList = HakList.ToList();

        // Time Settings
        _ifoFile.DawnHour = DawnHour;
        _ifoFile.DuskHour = DuskHour;
        _ifoFile.MinutesPerHour = MinutesPerHour;
        _ifoFile.StartYear = StartYear;
        _ifoFile.StartMonth = StartMonth;
        _ifoFile.StartDay = StartDay;
        _ifoFile.StartHour = StartHour;

        // Entry Point
        _ifoFile.EntryArea = EntryArea;
        _ifoFile.EntryX = EntryX;
        _ifoFile.EntryY = EntryY;
        _ifoFile.EntryZ = EntryZ;

        // Scripts
        _ifoFile.OnModuleLoad = OnModuleLoad;
        _ifoFile.OnClientEnter = OnClientEnter;
        _ifoFile.OnClientLeave = OnClientLeave;
        _ifoFile.OnHeartbeat = OnHeartbeat;
        _ifoFile.OnAcquireItem = OnAcquireItem;
        _ifoFile.OnActivateItem = OnActivateItem;
        _ifoFile.OnUnacquireItem = OnUnacquireItem;
        _ifoFile.OnPlayerDeath = OnPlayerDeath;
        _ifoFile.OnPlayerDying = OnPlayerDying;
        _ifoFile.OnPlayerRest = OnPlayerRest;
        _ifoFile.OnPlayerEquipItem = OnPlayerEquipItem;
        _ifoFile.OnPlayerUnequipItem = OnPlayerUnequipItem;
        _ifoFile.OnPlayerLevelUp = OnPlayerLevelUp;
        _ifoFile.OnUserDefined = OnUserDefined;
        _ifoFile.OnSpawnButtonDown = OnSpawnButtonDown;
        _ifoFile.OnCutsceneAbort = OnCutsceneAbort;

        // NWN:EE Extended Scripts
        _ifoFile.OnModuleStart = OnModuleStart;
        _ifoFile.OnPlayerChat = OnPlayerChat;
        _ifoFile.OnPlayerTarget = OnPlayerTarget;
        _ifoFile.OnPlayerGuiEvent = OnPlayerGuiEvent;
        _ifoFile.OnPlayerTileAction = OnPlayerTileAction;
        _ifoFile.OnNuiEvent = OnNuiEvent;

        // Other
        _ifoFile.XPScale = XpScale;
        _ifoFile.StartMovie = StartMovie;

        // Only set DefaultBic if checkbox is checked
        _ifoFile.DefaultBic = UseDefaultBic ? DefaultBic : string.Empty;

        // Variables
        _ifoFile.VarTable = Variables.Select(v => v.ToVariable()).ToList();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_ifoFile == null || string.IsNullOrEmpty(_modulePath))
        {
            StatusText = "No module to save";
            return;
        }

        IsLoading = true;
        StatusText = "Saving...";

        try
        {
            UpdateIfoFromViewModel();

            await Task.Run(() =>
            {
                if (_isFromModFile && !string.IsNullOrEmpty(_modFilePath))
                {
                    // Save to MOD file
                    var ifoData = IfoWriter.Write(_ifoFile);
                    var backupPath = ErfWriter.UpdateResource(_modFilePath, "module", ResourceTypes.Ifo, ifoData, createBackup: true);

                    if (backupPath != null)
                    {
                        UnifiedLogger.LogApplication(LogLevel.INFO, $"Created backup: {UnifiedLogger.SanitizePath(backupPath)}");
                    }
                }
                else
                {
                    // Save to extracted directory
                    var ifoPath = Path.Combine(_modulePath!, "module.ifo");
                    IfoWriter.Write(_ifoFile, ifoPath);
                }
            });

            StatusText = _isFromModFile ? "Saved to MOD file" : "Saved module.ifo";
            HasUnsavedChanges = false;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Saved IFO for: {UnifiedLogger.SanitizePath(_modulePath)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save: {ex.Message}");
            StatusText = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Unpack Command

    [RelayCommand(CanExecute = nameof(CanUnpack))]
    private async Task UnpackAsync()
    {
        if (string.IsNullOrEmpty(_modFilePath))
        {
            StatusText = "No MOD file to unpack";
            return;
        }

        // Default unpack directory: same folder as .mod, with module name
        var moduleName = Path.GetFileNameWithoutExtension(_modFilePath);
        var moduleDir = Path.GetDirectoryName(_modFilePath);
        var targetDir = Path.Combine(moduleDir!, moduleName);

        // Check if directory already exists
        if (Directory.Exists(targetDir))
        {
            // For now, we'll skip if exists - future: could prompt user
            StatusText = $"Directory already exists: {moduleName}/";
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Unpack target already exists: {UnifiedLogger.SanitizePath(targetDir)}");

            // Reload from unpacked directory to switch to editable mode
            await LoadModuleAsync(_modFilePath);
            return;
        }

        IsLoading = true;
        StatusText = "Unpacking module...";

        try
        {
            var resourceCount = await Task.Run(() => UnpackModuleToDirectory(_modFilePath, targetDir));

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Unpacked {resourceCount} resources to {UnifiedLogger.SanitizePath(targetDir)}");

            // Reload module from unpacked directory (now editable)
            StatusText = $"Unpacked {resourceCount} files. Reloading...";
            await LoadModuleAsync(_modFilePath);

            StatusText = $"Unpacked to {moduleName}/ - Now editable";
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Unpack failed: {ex.Message}");
            StatusText = $"Unpack failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Extract all resources from a MOD file to a directory.
    /// </summary>
    private static int UnpackModuleToDirectory(string modFilePath, string targetDir)
    {
        // Create target directory
        Directory.CreateDirectory(targetDir);

        // Read ERF metadata (doesn't load resource data into memory)
        var erf = ErfReader.ReadMetadataOnly(modFilePath);

        var count = 0;
        foreach (var resource in erf.Resources)
        {
            // Get file extension for this resource type
            var extension = ResourceTypes.GetExtension(resource.ResourceType);
            var fileName = $"{resource.ResRef}{extension}";
            var filePath = Path.Combine(targetDir, fileName);

            // Extract resource data from MOD file
            var data = ErfReader.ExtractResource(modFilePath, resource);

            // Write to file
            File.WriteAllBytes(filePath, data);
            count++;
        }

        return count;
    }

    // Version Edit Command

    [RelayCommand]
    private async Task EditVersionAsync()
    {
        if (IsVersionUnlocked)
        {
            // Lock the version field
            IsVersionUnlocked = false;
            return;
        }

        // Check for EE-specific fields that have values
        if (_ifoFile == null)
        {
            IsVersionUnlocked = true;
            return;
        }

        // Build current IFO state from ViewModel for accurate check
        UpdateIfoFromViewModel();

        var eeFields = IfoVersionRequirements.GetPopulatedEeFields(_ifoFile);

        if (eeFields.Count == 0)
        {
            // No EE fields populated, just unlock
            IsVersionUnlocked = true;
            StatusText = "Version field unlocked. No EE-specific fields detected.";
            return;
        }

        // Show warning dialog with list of EE fields
        var fieldList = string.Join("\n", eeFields.Select(f =>
            $"  - {f.DisplayName}: \"{f.Value}\" (requires {f.MinVersion}+)"));

        var requiredVersion = IfoVersionRequirements.GetRequiredVersion(_ifoFile);

        var message = $"This module contains NWN:EE-specific data:\n\n{fieldList}\n\n" +
            $"Minimum required version: {requiredVersion}\n\n" +
            "If you set the minimum version lower than required:\n" +
            "- The game will still load the module (unknown fields are ignored)\n" +
            "- EE-only scripts will not fire on older versions\n" +
            "- Opening in the 1.69 Aurora Toolset and saving will permanently remove these fields\n\n" +
            "Do you want to unlock the version field?";

        if (_parentWindow == null) return;

        // Build the dialog content
        var messageText = new Avalonia.Controls.TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var unlockButton = new Avalonia.Controls.Button
        {
            Content = "Unlock Version",
            Margin = new Avalonia.Thickness(8, 0, 0, 0),
            Padding = new Avalonia.Thickness(16, 6)
        };

        var cancelButton = new Avalonia.Controls.Button
        {
            Content = "Cancel",
            Padding = new Avalonia.Thickness(16, 6)
        };

        var buttonPanel = new Avalonia.Controls.StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 16, 0, 0),
            Children = { cancelButton, unlockButton }
        };

        var contentPanel = new Avalonia.Controls.StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Children =
            {
                new Avalonia.Controls.ScrollViewer
                {
                    MaxHeight = 300,
                    Content = messageText
                },
                buttonPanel
            }
        };

        var dialog = new Avalonia.Controls.Window
        {
            Title = "Version Compatibility Warning",
            Width = 500,
            SizeToContent = Avalonia.Controls.SizeToContent.Height,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = contentPanel
        };

        var result = false;
        unlockButton.Click += (_, _) => { result = true; dialog.Close(); };
        cancelButton.Click += (_, _) => { result = false; dialog.Close(); };

        await dialog.ShowDialog(_parentWindow);

        if (result)
        {
            IsVersionUnlocked = true;
            StatusText = $"Version unlocked. Current data requires {requiredVersion}+";
        }
    }

    // HAK List Commands

    [RelayCommand]
    private void AddHak()
    {
        if (string.IsNullOrWhiteSpace(NewHakName)) return;

        var hakName = NewHakName.Trim();
        // Remove .hak extension if present
        if (hakName.EndsWith(".hak", StringComparison.OrdinalIgnoreCase))
            hakName = hakName[..^4];

        if (!HakList.Contains(hakName, StringComparer.OrdinalIgnoreCase))
        {
            HakList.Add(hakName);
            HasUnsavedChanges = true;
        }

        NewHakName = string.Empty;
    }

    [RelayCommand]
    private void RemoveHak()
    {
        if (SelectedHak != null)
        {
            HakList.Remove(SelectedHak);
            HasUnsavedChanges = true;
        }
    }

    [RelayCommand]
    private void MoveHakUp()
    {
        if (SelectedHak == null) return;

        var index = HakList.IndexOf(SelectedHak);
        if (index > 0)
        {
            HakList.Move(index, index - 1);
            HasUnsavedChanges = true;
        }
    }

    [RelayCommand]
    private void MoveHakDown()
    {
        if (SelectedHak == null) return;

        var index = HakList.IndexOf(SelectedHak);
        if (index < HakList.Count - 1)
        {
            HakList.Move(index, index + 1);
            HasUnsavedChanges = true;
        }
    }

    // Variable Commands

    [RelayCommand]
    private void AddVariable()
    {
        var newVar = new VariableViewModel(Variable.CreateInt("", 0));
        newVar.SetUniquenessCheck(IsVariableNameUnique);
        Variables.Add(newVar);
        SelectedVariable = newVar;
        HasUnsavedChanges = true;

        // Notify View to auto-focus the name field
        VariableAdded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Check if a variable name is unique within the collection.
    /// </summary>
    private bool IsVariableNameUnique(string name, VariableViewModel currentVar)
    {
        if (string.IsNullOrEmpty(name)) return true; // Empty names handled separately

        return !Variables.Any(v => v != currentVar &&
            string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    [RelayCommand]
    private void RemoveVariable()
    {
        if (SelectedVariable != null)
        {
            Variables.Remove(SelectedVariable);
            HasUnsavedChanges = true;
        }
    }

    // Partial methods to track changes - generated by [ObservableProperty]
    // These are called when properties are set, allowing us to mark unsaved changes

    partial void OnModuleNameChanged(string value) => MarkChanged();
    partial void OnModuleDescriptionChanged(string value) => MarkChanged();
    partial void OnModuleTagChanged(string value) => MarkChanged();
    partial void OnCustomTlkChanged(string value) => MarkChanged();
    partial void OnMinGameVersionChanged(string value) => MarkChanged();
    partial void OnRequiresSoUChanged(bool value) => MarkChanged();
    partial void OnRequiresHotUChanged(bool value) => MarkChanged();
    partial void OnDawnHourChanged(byte value) => MarkChanged();
    partial void OnDuskHourChanged(byte value) => MarkChanged();
    partial void OnMinutesPerHourChanged(byte value) => MarkChanged();
    partial void OnStartYearChanged(uint value) => MarkChanged();
    partial void OnStartMonthChanged(byte value) => MarkChanged();
    partial void OnStartDayChanged(byte value) => MarkChanged();
    partial void OnStartHourChanged(byte value) => MarkChanged();
    partial void OnEntryAreaChanged(string value) => MarkChanged();
    partial void OnEntryXChanged(float value) => MarkChanged();
    partial void OnEntryYChanged(float value) => MarkChanged();
    partial void OnEntryZChanged(float value) => MarkChanged();
    partial void OnOnModuleLoadChanged(string value) => MarkChanged();
    partial void OnOnClientEnterChanged(string value) => MarkChanged();
    partial void OnOnClientLeaveChanged(string value) => MarkChanged();
    partial void OnOnHeartbeatChanged(string value) => MarkChanged();
    partial void OnOnAcquireItemChanged(string value) => MarkChanged();
    partial void OnOnActivateItemChanged(string value) => MarkChanged();
    partial void OnOnUnacquireItemChanged(string value) => MarkChanged();
    partial void OnOnPlayerDeathChanged(string value) => MarkChanged();
    partial void OnOnPlayerDyingChanged(string value) => MarkChanged();
    partial void OnOnPlayerRestChanged(string value) => MarkChanged();
    partial void OnOnPlayerEquipItemChanged(string value) => MarkChanged();
    partial void OnOnPlayerUnequipItemChanged(string value) => MarkChanged();
    partial void OnOnPlayerLevelUpChanged(string value) => MarkChanged();
    partial void OnOnUserDefinedChanged(string value) => MarkChanged();
    partial void OnOnSpawnButtonDownChanged(string value) => MarkChanged();
    partial void OnOnCutsceneAbortChanged(string value) => MarkChanged();
    partial void OnOnModuleStartChanged(string value) => MarkChanged();
    partial void OnOnPlayerChatChanged(string value) => MarkChanged();
    partial void OnOnPlayerTargetChanged(string value) => MarkChanged();
    partial void OnOnPlayerGuiEventChanged(string value) => MarkChanged();
    partial void OnOnPlayerTileActionChanged(string value) => MarkChanged();
    partial void OnOnNuiEventChanged(string value) => MarkChanged();
    partial void OnXpScaleChanged(byte value) => MarkChanged();
    partial void OnDefaultBicChanged(string value)
    {
        MarkChanged();
        // Sync to RadoubSettings so MainWindow can check if DefaultBic is set
        SyncDefaultBicToSettings();
    }

    partial void OnUseDefaultBicChanged(bool value)
    {
        MarkChanged();
        OnPropertyChanged(nameof(IsDefaultBicDropdownEnabled));

        if (!value)
        {
            // Clear DefaultBic when unchecked
            DefaultBic = string.Empty;
        }
        else if (AvailableBicFiles.Count > 0 && string.IsNullOrEmpty(DefaultBic))
        {
            // Auto-select first BIC if available and none selected
            DefaultBic = AvailableBicFiles[0];
        }

        // Sync to RadoubSettings so MainWindow can check if DefaultBic is set
        SyncDefaultBicToSettings();
    }

    partial void OnStartMovieChanged(string value) => MarkChanged();

    private void MarkChanged()
    {
        // Only mark as changed if module is loaded (not during initial population)
        if (IsModuleLoaded)
        {
            HasUnsavedChanges = true;
        }
    }
}

/// <summary>
/// ViewModel for a single variable in the VarTable.
/// </summary>
public partial class VariableViewModel : ObservableObject, System.ComponentModel.INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = new();
    private Func<string, VariableViewModel, bool>? _isNameUnique;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private VariableType _type;

    [ObservableProperty]
    private string _valueString;

    /// <summary>
    /// Validation error message for the Value field (shown in UI).
    /// </summary>
    [ObservableProperty]
    private string? _valueError;

    /// <summary>
    /// Validation error message for the Name field (shown in UI).
    /// </summary>
    [ObservableProperty]
    private string? _nameError;

    public static ObservableCollection<VariableType> VariableTypes { get; } = new()
    {
        VariableType.Int,
        VariableType.Float,
        VariableType.String
    };

    public VariableViewModel(Variable variable)
    {
        _name = variable.Name;
        _type = variable.Type;
        _valueString = variable.Type switch
        {
            VariableType.Int => variable.GetInt().ToString(),
            VariableType.Float => variable.GetFloat().ToString("F2"),
            VariableType.String => variable.GetString(),
            _ => string.Empty
        };
    }

    /// <summary>
    /// Set the uniqueness check function. Called by parent ViewModel.
    /// </summary>
    public void SetUniquenessCheck(Func<string, VariableViewModel, bool> isNameUnique)
    {
        _isNameUnique = isNameUnique;
    }

    partial void OnNameChanged(string value)
    {
        ValidateName();
    }

    partial void OnTypeChanged(VariableType value)
    {
        // Re-validate when type changes
        ValidateValue();
    }

    partial void OnValueStringChanged(string value)
    {
        ValidateValue();
    }

    private void ValidateName()
    {
        ClearErrors(nameof(Name));
        NameError = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            AddError(nameof(Name), "Name is required");
            NameError = "Name is required";
            return;
        }

        if (_isNameUnique != null && !_isNameUnique(Name, this))
        {
            AddError(nameof(Name), "Name must be unique");
            NameError = "Name must be unique";
        }
    }

    private void ValidateValue()
    {
        ClearErrors(nameof(ValueString));
        ValueError = null;

        switch (Type)
        {
            case VariableType.Int:
                if (!string.IsNullOrEmpty(ValueString) && !int.TryParse(ValueString, out _))
                {
                    AddError(nameof(ValueString), "Must be a valid integer");
                    ValueError = "Must be a valid integer";
                }
                break;

            case VariableType.Float:
                if (!string.IsNullOrEmpty(ValueString) && !float.TryParse(ValueString, out _))
                {
                    AddError(nameof(ValueString), "Must be a valid number");
                    ValueError = "Must be a valid number";
                }
                break;
        }
    }

    public Variable ToVariable()
    {
        return Type switch
        {
            VariableType.Int => Variable.CreateInt(Name ?? "", int.TryParse(ValueString, out var i) ? i : 0),
            VariableType.Float => Variable.CreateFloat(Name ?? "", float.TryParse(ValueString, out var f) ? f : 0f),
            VariableType.String => Variable.CreateString(Name ?? "", ValueString ?? ""),
            _ => Variable.CreateInt(Name ?? "", 0)
        };
    }

    // INotifyDataErrorInfo implementation
    public bool HasErrors => _errors.Count > 0;

    public event EventHandler<System.ComponentModel.DataErrorsChangedEventArgs>? ErrorsChanged;

    public System.Collections.IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return _errors.SelectMany(e => e.Value);

        return _errors.TryGetValue(propertyName, out var errors) ? errors : Enumerable.Empty<string>();
    }

    private void AddError(string propertyName, string error)
    {
        if (!_errors.ContainsKey(propertyName))
            _errors[propertyName] = new List<string>();

        if (!_errors[propertyName].Contains(error))
        {
            _errors[propertyName].Add(error);
            ErrorsChanged?.Invoke(this, new System.ComponentModel.DataErrorsChangedEventArgs(propertyName));
            OnPropertyChanged(nameof(HasErrors));
        }
    }

    private void ClearErrors(string propertyName)
    {
        if (_errors.Remove(propertyName))
        {
            ErrorsChanged?.Invoke(this, new System.ComponentModel.DataErrorsChangedEventArgs(propertyName));
            OnPropertyChanged(nameof(HasErrors));
        }
    }
}
