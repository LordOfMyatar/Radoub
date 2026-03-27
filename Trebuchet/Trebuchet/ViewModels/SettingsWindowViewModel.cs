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
using RadoubLauncher.Services;
using RadoubLauncher.Views;

namespace RadoubLauncher.ViewModels;

public partial class SettingsWindowViewModel : ObservableObject
{
    private readonly Window _window;
    private readonly string? _originalThemeId;
    private readonly double _originalFontSizeScale;
    private static IBrush SuccessBrush => BrushManager.GetSuccessBrush();
    private static IBrush ErrorBrush => BrushManager.GetErrorBrush();

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
    private double _fontSizeScale = 1.0;

    public string FontSizePercentText => $"{(int)(FontSizeScale * 100)}%";

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
    {
        _window = window;

        // Store originals for cancel revert
        _originalThemeId = ThemeManager.Instance.CurrentTheme?.Plugin.Id;
        _originalFontSizeScale = SettingsService.Instance.FontSizeScale;

        // Load current settings
        LoadSettings();

        // Get detected tools
        DetectedTools = new ObservableCollection<ToolInfo>(ToolLauncherService.Instance.Tools);

        // Load available themes
        foreach (var theme in ThemeManager.Instance.AvailableThemes)
        {
            AvailableThemes.Add(theme.Plugin.Name);
        }

        // Set current theme
        var currentTheme = ThemeManager.Instance.CurrentTheme;
        if (currentTheme != null && AvailableThemes.Contains(currentTheme.Plugin.Name))
        {
            SelectedTheme = currentTheme.Plugin.Name;
        }
    }

    private void LoadSettings()
    {
        var sharedSettings = RadoubSettings.Instance;
        var localSettings = SettingsService.Instance;

        GameInstallPath = sharedSettings.BaseGameInstallPath ?? "";
        NwnDocumentsPath = sharedSettings.NeverwinterNightsPath ?? "";
        FontSizeScale = localSettings.FontSizeScale;

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

    partial void OnFontSizeScaleChanged(double value)
    {
        OnPropertyChanged(nameof(FontSizePercentText));

        // Live preview: apply font scale immediately as slider moves
        SettingsService.Instance.FontSizeScale = value;
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

    [RelayCommand]
    private void Save()
    {
        var sharedSettings = RadoubSettings.Instance;
        var localSettings = SettingsService.Instance;

        sharedSettings.BaseGameInstallPath = GameInstallPath;
        sharedSettings.NeverwinterNightsPath = NwnDocumentsPath;
        localSettings.FontSizeScale = FontSizeScale;

        // Logging settings
        localSettings.LogRetentionSessions = LogRetentionSessions;
        if (Enum.TryParse<LogLevel>(SelectedLogLevel, out var logLevel))
        {
            localSettings.CurrentLogLevel = logLevel;
        }

        // Backup settings
        sharedSettings.BackupRetentionDays = BackupRetentionDays;

        // Save and apply selected theme
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

        _window.Close();
    }

    [RelayCommand]
    private void Cancel()
    {
        // Revert theme if changed
        if (_originalThemeId != null)
        {
            ThemeManager.Instance.ApplyTheme(_originalThemeId);
        }

        // Revert font scale if changed
        SettingsService.Instance.FontSizeScale = _originalFontSizeScale;

        _window.Close();
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
