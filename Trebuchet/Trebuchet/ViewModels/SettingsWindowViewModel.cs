using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Services;
using Radoub.UI.Views;
using RadoubLauncher.Services;
using RadoubLauncher.Views;
using Radoub.UI.Utils;

namespace RadoubLauncher.ViewModels;

/// <summary>
/// How the Settings window is being shown (#2419). The same tabbed window serves
/// normal settings and the first-run / version-gate setup flow.
/// </summary>
public enum SettingsSetupMode
{
    /// <summary>Opened from the toolbar — plain settings, no banner or module picker.</summary>
    Normal,
    /// <summary>True first run — welcome banner + Review-tab module picker + shortcut offer.</summary>
    Welcome,
    /// <summary>A newer build added settings, or a required setting is missing — review banner only.</summary>
    WelcomeBack,
}

public partial class SettingsWindowViewModel : ObservableObject
{
    private readonly Window? _window;
    private readonly bool _headless;
    private readonly string _appVersion;
    private readonly string? _originalThemeId;
    private readonly double _originalFontSizePoints;
    private string? _chosenModulePath;
    private static IBrush SuccessBrush => BrushManager.GetSuccessBrush();
    private static IBrush ErrorBrush => BrushManager.GetErrorBrush();

    /// <summary>How this window is being shown (#2419).</summary>
    public SettingsSetupMode SetupMode { get; }

    public bool IsSetupMode => SetupMode != SettingsSetupMode.Normal;
    public bool IsWelcome => SetupMode == SettingsSetupMode.Welcome;

    /// <summary>Module picker (Review tab) is offered only on a true first run.</summary>
    public bool ShowModulePicker => IsWelcome;

    public string BannerText => SetupMode switch
    {
        SettingsSetupMode.Welcome => "Welcome to the Radoub toolset — confirm your setup below. You can change any of these later in Settings.",
        SettingsSetupMode.WelcomeBack => "New settings were added — please review the values below. You can change any of these later in Settings.",
        _ => "",
    };

    [ObservableProperty]
    private int _selectedTabIndex;

    /// <summary>Tabs: 0 Game/Home, 1 Logging, 2 Backups, 3 Appearance, 4 Review.</summary>
    public int LastTabIndex => 4;

    /// <summary>Display name of the module chosen on the Review tab (first run), or empty.</summary>
    [ObservableProperty]
    private string _chosenModuleDisplay = "";

    [ObservableProperty]
    private bool _createDesktopShortcut;

    [ObservableProperty]
    private string _shortcutResultMessage = "";

    public bool HasShortcutResult => !string.IsNullOrEmpty(ShortcutResultMessage);

    [ObservableProperty]
    private string _gameInstallPath = "";

    [ObservableProperty]
    private string _nwnDocumentsPath = "";

    [ObservableProperty]
    private string _gameInstallValidation = "";

    [ObservableProperty]
    private IBrush _gameInstallValidationColor = SuccessBrush;

    [ObservableProperty]
    private string _nwnDocumentsValidation = "";

    [ObservableProperty]
    private IBrush _nwnDocumentsValidationColor = SuccessBrush;

    public bool HasGameInstallValidation => !string.IsNullOrEmpty(GameInstallValidation);
    public bool HasNwnDocumentsValidation => !string.IsNullOrEmpty(NwnDocumentsValidation);

    [ObservableProperty]
    private string _selectedTheme = "Light";

    [ObservableProperty]
    private double _fontSizePoints = 14.0;

    public string FontSizePointsText => $"{(int)FontSizePoints}pt";

    // Logging settings
    [ObservableProperty]
    private string _selectedLogLevel = "INFO";

    [ObservableProperty]
    private int _logRetentionSessions = 3;

    [ObservableProperty]
    private bool _isSharedLogging;

    public string LogRetentionText => $"{LogRetentionSessions} session{(LogRetentionSessions == 1 ? "" : "s")}";

