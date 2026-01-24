using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Gff;
using Radoub.Formats.Ifo;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;

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

    // Status

    [ObservableProperty]
    private string _statusText = "No module loaded";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private bool _isModuleLoaded;

    [ObservableProperty]
    private bool _isModuleReadOnly;

    public bool CanSave => IsModuleLoaded && !IsModuleReadOnly && HasUnsavedChanges;

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

    // Other Settings

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnsavedChanges))]
    private byte _xpScale = 100;

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
            IsModuleLoaded = true;
            IsModuleReadOnly = _isReadOnly;
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
        // 3. temp01 folder (alternate toolset folder)
        var candidates = new[]
        {
            Path.Combine(moduleDir, moduleName),
            Path.Combine(moduleDir, "temp0"),
            Path.Combine(moduleDir, "temp01")
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

        // Other
        XpScale = _ifoFile.XPScale;

        // Variables
        Variables = new ObservableCollection<VariableViewModel>(
            _ifoFile.VarTable.Select(v => new VariableViewModel(v)));
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

        // Other
        _ifoFile.XPScale = XpScale;

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
        var newVar = new VariableViewModel(Variable.CreateInt("NewVariable", 0));
        Variables.Add(newVar);
        SelectedVariable = newVar;
        HasUnsavedChanges = true;
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
}

/// <summary>
/// ViewModel for a single variable in the VarTable.
/// </summary>
public partial class VariableViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private VariableType _type;

    [ObservableProperty]
    private string _valueString;

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

    public Variable ToVariable()
    {
        return Type switch
        {
            VariableType.Int => Variable.CreateInt(Name, int.TryParse(ValueString, out var i) ? i : 0),
            VariableType.Float => Variable.CreateFloat(Name, float.TryParse(ValueString, out var f) ? f : 0f),
            VariableType.String => Variable.CreateString(Name, ValueString),
            _ => Variable.CreateInt(Name, 0)
        };
    }
}
