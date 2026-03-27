using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Services;
using System;
using System.IO;

namespace MerchantEditor.Views;

public partial class SettingsWindow : Window
{
    private bool _isInitializing = true;
    private MainWindow? _mainWindow;

    public SettingsWindow()
    {
        InitializeComponent();

        LoadSettings();
        UpdateCacheInfo();

        _isInitializing = false;
    }

    /// <summary>
    /// Set the main window reference for cache operations.
    /// </summary>
    public void SetMainWindow(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        UpdateCacheInfo();
    }

    private void UpdateCacheInfo()
    {
        var stats = _mainWindow?.GetPaletteCacheStatistics();

        if (stats != null && stats.TotalItems > 0)
        {
            CacheStatusText.Text = "Cached";
            CacheStatusText.Foreground = GetSuccessBrush();
            CacheItemCountText.Text = $"{stats.TotalItems:N0}";
            CacheSizeText.Text = $"{stats.TotalSizeKB:N1} KB";
            CacheCreatedText.Text = $"{stats.SourceCounts.Count} source(s)";
        }
        else
        {
            CacheStatusText.Text = "No cache";
            CacheStatusText.Foreground = GetWarningBrush();
            CacheItemCountText.Text = "-";
            CacheSizeText.Text = "-";
            CacheCreatedText.Text = "-";
        }
    }

    private async void OnClearCacheClick(object? sender, RoutedEventArgs e)
    {
        if (_mainWindow == null)
            return;

        CacheStatusText.Text = "Rebuilding...";
        CacheStatusText.Foreground = GetWarningBrush();
        ClearCacheButton.IsEnabled = false;

        try
        {
            await _mainWindow.ClearAndReloadPaletteCacheAsync();
            CacheStatusText.Text = "Rebuilt";
            CacheStatusText.Foreground = GetSuccessBrush();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Cache rebuild failed: {ex.Message}");
            CacheStatusText.Text = "Rebuild failed";
            CacheStatusText.Foreground = GetErrorBrush();
        }
        finally
        {
            ClearCacheButton.IsEnabled = true;
            UpdateCacheInfo();
        }
    }

    private void LoadSettings()
    {
        LoadModuleConfiguration();

        var sharedSettings = RadoubSettings.Instance;

        // Paths
        BaseGamePathTextBox.Text = sharedSettings.BaseGameInstallPath;
        GamePathTextBox.Text = sharedSettings.NeverwinterNightsPath;

        ValidateBaseGamePath();
        ValidateGamePath();
    }

    private async void OnBrowseBaseGamePathClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select NWN Game Installation Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            BaseGamePathTextBox.Text = folders[0].Path.LocalPath;
            RadoubSettings.Instance.BaseGameInstallPath = folders[0].Path.LocalPath;
            ValidateBaseGamePath();
        }
    }

    private void OnAutoDetectBaseGamePathClick(object? sender, RoutedEventArgs e)
    {
        var detected = ResourcePathDetector.AutoDetectBaseGamePath();
        if (!string.IsNullOrEmpty(detected))
        {
            BaseGamePathTextBox.Text = detected;
            RadoubSettings.Instance.BaseGameInstallPath = detected;
            ValidateBaseGamePath();
        }
        else
        {
            BaseGamePathValidation.Text = "Could not auto-detect game path";
            BaseGamePathValidation.Foreground = GetWarningBrush();
        }
    }

    private async void OnBrowseGamePathClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select NWN User Data Folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            GamePathTextBox.Text = folders[0].Path.LocalPath;
            RadoubSettings.Instance.NeverwinterNightsPath = folders[0].Path.LocalPath;
            ValidateGamePath();
        }
    }

    private void OnAutoDetectGamePathClick(object? sender, RoutedEventArgs e)
    {
        var detected = ResourcePathDetector.AutoDetectGamePath();
        if (!string.IsNullOrEmpty(detected))
        {
            GamePathTextBox.Text = detected;
            RadoubSettings.Instance.NeverwinterNightsPath = detected;
            ValidateGamePath();
        }
        else
        {
            GamePathValidation.Text = "Could not auto-detect user data path";
            GamePathValidation.Foreground = GetWarningBrush();
        }
    }

    private void ValidateBaseGamePath()
    {
        var path = BaseGamePathTextBox.Text;
        if (string.IsNullOrEmpty(path))
        {
            BaseGamePathValidation.Text = "";
            return;
        }

        if (Directory.Exists(path))
        {
            var dataPath = Path.Combine(path, "data");
            var keyFile = Path.Combine(path, "data", "nwn_base.key");

            if (Directory.Exists(dataPath) || File.Exists(keyFile))
            {
                BaseGamePathValidation.Text = "\u2713 Valid NWN installation detected";
                BaseGamePathValidation.Foreground = GetSuccessBrush();
            }
            else
            {
                BaseGamePathValidation.Text = "\u26A0 Directory exists but may not be NWN installation";
                BaseGamePathValidation.Foreground = GetWarningBrush();
            }
        }
        else
        {
            BaseGamePathValidation.Text = "\u2717 Directory does not exist";
            BaseGamePathValidation.Foreground = GetErrorBrush();
        }
    }

    private void ValidateGamePath()
    {
        var path = GamePathTextBox.Text;
        if (string.IsNullOrEmpty(path))
        {
            GamePathValidation.Text = "";
            return;
        }

        if (Directory.Exists(path))
        {
            var modulesPath = Path.Combine(path, "modules");
            if (Directory.Exists(modulesPath))
            {
                GamePathValidation.Text = "\u2713 Valid NWN user data directory";
                GamePathValidation.Foreground = GetSuccessBrush();
            }
            else
            {
                GamePathValidation.Text = "\u26A0 Directory exists but no modules folder found";
                GamePathValidation.Foreground = GetWarningBrush();
            }
        }
        else
        {
            GamePathValidation.Text = "\u2717 Directory does not exist";
            GamePathValidation.Foreground = GetErrorBrush();
        }
    }

    private IBrush GetSuccessBrush() => BrushManager.GetSuccessBrush(this);
    private IBrush GetWarningBrush() => BrushManager.GetWarningBrush(this);
    private IBrush GetErrorBrush() => BrushManager.GetErrorBrush(this);

    #region Module Configuration (#1325, #1322)

    private void LoadModuleConfiguration()
    {
        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        if (RadoubSettings.IsValidModulePath(modulePath))
        {
            string? workingDir = modulePath;
            if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
            {
                var moduleName = Path.GetFileNameWithoutExtension(modulePath);
                var moduleDir = Path.GetDirectoryName(modulePath);
                if (!string.IsNullOrEmpty(moduleDir))
                {
                    var candidate = Path.Combine(moduleDir, moduleName);
                    if (Directory.Exists(candidate))
                        workingDir = candidate;
                }
            }

            string? displayName = null;
            if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
            {
                var ifoPath = Path.Combine(workingDir, "module.ifo");
                if (File.Exists(ifoPath))
                {
                    try
                    {
                        var ifo = Radoub.Formats.Ifo.IfoReader.Read(ifoPath);
                        displayName = ifo.ModuleName.GetDefault();
                    }
                    catch { /* fall through to path-based name */ }
                }
            }

            CurrentModuleText.Text = displayName ?? Path.GetFileName(modulePath);
            CurrentModuleText.Foreground = BrushManager.GetInfoBrush(this);
        }
        else
        {
            CurrentModuleText.Text = "No module selected";
            CurrentModuleText.Foreground = BrushManager.GetWarningBrush(this);
        }
    }

    private void OnConfigureInTrebuchetClick(object? sender, RoutedEventArgs e)
    {
        var trebuchetPath = RadoubSettings.Instance.TrebuchetPath;

        if (string.IsNullOrEmpty(trebuchetPath) || !File.Exists(trebuchetPath))
        {
            TrebuchetStatusText.Text = "Trebuchet not found. Launch Trebuchet once to register its path.";
            TrebuchetStatusText.Foreground = BrushManager.GetWarningBrush(this);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = trebuchetPath,
                UseShellExecute = true
            });

            TrebuchetStatusText.Text = "Trebuchet launched. Restart this tool after changing module.";
            TrebuchetStatusText.Foreground = BrushManager.GetInfoBrush(this);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to launch Trebuchet: {ex.Message}");
            TrebuchetStatusText.Text = $"Failed to launch Trebuchet: {ex.Message}";
            TrebuchetStatusText.Foreground = BrushManager.GetErrorBrush(this);
        }
    }

    #endregion

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