    // Backup settings
    [ObservableProperty]
    private int _backupRetentionDays = 30;

    public string BackupRetentionText => $"{BackupRetentionDays} day{(BackupRetentionDays == 1 ? "" : "s")}";

    public ObservableCollection<string> AvailableLogLevels { get; } = new()
    {
        "TRACE", "DEBUG", "INFO", "WARN", "ERROR"
    };

    public ObservableCollection<string> AvailableThemes { get; } = new();

    public ObservableCollection<ToolInfo> DetectedTools { get; }

    public SettingsWindowViewModel(Window window)
        : this(window, SettingsSetupMode.Normal) { }

    public SettingsWindowViewModel(Window window, SettingsSetupMode setupMode)
        : this(window, setupMode, headless: false, appVersion: VersionHelper.GetVersion()) { }

    private SettingsWindowViewModel(Window? window, SettingsSetupMode setupMode, bool headless, string appVersion)
    {
        _window = window;
        _headless = headless;
        _appVersion = appVersion;
        SetupMode = setupMode;

        // Store originals for cancel revert
        _originalFontSizePoints = RadoubSettings.Instance.SharedFontSize;

        if (!headless)
        {
            _originalThemeId = ThemeManager.Instance.CurrentTheme?.Plugin.Id;

            // Load available themes
            foreach (var theme in ThemeManager.Instance.AvailableThemes)
            {
                AvailableThemes.Add(theme.Plugin.Name);
            }
        }

        // Load current settings
        LoadSettings();

        // Get detected tools (ToolLauncherService is reachable headless; tolerate failure)
        DetectedTools = headless
            ? new ObservableCollection<ToolInfo>()
            : new ObservableCollection<ToolInfo>(ToolLauncherService.Instance.Tools);

        // Set current theme
        if (!headless)
        {
            var currentTheme = ThemeManager.Instance.CurrentTheme;
            if (currentTheme != null && AvailableThemes.Contains(currentTheme.Plugin.Name))
            {
                SelectedTheme = currentTheme.Plugin.Name;
            }
        }
    }

    /// <summary>Headless factory for unit tests (no Window, no ThemeManager) (#2419).</summary>
    public static SettingsWindowViewModel CreateForTest(SettingsSetupMode mode, string appVersion) =>
        new(window: null, mode, headless: true, appVersion);

    /// <summary>Test hook: simulate the user choosing a module on the Review tab.</summary>
    internal void SetChosenModuleForTest(string modulePath)
    {
        _chosenModulePath = modulePath;
        ChosenModuleDisplay = System.IO.Path.GetFileName(modulePath);
    }

    private void LoadSettings()
    {
        var sharedSettings = RadoubSettings.Instance;
        var localSettings = SettingsService.Instance;

        GameInstallPath = sharedSettings.BaseGameInstallPath ?? "";
        NwnDocumentsPath = sharedSettings.NeverwinterNightsPath ?? "";
        FontSizePoints = sharedSettings.SharedFontSize;

        // Logging settings — show effective log level
        LogRetentionSessions = localSettings.LogRetentionSessions;
        IsSharedLogging = sharedSettings.UseSharedLogging;
        var effectiveLevel = sharedSettings.UseSharedLogging
            ? sharedSettings.SharedLogLevel
            : localSettings.CurrentLogLevel;
        SelectedLogLevel = effectiveLevel.ToString();

        // Backup settings
        BackupRetentionDays = sharedSettings.BackupRetentionDays;

        // Validate existing paths
        if (!string.IsNullOrEmpty(GameInstallPath))
        {
            ValidateGameInstallPath(GameInstallPath);
        }
        if (!string.IsNullOrEmpty(NwnDocumentsPath))
        {
            ValidateNwnDocumentsPath(NwnDocumentsPath);
        }
    }

    partial void OnGameInstallPathChanged(string value)
    {
        ValidateGameInstallPath(value);
    }

