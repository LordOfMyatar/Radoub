using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Radoub.Formats.Gff;
using Radoub.Formats.Ifo;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;

namespace RadoubLauncher.ViewModels;

// IFO <-> ViewModel synchronization, BIC scanning, TLK resolution, settings sync
public partial class ModuleEditorViewModel
{
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
}
