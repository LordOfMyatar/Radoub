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
        DefaultBic = _ifoFile.DefaultBic;
        StartMovie = _ifoFile.StartMovie;

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

        // NWN:EE Extended Scripts
        _ifoFile.OnModuleStart = OnModuleStart;
        _ifoFile.OnPlayerChat = OnPlayerChat;
        _ifoFile.OnPlayerTarget = OnPlayerTarget;
        _ifoFile.OnPlayerGuiEvent = OnPlayerGuiEvent;
        _ifoFile.OnPlayerTileAction = OnPlayerTileAction;
        _ifoFile.OnNuiEvent = OnNuiEvent;

        // Other
        _ifoFile.XPScale = XpScale;
        _ifoFile.DefaultBic = DefaultBic;
        _ifoFile.StartMovie = StartMovie;

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
            "- The module may fail to load on older game versions\n" +
            "- EE-only scripts will not fire\n" +
            "- Features like DefaultBic may be ignored\n\n" +
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
    partial void OnDefaultBicChanged(string value) => MarkChanged();
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