    partial void OnNwnDocumentsPathChanged(string value)
    {
        ValidateNwnDocumentsPath(value);
    }

    private void ValidateGameInstallPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            GameInstallValidation = "";
            OnPropertyChanged(nameof(HasGameInstallValidation));
            return;
        }

        var result = ResourcePathDetector.ValidateBaseGamePathWithMessage(path);
        GameInstallValidation = result.Message;
        GameInstallValidationColor = result.IsValid ? SuccessBrush : ErrorBrush;
        OnPropertyChanged(nameof(HasGameInstallValidation));
    }

    private void ValidateNwnDocumentsPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            NwnDocumentsValidation = "";
            OnPropertyChanged(nameof(HasNwnDocumentsValidation));
            return;
        }

        var result = ResourcePathDetector.ValidateGamePathWithMessage(path);
        NwnDocumentsValidation = result.Message;
        NwnDocumentsValidationColor = result.IsValid ? SuccessBrush : ErrorBrush;
        OnPropertyChanged(nameof(HasNwnDocumentsValidation));
    }

    partial void OnFontSizePointsChanged(double value)
    {
        OnPropertyChanged(nameof(FontSizePointsText));

        // Live preview: write the global font size as the slider moves. Trebuchet is the
        // sole authority for SharedFontSize (#2152); all tools read this value on launch.
        RadoubSettings.Instance.SharedFontSize = value;
    }

    partial void OnLogRetentionSessionsChanged(int value)
    {
        OnPropertyChanged(nameof(LogRetentionText));
    }

    partial void OnBackupRetentionDaysChanged(int value)
    {
        OnPropertyChanged(nameof(BackupRetentionText));
    }

    partial void OnSelectedThemeChanged(string value)
    {
        // Apply theme immediately for live preview
        var selectedThemeInfo = ThemeManager.Instance.AvailableThemes
            .FirstOrDefault(t => t.Plugin.Name == value);
        if (selectedThemeInfo != null)
        {
            ThemeManager.Instance.ApplyTheme(selectedThemeInfo.Plugin.Id);
        }
    }

    [RelayCommand]
    private async Task BrowseGamePath()
    {
        var folder = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select NWN Base Game Installation (contains data\\ folder)",
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            GameInstallPath = folder[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private void AutoDetectGamePath()
    {
        var detected = ResourcePathDetector.AutoDetectBaseGamePath();
        if (!string.IsNullOrEmpty(detected))
        {
            GameInstallPath = detected;
        }
        else
        {
            GameInstallValidation = "Could not auto-detect. Please browse manually.";
            GameInstallValidationColor = ErrorBrush;
            OnPropertyChanged(nameof(HasGameInstallValidation));
        }
    }

    [RelayCommand]
    private async Task BrowseDocumentsPath()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var defaultPath = System.IO.Path.Combine(documentsPath, "Neverwinter Nights");

        var folder = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select NWN Documents Folder (Documents\\Neverwinter Nights)",
            AllowMultiple = false,
            SuggestedStartLocation = System.IO.Directory.Exists(defaultPath)
                ? await _window.StorageProvider.TryGetFolderFromPathAsync(new Uri(defaultPath))
                : await _window.StorageProvider.TryGetFolderFromPathAsync(new Uri(documentsPath))
        });

        if (folder.Count > 0)
        {
            NwnDocumentsPath = folder[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private void AutoDetectDocumentsPath()
    {
        var detected = ResourcePathDetector.AutoDetectGamePath();
        if (!string.IsNullOrEmpty(detected))
        {
            NwnDocumentsPath = detected;
        }
        else
        {
            NwnDocumentsValidation = "Could not auto-detect. Please browse manually.";
            NwnDocumentsValidationColor = ErrorBrush;
            OnPropertyChanged(nameof(HasNwnDocumentsValidation));
        }
    }

    /// <summary>Advance to the next tab (convenience in setup mode); clamps at the last tab.</summary>
    [RelayCommand]
    private void Next()
    {
        if (SelectedTabIndex < LastTabIndex)
            SelectedTabIndex++;
    }

    /// <summary>
    /// Review-tab module picker (first run only): reuse the known-working module browser
    /// (#2419). Stores the selection; it is applied on Save. Skipping leaves it unset.
    /// </summary>
    [RelayCommand]
    private async Task ChooseModule()
    {
        if (_window == null) return; // headless

        try
        {
            var browser = new ModuleBrowserWindow(RadoubSettings.Instance.NeverwinterNightsPath);
            var result = await browser.ShowDialog<string?>(_window);
            if (!string.IsNullOrEmpty(result))
            {
                _chosenModulePath = result;
                ChosenModuleDisplay = System.IO.Path.GetFileName(result);
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Module picker failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Save()
    {
        var sharedSettings = RadoubSettings.Instance;
        var localSettings = SettingsService.Instance;

        sharedSettings.BaseGameInstallPath = GameInstallPath;
        sharedSettings.NeverwinterNightsPath = NwnDocumentsPath;
        sharedSettings.SharedFontSize = FontSizePoints;

        // Logging settings
        localSettings.LogRetentionSessions = LogRetentionSessions;
        if (Enum.TryParse<LogLevel>(SelectedLogLevel, out var logLevel))
        {
            localSettings.CurrentLogLevel = logLevel;
        }

        // Backup settings
        sharedSettings.BackupRetentionDays = BackupRetentionDays;

        // Save and apply selected theme (skipped headless — no ThemeManager)
        if (!_headless)
        {
            var selectedThemeInfo = ThemeManager.Instance.AvailableThemes
                .FirstOrDefault(t => t.Plugin.Name == SelectedTheme);
            if (selectedThemeInfo != null)
            {
                var themeId = selectedThemeInfo.Plugin.Id;

                // Save theme to shared settings (centralized theme management)
                sharedSettings.SharedThemeId = themeId;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Set shared theme: {themeId}");

                ThemeManager.Instance.ApplyTheme(themeId);
            }
        }

        if (IsSetupMode)
            CompleteSetup(openModule: IsWelcome, createShortcut: IsWelcome && CreateDesktopShortcut);

        _window?.Close();
    }

    [RelayCommand]
    private void Cancel()
    {
        // Revert theme if changed
        if (!_headless && _originalThemeId != null)
        {
            ThemeManager.Instance.ApplyTheme(_originalThemeId);
        }

        // Revert font size if changed
        RadoubSettings.Instance.SharedFontSize = _originalFontSizePoints;

        // B1 (#2419): dismissing setup still marks first-run complete for this version
        // so we do not re-nag every launch. No module open, no shortcut on cancel.
        if (IsSetupMode)
            CompleteSetup(openModule: false, createShortcut: false);

        _window?.Close();
    }

    /// <summary>
    /// Persist first-run/version-gate completion (#2419): stamp WizardHasRun +
    /// LastSetupVersion + acknowledge all gaps. Optionally open the chosen module and
    /// create a desktop shortcut (Welcome/Save only). Never throws out to the caller.
    /// </summary>
    private void CompleteSetup(bool openModule, bool createShortcut)
    {
        RadoubSettings.Instance.AcknowledgeWizardGaps(SetupGapKeys, _appVersion);

        if (openModule && !string.IsNullOrEmpty(_chosenModulePath))
        {
            try
            {
                RadoubSettings.Instance.CurrentModulePath = _chosenModulePath;
                SettingsService.Instance.AddRecentModule(_chosenModulePath);
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Setup opened module: {UnifiedLogger.SanitizePath(_chosenModulePath)}");
            }
            catch (Exception ex)
            {
                // A failed module open must not block setup completing.
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Setup could not open module: {ex.Message}");
            }
        }

        if (createShortcut && !_headless)
        {
            try
            {
                var result = DesktopShortcutService.CreateForCurrentApp(ResolveIconPath());
                ShortcutResultMessage = result.Success
                    ? $"Shortcut created: {result.Path}"
                    : $"Shortcut failed: {result.Error}";
                OnPropertyChanged(nameof(HasShortcutResult));
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Desktop shortcut failed: {ex.Message}");
            }
        }
    }

    // Stable gap keys (persisted in RadoubSettings.AcknowledgedWizardGaps). Shared with
    // the launch-time decider in App so first-run completion acknowledges every step.
    public const string GapGamePath = "gamePath";
    public const string GapAppearance = "appearance";
    public const string GapLogging = "logging";
    public const string GapBackup = "backup";

    /// <summary>Gap keys acknowledged on setup completion — mirrors the registry in App.</summary>
    private static readonly string[] SetupGapKeys = { GapGamePath, GapAppearance, GapLogging, GapBackup };

    private static string? ResolveIconPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var ico = System.IO.Path.Combine(baseDir, "Assets", "Trebuchet.ico");
        return System.IO.File.Exists(ico) ? ico : null;
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            // Open the Radoub root folder to show all tool logs
            var radoubFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Radoub");

            if (System.IO.Directory.Exists(radoubFolder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = radoubFolder,
                    UseShellExecute = true
                })?.Dispose();
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open log folder: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CleanupLogs()
    {
        try
        {
            var radoubFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Radoub");

            if (!System.IO.Directory.Exists(radoubFolder))
                return;

            int deletedCount = 0;

            // Nuke all Logs directories in all tool folders
            foreach (var toolDir in System.IO.Directory.GetDirectories(radoubFolder))
            {
                var logsDir = System.IO.Path.Combine(toolDir, "Logs");
                if (System.IO.Directory.Exists(logsDir))
                {
                    System.IO.Directory.Delete(logsDir, true);
                    deletedCount++;
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Deleted logs: {System.IO.Path.GetFileName(toolDir)}/Logs");
                }
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Cleaned up {deletedCount} log directories");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to cleanup logs: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExportLogs()
    {
        try
        {
            var logFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Radoub", "Trebuchet", "Logs");

            if (!System.IO.Directory.Exists(logFolder))
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "No logs to export");
                return;
            }

            var options = new FilePickerSaveOptions
            {
                Title = "Export Logs for Support",
                SuggestedFileName = $"Trebuchet_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.zip",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("ZIP Archive") { Patterns = new[] { "*.zip" } }
                }
            };

            var file = await _window.StorageProvider.SaveFilePickerAsync(options);
            if (file == null) return;

            var result = file.Path.LocalPath;
            if (System.IO.File.Exists(result)) System.IO.File.Delete(result);

            System.IO.Compression.ZipFile.CreateFromDirectory(logFolder, result);

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Exported logs to: ~/{System.IO.Path.GetFileName(result)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to export logs: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteAllBackups()
    {
        try
        {
            var (fileCount, totalBytes) = BackupCleanupService.GetBackupSummary();
            if (fileCount == 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "No backups to delete");
                return;
            }

            var sizeMb = totalBytes / (1024.0 * 1024.0);
            var dialog = new ConfirmDialog(
                "Delete All Backups",
                $"Delete all backups? ({fileCount} files, {sizeMb:F1} MB)\nThis cannot be undone.");
            await dialog.ShowDialog(_window);

            if (dialog.Confirmed)
            {
                BackupCleanupService.DeleteAllBackups();
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Deleted all backups ({fileCount} files, {sizeMb:F1} MB)");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to delete backups: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenThemeEditor()
    {
        try
        {
            var viewModel = new ThemeEditorViewModel();
            var editorWindow = new ThemeEditorWindow(viewModel);
            editorWindow.Show(_window);

            UnifiedLogger.LogApplication(LogLevel.INFO, "Opened Theme Editor");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open Theme Editor: {ex.Message}");
        }
    }
}
