using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Utils;
using Radoub.UI.Views;
using RadoubLauncher.Services;
using RadoubLauncher.Views;

namespace RadoubLauncher.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ToolLauncherService _toolLauncher;
    private readonly GameLauncherService _gameLauncher;
    private readonly CancellationTokenSource _cts = new();
    private System.Threading.Timer? _lockPollTimer;
    private Window? _parentWindow;
    private ModuleEditorViewModel? _moduleEditorViewModel;
    private FactionEditorViewModel? _factionEditorViewModel;

    [ObservableProperty]
    private ObservableCollection<ToolInfo> _tools;

    [ObservableProperty]
    private ObservableCollection<string> _recentModules;

    [ObservableProperty]
    private string _currentModuleName = "(No module selected)";

    [ObservableProperty]
    private bool _isModuleValid = true;

    [ObservableProperty]
    private string _gameStatusText = "Game: Not configured";

    [ObservableProperty]
    private string _tlkStatusText = "TLK: Default";

    [ObservableProperty]
    private string _versionText = "v1.0.0";

    [ObservableProperty]
    private bool _updateAvailable;

    [ObservableProperty]
    private string _updateStatusText = "";

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTestModule))]
    [NotifyPropertyChangedFor(nameof(CanLoadModule))]
    private bool _isGameAvailable;

    public bool HasRecentModules => RecentModules.Count > 0;

    public bool HasModule => !string.IsNullOrEmpty(RadoubSettings.Instance.CurrentModulePath);

    public bool CanEditModule => IsModuleValid && !string.IsNullOrEmpty(RadoubSettings.Instance.CurrentModulePath);
    public bool CanTestModule => IsGameAvailable && !string.IsNullOrEmpty(RadoubSettings.Instance.CurrentModulePath);

    /// <summary>
    /// Load Module is disabled when DefaultBic is set (only Test Module works with pre-generated characters).
    /// </summary>
    public bool CanLoadModule => CanTestModule && string.IsNullOrEmpty(RadoubSettings.Instance.CurrentModuleDefaultBic);

    /// <summary>
    /// Dynamic tooltip for Load Module button explaining why it's disabled.
    /// </summary>
    public string LoadModuleTooltip => !CanLoadModule && !string.IsNullOrEmpty(RadoubSettings.Instance.CurrentModuleDefaultBic)
        ? $"Disabled: Module uses DefaultBic ({RadoubSettings.Instance.CurrentModuleDefaultBic}). Use Test Module instead."
        : "Launch with current module, show character select";

    /// <summary>
    /// Whether the DefaultBic checkbox is checked (module uses pre-generated character).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadModule))]
    [NotifyPropertyChangedFor(nameof(LoadModuleTooltip))]
    [NotifyPropertyChangedFor(nameof(IsDefaultBicDropdownEnabled))]
    private bool _useDefaultBic;

    /// <summary>
    /// Available BIC files in the current module.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _availableBicFiles = new();

    /// <summary>
    /// Currently selected DefaultBic.
    /// </summary>
    [ObservableProperty]
    private string _selectedDefaultBic = string.Empty;

    /// <summary>
    /// Whether the DefaultBic dropdown should be enabled.
    /// </summary>
    public bool IsDefaultBicDropdownEnabled => UseDefaultBic && AvailableBicFiles.Count > 0 && HasUnpackedWorkingDirectory();

    /// <summary>
    /// Whether there are BIC files available to select.
    /// </summary>
    public bool HasBicFilesAvailable => AvailableBicFiles.Count > 0;

    /// <summary>
    /// Warning message when no BIC files are found.
    /// </summary>
    public string NoBicFilesMessage => HasUnpackedWorkingDirectory()
        ? "No .bic files found in module folder"
        : "Module not unpacked - unpack first to use DefaultBic";

    /// <summary>
    /// Can build when a module is selected and .mod file is not locked.
    /// </summary>
    public bool CanBuildModule => IsModuleValid && !string.IsNullOrEmpty(RadoubSettings.Instance.CurrentModulePath) && !IsModFileLocked;

    /// <summary>
    /// Can compile scripts when a module is selected and compiler is available.
    /// Works even when .mod is locked — compilation writes to the working directory.
    /// </summary>
    public bool CanCompileScripts => IsModuleValid && !string.IsNullOrEmpty(RadoubSettings.Instance.CurrentModulePath) && IsCompilerAvailable;

    /// <summary>
    /// True when the .mod file is locked by another process (e.g., Aurora Toolset).
    /// Polled periodically. When locked, Build &amp; Save is disabled.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanBuildModule))]
    [NotifyPropertyChangedFor(nameof(NeedsBuildWarning))]
    [NotifyPropertyChangedFor(nameof(BuildWarningText))]
    [NotifyPropertyChangedFor(nameof(ModFileLockWarningText))]
    private bool _isModFileLocked;

    /// <summary>
    /// Warning text shown next to Launch Game when .mod is locked.
    /// </summary>
    public string ModFileLockWarningText
    {
        get
        {
            if (!IsModFileLocked) return "";
            var modPath = GetModFilePath();
            var fileName = string.IsNullOrEmpty(modPath) ? "Module" : Path.GetFileName(modPath);
            return $"{fileName} is locked by another editor. Save your changes in the other editor before testing, or close the other editor.";
        }
    }

    /// <summary>
    /// True when module IFO has been modified since the last build.
    /// Changes via DefaultBic or Module Editor mark this dirty; Build clears it.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsBuildWarning))]
    [NotifyPropertyChangedFor(nameof(BuildWarningText))]
    private bool _isModuleDirty;

    /// <summary>
    /// True when the module needs building before testing.
    /// Checks if any file in the working directory is newer than the .mod file,
    /// or if there are stale scripts (.nss newer than .ncs).
    /// </summary>
    public bool NeedsBuildWarning => IsModuleDirty || StaleScriptCount > 0 || HasUnsavedModuleEditorChanges || HasUnsavedFactionEditorChanges;

    /// <summary>
    /// Poll the .mod file lock status from a background timer.
    /// Updates IsModFileLocked on the UI thread.
    /// </summary>
    private void PollModFileLock()
    {
        var modPath = GetModFilePath();
        var locked = ModuleFileLockService.IsFileLocked(modPath);

        if (locked != IsModFileLocked)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => IsModFileLocked = locked);
        }
    }

    /// <summary>
    /// Describes why a build is recommended before testing.
    /// </summary>
    public string BuildWarningText
    {
        get
        {
            var hasUnsaved = HasUnsavedModuleEditorChanges || HasUnsavedFactionEditorChanges || IsModuleDirty;
            var reasons = new List<string>();

            if (HasUnsavedModuleEditorChanges)
                reasons.Add("unsaved IFO changes");
            if (HasUnsavedFactionEditorChanges)
                reasons.Add("unsaved faction changes");
            if (IsModuleDirty)
                reasons.Add("module modified since last build");
            if (StaleScriptCount > 0)
                reasons.Add($"{StaleScriptCount} script(s) need recompiling");

            if (reasons.Count == 0)
                return "";

            var prefix = hasUnsaved ? "You have unsaved changes" : "Build recommended";
            return $"{prefix}: {string.Join(", ", reasons)}";
        }
    }

    [ObservableProperty]
    private bool _isBuilding;

    [ObservableProperty]
    private string _buildStatusText = "";

    [ObservableProperty]
    private bool _hasBuildLog;

    [ObservableProperty]
    private bool _hasFailedScripts;

    [ObservableProperty]
    private ObservableCollection<FailedScriptItem> _failedScriptItems = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsBuildWarning))]
    [NotifyPropertyChangedFor(nameof(BuildWarningText))]
    private int _staleScriptCount;

    /// <summary>
    /// Whether script compilation is enabled (bound to checkbox near Build button).
    /// </summary>
    public bool CompileScriptsEnabled
    {
        get => SettingsService.Instance.CompileScriptsEnabled;
        set
        {
            if (SettingsService.Instance.CompileScriptsEnabled != value)
            {
                SettingsService.Instance.CompileScriptsEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Whether to compile all scripts (including uncompiled .nss with no .ncs).
    /// When true: compile changed + uncompiled. When false: only changed.
    /// </summary>
    public bool BuildUncompiledScriptsEnabled
    {
        get => SettingsService.Instance.BuildUncompiledScriptsEnabled;
        set
        {
            if (SettingsService.Instance.BuildUncompiledScriptsEnabled != value)
            {
                SettingsService.Instance.BuildUncompiledScriptsEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OnlyCompileChangedScripts));
            }
        }
    }

    /// <summary>
    /// Inverse of BuildUncompiledScriptsEnabled for radio button binding.
    /// </summary>
    public bool OnlyCompileChangedScripts
    {
        get => !BuildUncompiledScriptsEnabled;
        set => BuildUncompiledScriptsEnabled = !value;
    }

    /// <summary>
    /// Whether to auto-run Build &amp; Save before launching a test.
    /// </summary>
    public bool AlwaysSaveBeforeTesting
    {
        get => SettingsService.Instance.AlwaysSaveBeforeTesting;
        set
        {
            if (SettingsService.Instance.AlwaysSaveBeforeTesting != value)
            {
                SettingsService.Instance.AlwaysSaveBeforeTesting = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Whether the NWScript compiler is available.
    /// </summary>
    public bool IsCompilerAvailable => ScriptCompilerService.Instance.IsCompilerAvailable;

    public string CompilerSourceDescription => ScriptCompilerService.Instance.CompilerSourceDescription;

    public string ScriptCompilerPath
    {
        get => SettingsService.Instance.ScriptCompilerPath;
        set
        {
            if (SettingsService.Instance.ScriptCompilerPath != value)
            {
                SettingsService.Instance.ScriptCompilerPath = value;
                ScriptCompilerService.Instance.RefreshCompiler();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCompilerAvailable));
                OnPropertyChanged(nameof(CompilerSourceDescription));
            }
        }
    }

    [RelayCommand]
    private async Task BrowseScriptCompilerPath()
    {
        if (_parentWindow == null) return;

        var files = await _parentWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Script Compiler Executable",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Executables") { Patterns = new[] { "*.exe", "*" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (files.Count > 0)
        {
            ScriptCompilerPath = files[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private void ClearScriptCompilerPath()
    {
        ScriptCompilerPath = "";
    }

    /// <summary>
    /// Path to code editor for opening failed scripts.
    /// </summary>
    public string CodeEditorPath
    {
        get => SettingsService.Instance.CodeEditorPath;
        set
        {
            if (SettingsService.Instance.CodeEditorPath != value)
            {
                SettingsService.Instance.CodeEditorPath = value;
                OnPropertyChanged();
            }
        }
    }

    [RelayCommand]
    private async Task BrowseCodeEditorPath()
    {
        if (_parentWindow == null) return;

        var files = await _parentWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Code Editor Executable",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Executables") { Patterns = new[] { "*.exe" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (files.Count > 0)
        {
            CodeEditorPath = files[0].Path.LocalPath;
        }
    }

    public MainWindowViewModel()
    {
        _toolLauncher = ToolLauncherService.Instance;
        _gameLauncher = GameLauncherService.Instance;
        _tools = new ObservableCollection<ToolInfo>(_toolLauncher.Tools);
        _recentModules = new ObservableCollection<string>(SettingsService.Instance.RecentModules);

        LoadVersionInfo();
        UpdateStatusFromSettings();
        UpdateGameAvailability();
    }

    // --- Lifecycle ---

    public void SetParentWindow(Window window)
    {
        _parentWindow = window;

        // Subscribe to settings changes
        RadoubSettings.Instance.PropertyChanged += OnSharedSettingsChanged;
        SettingsService.Instance.PropertyChanged += OnLocalSettingsChanged;

        // Check for updates on startup (fire and forget)
        _ = CheckForUpdatesAsync(_cts.Token);

        // Poll .mod file lock status every 5 seconds
        _lockPollTimer = new System.Threading.Timer(
            _ => PollModFileLock(),
            null,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5));

        // Scan for BIC files if a module is already selected at startup
        if (!string.IsNullOrEmpty(RadoubSettings.Instance.CurrentModulePath))
        {
            _ = ReadModuleDefaultBicAsync(_cts.Token);
        }
    }

    /// <summary>
    /// Set the embedded module editor ViewModel reference.
    /// Called by MainWindow after initializing the ModuleEditorPanel.
    /// </summary>
    public void SetModuleEditorViewModel(ModuleEditorViewModel viewModel)
    {
        _moduleEditorViewModel = viewModel;

        // Forward HasUnsavedChanges from module editor to build warning
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ModuleEditorViewModel.HasUnsavedChanges))
            {
                OnPropertyChanged(nameof(NeedsBuildWarning));
                OnPropertyChanged(nameof(BuildWarningText));
            }
        };

        // Update status bar when TLK language changes
        viewModel.TlkLanguageChanged += (_, _) => UpdateStatusFromSettings();
    }

    /// <summary>
    /// Set the embedded faction editor ViewModel reference.
    /// Called by MainWindow after initializing the FactionEditorPanel.
    /// </summary>
    public void SetFactionEditorViewModel(FactionEditorViewModel viewModel)
    {
        _factionEditorViewModel = viewModel;

        // Forward HasUnsavedChanges from faction editor to build warning
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FactionEditorViewModel.HasUnsavedChanges))
            {
                OnPropertyChanged(nameof(NeedsBuildWarning));
                OnPropertyChanged(nameof(BuildWarningText));
            }
        };
    }

    /// <summary>
    /// Whether the embedded module editor has unsaved IFO changes.
    /// </summary>
    public bool HasUnsavedModuleEditorChanges => _moduleEditorViewModel?.HasUnsavedChanges == true;

    /// <summary>
    /// Whether the embedded faction editor has unsaved changes.
    /// </summary>
    public bool HasUnsavedFactionEditorChanges => _factionEditorViewModel?.HasUnsavedChanges == true;

    /// <summary>
    /// Unsubscribe from singleton events to prevent memory leaks (#1282).
    /// Called from MainWindow.OnWindowClosing.
    /// </summary>
    public void Cleanup()
    {
        _lockPollTimer?.Dispose();
        _cts.Cancel();
        _cts.Dispose();
        RadoubSettings.Instance.PropertyChanged -= OnSharedSettingsChanged;
        SettingsService.Instance.PropertyChanged -= OnLocalSettingsChanged;
    }

    // --- Status & Settings ---

    private void LoadVersionInfo()
    {
        try
        {
            VersionText = $"v{VersionHelper.GetVersion()}";
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Could not get version info: {ex.Message}");
            VersionText = "v0.1.0";
        }
    }

    private void UpdateStatusFromSettings()
    {
        var shared = RadoubSettings.Instance;

        // Module status with validation
        if (!string.IsNullOrEmpty(shared.CurrentModulePath))
        {
            var modulePath = shared.CurrentModulePath;
            var moduleName = Path.GetFileNameWithoutExtension(modulePath);

            // Check if it's a .mod file that exists
            if (modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase) && File.Exists(modulePath))
            {
                CurrentModuleName = moduleName;
                IsModuleValid = true;
            }
            // Legacy: check if it's a working directory with module.ifo
            else if (Directory.Exists(modulePath) && File.Exists(Path.Combine(modulePath, "module.ifo")))
            {
                CurrentModuleName = Path.GetFileName(modulePath);
                IsModuleValid = true;
            }
            else
            {
                CurrentModuleName = $"{Path.GetFileName(modulePath)} (Invalid)";
                IsModuleValid = false;
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Module validation failed: file not found or invalid format");
            }
        }
        else
        {
            CurrentModuleName = "(No module selected)";
            IsModuleValid = true;
        }

        // Game status
        if (!string.IsNullOrEmpty(shared.BaseGameInstallPath))
        {
            var gamePath = shared.BaseGameInstallPath;
            // Detect game type from path
            if (gamePath.Contains("Steam", StringComparison.OrdinalIgnoreCase))
            {
                GameStatusText = "Game: NWN:EE (Steam)";
            }
            else if (gamePath.Contains("GOG", StringComparison.OrdinalIgnoreCase))
            {
                GameStatusText = "Game: NWN:EE (GOG)";
            }
            else
            {
                GameStatusText = "Game: Configured";
            }
        }
        else
        {
            GameStatusText = "Game: Not configured";
        }

        // TLK status
        var lang = shared.EffectiveLanguage;
        var gender = shared.TlkUseFemale ? " (F)" : "";
        TlkStatusText = $"TLK: {lang}{gender}";
    }

    private void OnSharedSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        UpdateStatusFromSettings();
        UpdateGameAvailability();

        // Update module-dependent properties when module changes
        if (e.PropertyName == nameof(RadoubSettings.CurrentModulePath))
        {
            // New module selected - reset manual dirty state
            IsModuleDirty = false;
            RefreshBuildStatus();

            // Reload module editor first (may auto-unpack .mod), then faction editor (#1384)
            _ = ReloadModuleEditorThenDependentsAsync();

            OnPropertyChanged(nameof(HasModule));
            OnPropertyChanged(nameof(CanEditModule));
            OnPropertyChanged(nameof(CanTestModule));
            OnPropertyChanged(nameof(CanLoadModule));
            OnPropertyChanged(nameof(CanBuildModule));
            BuildModuleCommand.NotifyCanExecuteChanged();
        }

        // Update Load Module button when DefaultBic changes
        if (e.PropertyName == nameof(RadoubSettings.CurrentModuleDefaultBic))
        {
            OnPropertyChanged(nameof(CanLoadModule));
        }
    }

    private void UpdateGameAvailability()
    {
        _gameLauncher.RefreshDiscovery();
        IsGameAvailable = _gameLauncher.IsGameAvailable;
    }

    private void OnLocalSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.RecentModules))
        {
            RecentModules = new ObservableCollection<string>(SettingsService.Instance.RecentModules);
            OnPropertyChanged(nameof(HasRecentModules));
        }
    }

    // --- Tool Launching ---

    [RelayCommand]
    private void LaunchTool(ToolInfo tool)
    {
        if (tool == null) return;

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Launching tool: {tool.Name}");

        // Ensure module info is synced to RadoubSettings before launching (#1384)
        EnsureSettingsSynced();

        _toolLauncher.LaunchTool(tool);
    }

    [RelayCommand]
    private void LaunchToolWithFile(ToolFileLaunchInfo launchInfo)
    {
        if (launchInfo?.Tool == null) return;

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"Launching {launchInfo.Tool.Name} with file: {Path.GetFileName(launchInfo.FilePath)}");

        // Ensure module info is synced to RadoubSettings before launching (#1384)
        EnsureSettingsSynced();

        _toolLauncher.LaunchToolWithFile(launchInfo.Tool, launchInfo.FilePath);
    }

    /// <summary>
    /// Ensure RadoubSettings is fully synced before launching a child tool.
    /// Forces module editor to write current TLK/module state to the shared settings file
    /// so the child process reads up-to-date values. (#1384)
    /// </summary>
    private void EnsureSettingsSynced()
    {
        _moduleEditorViewModel?.SyncToRadoubSettings();

        var settings = RadoubSettings.Instance;
        var modulePath = settings.CurrentModulePath;
        var tlkPath = settings.CustomTlkPath;

        UnifiedLogger.LogApplication(LogLevel.DEBUG,
            $"Pre-launch settings sync - Module: {(string.IsNullOrEmpty(modulePath) ? "(none)" : UnifiedLogger.SanitizePath(modulePath))}, " +
            $"TLK: {(string.IsNullOrEmpty(tlkPath) ? "(none)" : UnifiedLogger.SanitizePath(tlkPath))}");
    }

    /// <summary>
    /// Get recent files for a tool (for UI binding).
    /// </summary>
    public List<RecentFileInfo> GetToolRecentFiles(string toolName)
    {
        return ToolRecentFilesService.Instance.GetRecentFiles(toolName);
    }

    /// <summary>
    /// Check if a tool has recent files.
    /// </summary>
    public bool ToolHasRecentFiles(string toolName)
    {
        return ToolRecentFilesService.Instance.HasRecentFiles(toolName);
    }

    // --- Settings & About ---

    [RelayCommand]
    private void OpenSettings()
    {
        if (_parentWindow == null) return;

        UnifiedLogger.LogApplication(LogLevel.INFO, "Opening settings window");
        var settingsWindow = new SettingsWindow();
        settingsWindow.Show(_parentWindow);  // Non-modal settings window
    }

    [RelayCommand]
    private void EditSettingsFile()
    {
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Radoub", "RadoubSettings.json");

        if (!File.Exists(settingsPath))
        {
            UpdateStatusText = "Settings file not found";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(settingsPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to open settings file: {ex.Message}");
            UpdateStatusText = "Could not open settings file";
        }
    }

    [RelayCommand]
    private void OpenAbout()
    {
        if (_parentWindow == null) return;

        UnifiedLogger.LogApplication(LogLevel.INFO, "Opening about window");

        var aboutWindow = Radoub.UI.Views.AboutWindow.Create(new AboutWindowConfig
        {
            ToolName = "Trebuchet",
            Subtitle = "Radoub Launcher for Neverwinter Nights",
            Version = VersionHelper.GetVersion(),
            AdditionalInfo = "Radoub Toolset includes:\n" +
                "Parley - Dialog Editor\n" +
                "Manifest - Journal Editor\n" +
                "Quartermaster - Creature/Item Editor\n" +
                "Fence - Merchant Editor\n\n" +
                "Third-Party Components:\n" +
                "nwn_script_comp - NWScript Compiler (MIT License)\n" +
                "by Bernhard Stöckner (niv)",
            ThirdPartyUrl = "https://github.com/niv/neverwinter.nim",
            ThirdPartyLinkText = "github.com/niv/neverwinter.nim"
        });
        aboutWindow.Show(_parentWindow);  // Non-modal about window
    }

    [RelayCommand]
    private void RefreshTools()
    {
        _toolLauncher.RefreshTools();
        Tools = new ObservableCollection<ToolInfo>(_toolLauncher.Tools);
    }

    // --- Updates ---

    [RelayCommand]
    private Task CheckForUpdatesAsync() => CheckForUpdatesAsync(_cts.Token);

    private async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        IsCheckingForUpdates = true;
        UpdateStatusText = "Checking for updates...";

        try
        {
            var hasUpdate = await UpdateService.Instance.CheckForUpdatesAsync(cancellationToken);
            UpdateAvailable = hasUpdate;

            if (hasUpdate)
            {
                UpdateStatusText = $"Update available: v{UpdateService.Instance.LatestVersion}";
            }
            else
            {
                UpdateStatusText = "Up to date";
            }
        }
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Update check cancelled");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Update check failed: {ex.Message}");
            UpdateStatusText = "Update check failed";
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    [RelayCommand]
    private void OpenReleasePage()
    {
        UpdateService.Instance.OpenReleasePage();
    }
}
